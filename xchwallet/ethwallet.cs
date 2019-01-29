using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Text;
using Nethereum.Web3;
using Nethereum.HdWallet;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace xchwallet
{

    public class EthWallet : BaseWallet
    {
        public const string TYPE = "ETH";

        const string PATH = "m/44'/60'/0'/x";
        const int TX_GAS = 21000;

        Web3 web3 = null;
        string gethTxScanAddress = null;
        WebClient scanClient = null;
        Wallet wallet = null;
        bool mainNet = false;

        ILogger logger;

        public EthWallet(ILogger logger, string seedHex, WalletContext db, bool mainNet, string gethAddress, string gethTxScanAddress) : base (db)
        {
            this.logger = logger;

            // create HD wallet from seed
            wallet = new Wallet(Utils.ParseHexString(seedHex), PATH);

            // create web3 client
            web3 = new Web3(gethAddress);

            // create http client
            this.gethTxScanAddress = gethTxScanAddress;
            scanClient = new WebClient();

            this.mainNet = mainNet;
            var netVersionTask = web3.Net.Version.SendRequestAsync();
            netVersionTask.Wait();
            if (mainNet)
                if (netVersionTask.Result != "1")
                    throw new Exception("client is on wrong network");
            if (!mainNet)
                if (netVersionTask.Result != "3")
                    throw new Exception("client is on wrong network");
        }

        public override string Type()
        {
            return EthWallet.TYPE;
        }

        public override bool IsMainnet()
        {
            return mainNet;
        }

        public override WalletAddr NewAddress(string tag)
        {
            var pathIndex = db.LastPathIndex + 1;
            // create new address that is unused
            var _tag = db.TagGetOrCreate(tag);
            var acct = wallet.GetAccount(pathIndex);
            var address = new WalletAddr(_tag, PATH.Replace("x", pathIndex.ToString()), pathIndex, acct.Address);
            // regsiter address with gethtxscan
            scanClient.DownloadString(gethTxScanAddress + "/watch_account/" + acct.Address);
            // add to address list
            db.WalletAddrs.Add(address);
            // update last path index and return address
            db.LastPathIndex = pathIndex;
            return address;
        }

        struct scantx
        {
            public string txid;
            public string from_;
            public string to;
            public string value;
            public long block_num;
            public long date;
            public scantx(string txid, string from_, string to, string value, long blockNum, long date)
            {
                this.txid = txid;
                this.from_ = from_;
                this.to = to;
                this.value = value;
                this.block_num = blockNum;
                this.date = date;
            }
        }

        public override void UpdateFromBlockchain()
        {
            foreach (var tag in GetTags())
            {
                foreach (var addr in tag.Addrs)
                    UpdateTxs(addr);
            }
        }

        void UpdateTxs(WalletAddr address)
        {
            var blockNumTask = web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            blockNumTask.Wait();
            var blockNum = (long)blockNumTask.Result.Value;
            var json = scanClient.DownloadString(gethTxScanAddress + "/list_transactions/" + address.Address);
            var scantxs = JsonConvert.DeserializeObject<List<scantx>>(json);
            foreach (var scantx in scantxs)
            {
                long confirmations = 0;
                if (scantx.block_num > 0)
                    confirmations = blockNum - scantx.block_num;
                var ctx = db.ChainTxGet(scantx.txid);
                if (ctx == null)
                {
                    ctx = new ChainTx(scantx.txid, scantx.date, scantx.from_, address.Address,
                        BigInteger.Parse(scantx.value), -1, confirmations);
                    db.ChainTxs.Add(ctx);
                }
                else
                {
                    ctx.Confirmations = confirmations;
                    db.ChainTxs.Update(ctx);
                }
                var wtx = db.TxGet(address, ctx);
                if (wtx == null)
                {
                    wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Incomming };
                    db.WalletTxs.Add(wtx);
                }
            }
        }

        public override IEnumerable<WalletTx> GetTransactions(string tag)
        {
            return db.TxsGet(tag);
        }

        public override IEnumerable<WalletTx> GetAddrTransactions(string address)
        {
            var addr = db.AddrGet(address);
            System.Diagnostics.Debug.Assert(addr != null);
            return addr.Txs;
        }

        public override BigInteger GetBalance(string tag)
        {
            BigInteger total = 0;
            foreach (var addr in db.AddrsGet(tag))
                total += GetAddrBalance(addr);
            return total;
        }

        public override BigInteger GetAddrBalance(string address)
        {
            var addr = db.AddrGet(address);
            System.Diagnostics.Debug.Assert(addr != null);
            return GetAddrBalance(addr);
        }

        public BigInteger GetAddrBalance(WalletAddr addr)
        {
            BigInteger total = 0;
            foreach (var tx in addr.Txs)
                if (tx.Direction == WalletDirection.Incomming)
                    total += tx.ChainTx.Amount;
                else if (tx.Direction == WalletDirection.Outgoing)
                {
                    total -= tx.ChainTx.Amount;
                    total -= tx.ChainTx.Fee;
                }
            return total;
        }

        WalletError CreateSpendTxs(IEnumerable<WalletAddr> candidates, string to, BigInteger amount, BigInteger gasPrice, BigInteger gasLimit, BigInteger feeMax,
            out List<Tuple<string, string>> signedSpendTxs)
        {
            signedSpendTxs = new List<Tuple<string, string>>();
            var amountRemaining = amount;
            var fee = gasPrice * gasLimit;
            var feeTotal = new BigInteger(0);
            foreach (var acct in candidates)
            {
                if (amountRemaining == 0)
                    break;
                // get transaction count and balance for the account
                var txCountTask = web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(acct.Address);
                txCountTask.Wait();
                var txCount = txCountTask.Result.Value;
                var balance = GetAddrBalance(acct.Address);
                // if the balance is greater then the fee we can use this account
                if (balance > fee)
                {
                    // calcuate the actual amount we will use from this account
                    var amountThisAddress = balance - fee;
                    if (amountThisAddress > amountRemaining)
                        amountThisAddress = amountRemaining;
                    // create signed transaction
                    var account = wallet.GetAccount(acct.Address);
                    var signedTx = Web3.OfflineTransactionSigner.SignTransaction(account.PrivateKey, to, amountThisAddress, txCount, gasPrice, gasLimit);
                    // update spend tx list and amount remaining
                    amountRemaining -= amountThisAddress;
                    signedSpendTxs.Add(new Tuple<string, string>(acct.Address, signedTx));
                }
                feeTotal += fee;
            }
            logger.LogDebug("feeMax {0}, feeTotal {1}", feeMax, feeTotal);
            logger.LogDebug("amountRemaining {0}", amountRemaining);
            if (feeTotal > feeMax)
                return WalletError.MaxFeeBreached;
            if (amountRemaining != 0)
                return WalletError.InsufficientFunds;
            return WalletError.Success;
        }

        void AddOutgoingTx(string from, string signedTx)
        {
            var address = db.AddrGet(from);
            var tx = new Transaction(signedTx.HexToByteArray());
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fee = HexBigIntegerConvertorExtensions.HexToBigInteger(tx.GasLimit.ToHex(), false) * HexBigIntegerConvertorExtensions.HexToBigInteger(tx.GasPrice.ToHex(), false);
            var amount = HexBigIntegerConvertorExtensions.HexToBigInteger(tx.Value.ToHex(), false);
            logger.LogDebug("outgoing tx: amount: {0}, fee: {1}", amount, fee);
            var ctx = new ChainTx(tx.Hash.ToHex(true), date, from, tx.ReceiveAddress.ToHex(true), amount, fee, 0);
            db.ChainTxs.Add(ctx);
            var wtx = new WalletTx{ChainTx=ctx, Address=address, Direction=WalletDirection.Outgoing};
            db.WalletTxs.Add(wtx);
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids)
        {
            txids = new List<string>();
            // get gas price
            //var gasPriceTask = web3.Eth.GasPrice.SendRequestAsync();
            //gasPriceTask.Wait();
            //var gasPrice = gasPriceTask.Result.Value;
            var gasPrice = feeUnit;
            // create spend transaction from accounts
            var accts = GetAddresses(tag);
            List<Tuple<string, string>> signedSpendTxs;
                    //TODO: check gas price, and/or allow custom setting (max total wei too?)
            var res = CreateSpendTxs(accts, to, amount, gasPrice, TX_GAS, feeMax, out signedSpendTxs);
            if (res == WalletError.Success)
            {
                // send each raw signed transaction and get the txid
                foreach (var tx in signedSpendTxs)
                {
                    try
                    {
                        var sendTxTask = web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(tx.Item2);
                        sendTxTask.Wait();
                        var txid = sendTxTask.Result;
                        ((List<string>)txids).Add(txid);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("{0}", ex);
                        return WalletError.PartialBroadcast;
                    }
                    // add to wallet data
                    AddOutgoingTx(tx.Item1, tx.Item2);
                }
            }
            return res;
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids)
        {
            BigInteger gasPrice = feeUnit;

            txids = new List<string>();
            var to = NewOrUnusedAddress(tagTo);
            BigInteger balance = 0;
            var accts = new List<WalletAddr>();
            foreach (var tag in tagFrom)
            {
                balance += GetBalance(tag);
                var tagAccts = GetAddresses(tag);
                accts.AddRange(tagAccts);
            }
            List<Tuple<string, string>> signedSpendTxs;
            var res = CreateSpendTxs(accts, to.Address, balance, gasPrice, TX_GAS, feeMax, out signedSpendTxs);
            if (res == WalletError.Success)
            {
                // send each raw signed transaction and get the txid
                foreach (var tx in signedSpendTxs)
                {
                    try
                    {
                        var sendTxTask = web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(tx.Item2);
                        sendTxTask.Wait();
                        var txid = sendTxTask.Result;
                        ((List<string>)txids).Add(txid);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("{0}", ex);
                        return WalletError.PartialBroadcast;
                    }
                    // add to wallet data
                    AddOutgoingTx(tx.Item1, tx.Item2);
                }
            }
            return res;
        }

        public override bool ValidateAddress(string address)
        {
            throw new Exception("not yet implemented");
        }

        public override string AmountToString(BigInteger value)
        {
            var _scale = new decimal(1, 0, 0, false, 18);
            var d = (decimal)value * _scale;
            return d.ToString();
        }

        public override BigInteger StringToAmount(string value)
        {
            var _scale = new decimal(1, 0, 0, false, 18);
            var d = decimal.Parse(value) / _scale;
            return (BigInteger)d;
        }
    }
}
