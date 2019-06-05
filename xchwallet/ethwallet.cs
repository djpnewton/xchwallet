using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Text;
using System.Linq;
using Nethereum.Web3;
using Nethereum.HdWallet;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

namespace xchwallet
{

    public class EthWallet : BaseWallet
    {
        public const string TYPE = "ETH";
        protected override string _type()
        {
            return TYPE;
        }

        const string PATH = "m/44'/60'/0'/x";
        const int TX_GAS = 21000;

        Web3 web3 = null;
        string gethTxScanAddress = null;
        WebClient scanClient = null;
        Wallet wallet = null;

        public EthWallet(ILogger logger, WalletContext db, bool mainnet, string gethAddress, string gethTxScanAddress) : base (logger, db, mainnet)
        {
            this.logger = logger;

            // create HD wallet from seed
            wallet = new Wallet(Utils.ParseHexString(seedHex), PATH);

            // create web3 client
            web3 = new Web3(gethAddress);

            // create http client
            this.gethTxScanAddress = gethTxScanAddress;
            scanClient = new WebClient();

            var netVersionTask = web3.Net.Version.SendRequestAsync();
            netVersionTask.Wait();
            if (mainnet)
                if (netVersionTask.Result != "1")
                    throw new Exception("client is on wrong network");
            if (!mainnet)
                if (netVersionTask.Result != "3")
                    throw new Exception("client is on wrong network");
        }

        public override bool IsMainnet()
        {
            return mainnet;
        }

        public override LedgerModel LedgerModel { get { return LedgerModel.Account; } }

        public override WalletAddr NewAddress(string tag)
        {
            var pathIndex = db.LastPathIndex + 1;
            // create new address that is unused
            var _tag = db.TagGet(tag);
            Util.WalletAssert(_tag != null, $"Tag '{tag}' does not exist");
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

        public override void UpdateFromBlockchain(IDbContextTransaction dbtx)
        {
            System.Diagnostics.Debug.Assert(dbtx != null);
            foreach (var tag in GetTags())
                foreach (var addr in tag.Addrs)
                {
                    UpdateTxs(addr);
                    db.SaveChanges();
                }
            return;
        }

        void UpdateTxs(WalletAddr address)
        {
            //TODO - add attachment
            // - read the 'data' field (https://ethereum.stackexchange.com/questions/2466/how-do-i-send-an-arbitary-message-to-an-ethereum-address)

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
                // add/update chain tx
                var ctx = db.ChainTxGet(scantx.txid);
                if (ctx == null)
                {
                    ctx = new ChainTx(scantx.txid, scantx.date, -1, scantx.block_num, confirmations);
                    db.ChainTxs.Add(ctx);
                }
                else
                {
                    ctx.Height = scantx.block_num;
                    ctx.Confirmations = confirmations;
                    db.ChainTxs.Update(ctx);
                }

                // add output
                var o = db.TxOutputGet(scantx.txid, 0);
                if (o == null)
                {
                    o = new TxOutput(scantx.txid, scantx.to, 0, BigInteger.Parse(scantx.value));
                    o.ChainTx = ctx;
                    if (scantx.to == address.Address)
                        o.WalletAddr = address;
                    db.TxOutputs.Add(o);
                }
                else if (o.WalletAddr == null && o.Addr == address.Address)
                {
                    logger.LogInformation($"Updating output with no address: {scantx.txid}, {o.N}, {address.Address}");
                    o.WalletAddr = address;
                    db.TxOutputs.Update(o);
                }
                // add / update wallet tx
                var dir = scantx.to == address.Address ? WalletDirection.Incomming : WalletDirection.Outgoing;
                var wtx = db.TxGet(address, ctx, dir);
                if (wtx == null)
                {
                    wtx = new WalletTx { ChainTx = ctx, Address = address, Direction = dir, Acknowledged = false };
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
            Util.WalletAssert(addr != null, $"Address '{address}' does not exist");
            return addr.Txs;
        }

        public override BigInteger GetBalance(string tag, int minConfs=0)
        {
            BigInteger total = 0;
            foreach (var addr in db.AddrsGet(tag))
                total += GetAddrBalance(addr);
            return total;
        }

        public override BigInteger GetAddrBalance(string address, int minConfs=0)
        {
            var addr = db.AddrGet(address);
            Util.WalletAssert(addr != null, $"Address '{address}' does not exist");
            return GetAddrBalance(addr, minConfs);
        }

        WalletError CreateSpendTx(IEnumerable<WalletAddr> candidates, string to, BigInteger amount, BigInteger gasPrice, BigInteger gasLimit, BigInteger feeMax,
            out Tuple<string, string> signedSpendTx)
        {
            signedSpendTx = null;
            var fee = gasPrice * gasLimit;
            if (fee > feeMax)
                return WalletError.MaxFeeBreached;
            foreach (var acct in candidates)
            {
                // get transaction count and balance for the account
                var txCountTask = web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(acct.Address);
                txCountTask.Wait();
                var txCount = txCountTask.Result.Value;
                var balance = GetAddrBalance(acct.Address);
                // if the balance is sufficient then we can use this account
                if (balance >= fee + amount)
                {
                    // create signed transaction
                    var ethAccount = wallet.GetAccount(acct.Address);
                    var signedTx = Web3.OfflineTransactionSigner.SignTransaction(ethAccount.PrivateKey, to, amount, txCount, gasPrice, gasLimit);
                    // return success
                    signedSpendTx = new Tuple<string, string>(acct.Address, signedTx);
                    return WalletError.Success;
                }
            }
            return WalletError.InsufficientFunds;
        }

        WalletError CreateSpendTxs(IEnumerable<Tuple<WalletAddr, BigInteger>> candidates, string to, BigInteger amount, BigInteger gasPrice, BigInteger gasLimit, BigInteger feeMax,
            out List<Tuple<string, string>> signedSpendTxs)
        {
            signedSpendTxs = new List<Tuple<string, string>>();
            var amountRemaining = amount;
            var fee = gasPrice * gasLimit;
            var feeTotal = new BigInteger(0);
            foreach (var candidate in candidates)
            {
                if (amountRemaining == 0)
                    break;
                // get transaction count and balance for the account
                var acct = candidate.Item1;
                var txCountTask = web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(acct.Address);
                txCountTask.Wait();
                var txCount = txCountTask.Result.Value;
                var balance = candidate.Item2;
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

        WalletTx AddOutgoingTx(string from, string signedTx, WalletTxMeta meta)
        {
            var address = db.AddrGet(from);
            var tx = new Transaction(signedTx.HexToByteArray());
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fee = HexBigIntegerConvertorExtensions.HexToBigInteger(tx.GasLimit.ToHex(), false) * HexBigIntegerConvertorExtensions.HexToBigInteger(tx.GasPrice.ToHex(), false);
            var amount = HexBigIntegerConvertorExtensions.HexToBigInteger(tx.Value.ToHex(), false);
            logger.LogDebug("outgoing tx: amount: {0}, fee: {1}", amount, fee);
            // create chain tx
            var ctx = new ChainTx(tx.Hash.ToHex(true), date, fee, -1, 0);
            db.ChainTxs.Add(ctx);
            // create tx input
            var i = new TxInput(tx.Hash.ToHex(true), from, 0, amount + fee);
            i.ChainTx = ctx;
            i.WalletAddr = address;
            db.TxInputs.Add(i);
            // create tx output
            var o = new TxOutput(tx.Hash.ToHex(true), tx.ReceiveAddress.ToHex(true), 0, amount);
            o.ChainTx = ctx;
            db.TxOutputs.Add(o);
            // create wallet tx
            var wtx = new WalletTx{ChainTx=ctx, Address=address, Direction=WalletDirection.Outgoing};
            wtx.Meta = meta;
            db.WalletTxs.Add(wtx);
            return wtx;
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, WalletTxMeta meta=null)
        {
            wtxs = new List<WalletTx>();
            // get gas price
            //var gasPriceTask = web3.Eth.GasPrice.SendRequestAsync();
            //gasPriceTask.Wait();
            //var gasPrice = gasPriceTask.Result.Value;
            var gasPrice = feeUnit;
            // create spend transaction from accounts
            var accts = GetAddresses(tag);
            Tuple<string, string> signedSpendTx;
                    //TODO: check gas price, and/or allow custom setting (max total wei too?)
            var res = CreateSpendTx(accts, to, amount, gasPrice, TX_GAS, feeMax, out signedSpendTx);
            if (res == WalletError.Success)
            {
                // send the raw signed transaction and get the txid
                try
                {
                    var sendTxTask = web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedSpendTx.Item2);
                    sendTxTask.Wait();
                    var txid = sendTxTask.Result;
                    // add to wallet data
                    var wtx = AddOutgoingTx(signedSpendTx.Item1, signedSpendTx.Item2, meta);
                    ((List<WalletTx>)wtxs).Add(wtx);
                }
                catch (Exception ex)
                {
                    logger.LogError("{0}", ex);
                    return WalletError.FailedBroadcast;
                }
            }
            return res;
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, int minConfs=0)
        {
            BigInteger gasPrice = feeUnit;

            wtxs = new List<WalletTx>();
            var to = NewOrExistingAddress(tagTo);
            BigInteger balance = 0;
            var candidates = new List<Tuple<WalletAddr, BigInteger>>();
            foreach (var tag in tagFrom)
            {
                var tagAccts = GetAddresses(tag);
                foreach (var acct in tagAccts)
                {
                    var acctBal = GetAddrBalance(acct, minConfs);
                    if (acctBal > 0)
                        candidates.Add(new Tuple<WalletAddr, BigInteger>(acct, acctBal));
                    balance += acctBal;
                }
            }
            List<Tuple<string, string>> signedSpendTxs;
            var res = CreateSpendTxs(candidates, to.Address, balance, gasPrice, TX_GAS, feeMax, out signedSpendTxs);
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
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("{0}", ex);
                        return WalletError.PartialBroadcast;
                    }
                    // add to wallet data
                    var wtx = AddOutgoingTx(tx.Item1, tx.Item2, null);
                    ((List<WalletTx>)wtxs).Add(wtx);
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
