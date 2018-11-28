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
using NLog;

namespace xchwallet
{
    using Accts = Dictionary<string, List<EthAccount>>;
    using AcctTxs = Dictionary<string, List<EthTransaction>>;

    public class EthAccount : BaseAddress
    {
        public int PathIndex;

        public EthAccount(string tag, string path, string address, int pathIndex) : base(tag, path, address)
        {
            this.PathIndex = pathIndex;
        }
    }

    public class EthTransaction : BaseTransaction
    {
        public EthTransaction(string id, string from, string to, WalletDirection direction, BigInteger amount, BigInteger fee, long confirmations) :
            base(id, from, to, direction, amount, fee, confirmations)
        {}
    }

    public class EthWallet : BaseWallet
    {
        public const string TYPE = "ETH";
        struct WalletData
        {
            public string Type;
            public Accts Accounts;
            public AcctTxs Txs;
            public int LastPathIndex;
        }

        const string PATH = "m/44'/60'/0'/x";
        const int TX_GAS = 21000;

        Web3 web3 = null;
        string gethTxScanAddress = null;
        WebClient scanClient = null;
        Wallet wallet = null;

        WalletData wd = new WalletData{Accounts = new Accts(), Txs = new AcctTxs(), LastPathIndex = 0};
        bool mainNet;
        ILogger logger;

        public EthWallet(ILogger logger, string seedHex, string filename, bool mainNet, string gethAddress, string gethTxScanAddress)
        {
            this.logger = logger;

            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));
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

        public override void Save(string filename)
        {
            wd.Type = TYPE;
            // save data
            if (!string.IsNullOrWhiteSpace(filename))  
                File.WriteAllText(filename, JsonConvert.SerializeObject(wd, Formatting.Indented));
        }

        public override bool IsMainnet()
        {
            return mainNet;
        }

        public override IEnumerable<string> GetTags()
        {
            return wd.Accounts.Keys;
        }

        public override IAddress NewAddress(string tag)
        {
            var pathIndex = wd.LastPathIndex + 1;
            // create new address that is unused
            var acct = wallet.GetAccount(pathIndex);
            var address = new EthAccount(tag, PATH.Replace("x", pathIndex.ToString()), acct.Address, pathIndex);
            // regsiter address with gethtxscan
            scanClient.DownloadString(gethTxScanAddress + "/watch_account/" + acct.Address);
            // add to address list
            if (wd.Accounts.ContainsKey(tag))
                wd.Accounts[tag].Add(address);
            else
            {
                var list = new List<EthAccount>();
                list.Add(address);
                wd.Accounts[tag] = list;
            }
            // update last path index and return address
            wd.LastPathIndex = pathIndex;
            return address;
        }

        public override IEnumerable<IAddress> GetAddresses(string tag)
        {
            if (wd.Accounts.ContainsKey(tag))
                return wd.Accounts[tag];
            return new List<IAddress>();
        }

        struct scantx
        {
            public string txid;
            public string from_;
            public string to;
            public string value;
            public long block_num;
            public scantx(string txid, string from_, string to, string value, long blockNum)
            {
                this.txid = txid;
                this.from_ = from_;
                this.to = to;
                this.value = value;
                this.block_num = blockNum;
            }
        }

        void UpdateTxs(string address)
        {
            var blockNumTask = web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            blockNumTask.Wait();
            var blockNum = (long)blockNumTask.Result.Value;
            var json = scanClient.DownloadString(gethTxScanAddress + "/list_transactions/" + address);
            var scantxs = JsonConvert.DeserializeObject<List<scantx>>(json);
            foreach (var scantx in scantxs)
            {
                long confirmations = 0;
                if (scantx.block_num > 0)
                    confirmations = blockNum - scantx.block_num;
                var tx = new EthTransaction(scantx.txid, scantx.from_, address, WalletDirection.Incomming,
                    BigInteger.Parse(scantx.value), -1, confirmations);
                List<EthTransaction> txs = null;
                if (wd.Txs.ContainsKey(tx.To))
                    txs = wd.Txs[tx.To];
                else
                    txs = new List<EthTransaction>();
                bool replacedTx = false;
                for (var i = 0; i < txs.Count; i++)
                {
                    if (txs[i].Id == tx.Id)
                    {
                        txs[i] = tx;
                        replacedTx = true;
                        break;
                    }
                }
                if (!replacedTx)
                    txs.Add(tx);
                wd.Txs[tx.To] = txs;
            }
        }

        void AddTxs(List<ITransaction> txs, string address)
        {
            if (wd.Txs.ContainsKey(address))
                foreach (var tx in wd.Txs[address])
                    txs.Add(tx);
        }

        public override IEnumerable<ITransaction> GetTransactions(string tag)
        {
            var txs = new List<ITransaction>(); 
            if (wd.Accounts.ContainsKey(tag))
                foreach (var item in wd.Accounts[tag])
                {
                    UpdateTxs(item.Address);
                    AddTxs(txs, item.Address);
                }
            return txs;
        }

        public override IEnumerable<ITransaction> GetAddrTransactions(string address)
        {
            UpdateTxs(address);
            if (wd.Txs.ContainsKey(address))
                return wd.Txs[address];
            return new List<ITransaction>(); 
        }

        public override BigInteger GetBalance(string tag)
        {
            if (wd.Accounts.ContainsKey(tag))
            {
                BigInteger total = 0;
                foreach (var item in wd.Accounts[tag])
                    total += GetAddrBalance(item.Address);
                return total;
            }
            return 0;
        }

        public override BigInteger GetAddrBalance(string address)
        {
            UpdateTxs(address);
            if (wd.Txs.ContainsKey(address))
            {
                BigInteger total = 0;
                foreach (var tx in wd.Txs[address])
                    if (tx.Direction == WalletDirection.Incomming)
                        total += tx.Amount;
                    else if (tx.Direction == WalletDirection.Outgoing)
                    {
                        total -= tx.Amount;
                        total -= tx.Fee;
                    }
                return total;
            }
            return 0;
        }

        WalletError CreateSpendTxs(IEnumerable<IAddress> candidates, string to, BigInteger amount, BigInteger gasPrice, BigInteger gasLimit, BigInteger feeMax,
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
            logger.Debug("feeMax {0}, feeTotal {1}", feeMax, feeTotal);
            logger.Debug("amountRemaining {0}", amountRemaining);
                return WalletError.MaxFeeBreached;
            if (amountRemaining != 0)
                return WalletError.InsufficientFunds;
            return WalletError.Success;
        }

        void AddOutgoingTx(string from, string signedTx)
        {
            var tx = new Transaction(signedTx.HexToByteArray());
            if (!wd.Txs.ContainsKey(from))
                wd.Txs[from] = new List<EthTransaction>();
            var fee = HexBigIntegerConvertorExtensions.HexToBigInteger(tx.GasLimit.ToHex(), false) * HexBigIntegerConvertorExtensions.HexToBigInteger(tx.GasPrice.ToHex(), false);
            var amount = HexBigIntegerConvertorExtensions.HexToBigInteger(tx.Value.ToHex(), false);
            wd.Txs[from].Add(new EthTransaction(tx.Hash.ToHex(true), from, tx.ReceiveAddress.ToHex(true), WalletDirection.Outgoing,
                amount, fee, 0));
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
                        logger.Error(ex);
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
            var accts = new List<IAddress>();
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
                        logger.Error(ex);
                        return WalletError.PartialBroadcast;
                    }
                    // add to wallet data
                    AddOutgoingTx(tx.Item1, tx.Item2);
                }
            }
            return res;
        }

        public override IEnumerable<ITransaction> GetUnacknowledgedTransactions(string tag)
        {
            var txs = new List<EthTransaction>();
            if (wd.Accounts.ContainsKey(tag))
                foreach (var acct in wd.Accounts[tag])
                    if (wd.Txs.ContainsKey(acct.Address))
                        foreach (var tx in wd.Txs[acct.Address])
                            if (!tx.Acknowledged)
                                txs.Add(tx);
            return txs;
        }

        public override void AcknowledgeTransactions(string tag, IEnumerable<ITransaction> txs)
        {
            foreach (var tx in txs)
            {
                System.Diagnostics.Debug.Assert(tx is BaseTransaction);
                ((BaseTransaction)tx).Acknowledged = true;
            }
        }
    }
}
