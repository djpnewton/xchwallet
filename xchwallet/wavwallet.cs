using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Newtonsoft.Json;
using WavesCS;
using NLog;

namespace xchwallet
{
    using Accts = Dictionary<string, List<WavAccount>>;
    using AddrTxs = Dictionary<string, List<WavTransaction>>;

    public class WavAccount : BaseAddress
    {
        public WavAccount(string tag, string path, string address) : base(tag, path, address)
        {}
    }

    public class WavTransaction : BaseTransaction
    {
        public WavTransaction(string id, string from, string to, WalletDirection direction, BigInteger amount, BigInteger fee, long confirmations) :
            base(id, from, to, direction, amount, fee, confirmations)
        {}
    }

    public class WavWallet : BaseWallet
    {
        public const string TYPE = "WAVES";
        struct WalletData
        {
            public string Type;
            public Accts Accounts;
            public AddrTxs Txs;
            public int Nonce;
        }

        WalletData wd = new WalletData{Accounts = new Accts(), Txs = new AddrTxs(), Nonce = 0};
        string seedHex;
        bool mainNet;
        Node node;
        ILogger logger;

        char ChainId()
        {
            if (mainNet)
                return Node.MainNetChainId;
            return Node.TestNetChainId;
        }

        PrivateKeyAccount CreateAccount(int nonce)
        {
            var seed = Utils.ParseHexString(seedHex);
            return PrivateKeyAccount.CreateFromSeed(seed, ChainId(), nonce);
        }

        public WavWallet(ILogger logger, string seedHex, string filename, bool mainNet, Uri nodeAddress)
        {
            this.logger = logger;

            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));

            this.seedHex = seedHex;
            this.mainNet = mainNet;
            this.node = new Node(nodeAddress.ToString(), ChainId());
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

        void AddAddress(string tag, WavAccount address)
        {
            // add to address list
            if (wd.Accounts.ContainsKey(tag))
                wd.Accounts[tag].Add(address);
            else
            {
                var list = new List<WavAccount>();
                list.Add(address);
                wd.Accounts[tag] = list;
            }
        }

        public override IAddress NewAddress(string tag)
        {
            var nonce = wd.Nonce;
            wd.Nonce++;
            var acct = CreateAccount(nonce);
            // create new address that is unused
            var address = new WavAccount(tag, nonce.ToString(), acct.Address);
            // add to address list
            AddAddress(tag, address);
            return address;
        }

        public override IEnumerable<IAddress> GetAddresses(string tag)
        {
            if (wd.Accounts.ContainsKey(tag))
                return wd.Accounts[tag];
            return new List<IAddress>();
        }

        void UpdateTxs(string address)
        {
            //TODO: handle more then 100 txs in address

            var limit = 100;
            var nodeTxs = node.GetTransactions(address, limit);
            foreach (var nodeTx in nodeTxs)
            {
                if (nodeTx is TransferTransaction)
                {
                    var trans = (TransferTransaction)nodeTx;
                    if (trans.Asset.Id == Assets.WAVES.Id)
                    {
                        WavTransaction tx = null;
                        var id = trans.GenerateId();
                        var amount = trans.Asset.AmountToLong(trans.Amount);
                        var fee = trans.FeeAsset.AmountToLong(trans.Fee);
                        var confs = 0; //TODO: find out confirmations
                        if (trans.Recipient == address)
                        {
                            // special case we are recipient and sender
                            if (trans.Sender == address)
                                tx = new WavTransaction(id, address, address,
                                    WalletDirection.Outgoing, 0, fee, confs);
                            else
                                tx = new WavTransaction(id, trans.Sender, address,
                                    WalletDirection.Incomming, amount, fee, confs);
                        }
                        else
                            tx = new WavTransaction(id, trans.Sender, trans.Recipient,
                                WalletDirection.Outgoing, amount, fee, confs);
                    
                        List<WavTransaction> txs = null;
                        if (wd.Txs.ContainsKey(tx.To))
                            txs = wd.Txs[tx.To];
                        else
                            txs = new List<WavTransaction>();
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

        bool CreateSpendTxs(IEnumerable<IAddress> candidates, string to, BigInteger amount, BigInteger fee, BigInteger feeMax,
            out List<Tuple<string, TransferTransaction>> signedSpendTxs, bool ignoreFees=false)
        {
            signedSpendTxs = new List<Tuple<string, TransferTransaction>>();
            var amountRemaining = amount;
            var feeTotal = new BigInteger(0);
            foreach (var acct in candidates)
            {
                if (amountRemaining == 0)
                    break;
                // get balance for the account
                var balance = GetAddrBalance(acct.Address);
                // if the balance is greater then the fee we can use this account
                if (balance > fee)
                {
                    // calcuate the actual amount we will use from this account
                    var amountThisAddress = balance - fee;
                    if (amountThisAddress > amountRemaining)
                        amountThisAddress = amountRemaining;
                    // create signed transaction
                    var account = CreateAccount(Int32.Parse(acct.Path));
                    var amountThisAddressDecimal = Assets.WAVES.LongToAmount((long)amountThisAddress);  //TODO: how can we use our biginteger
                    var feeDecimal = Assets.WAVES.LongToAmount((long)fee);                              //TODO: ditto
                    var tx = new TransferTransaction(account.PublicKey, to, Assets.WAVES, amountThisAddressDecimal, feeDecimal);
                    tx.Sign(account);
                    // update spend tx list and amount remaining
                    amountRemaining -= amountThisAddress;
                    if (ignoreFees)
                        amountRemaining -= fee;
                    signedSpendTxs.Add(new Tuple<string, TransferTransaction>(acct.Address, tx));
                }
                feeTotal += fee;
            }
            logger.Debug("feeMax {0}, feeTotal {1}", feeMax, feeTotal);
            logger.Debug("amountRemaining {0}", amountRemaining);
            if (feeTotal > feeMax)
                return false; //TODO: error code??
            return amountRemaining == 0; //TODO: error code??
        }

        void AddOutgoingTx(string from, TransferTransaction signedTx)
        {
            if (!wd.Txs.ContainsKey(from))
                wd.Txs[from] = new List<WavTransaction>();
            var fee = Assets.WAVES.AmountToLong(signedTx.Fee);
            var amount = Assets.WAVES.AmountToLong(signedTx.Amount);
            if (from == signedTx.Recipient) // special case if we send to ourselves
                amount = 0;
            wd.Txs[from].Add(new WavTransaction(signedTx.GenerateId(), from, signedTx.Recipient, WalletDirection.Outgoing,
                amount, fee, 0));
        }

        public override IEnumerable<string> Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit)
        {
            List<string> txids = new List<string>();
            // create spend transaction from accounts
            var accts = GetAddresses(tag);
            List<Tuple<string, TransferTransaction>> signedSpendTxs;
            if (CreateSpendTxs(accts, to, amount, feeUnit, feeMax, out signedSpendTxs))
            {
                // send each raw signed transaction and get the txid
                foreach (var tx in signedSpendTxs)
                {
                    var output = node.Broadcast(tx.Item2);
                    logger.Debug(output);
                    var txid = tx.Item2.GenerateId();
                    txids.Add(txid);
                    // add to wallet data
                    AddOutgoingTx(tx.Item1, tx.Item2);
                }
            }
            return txids;
        }

        public override IEnumerable<string> Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit)
        {
            List<string> txids = new List<string>();
            var to = NewOrUnusedAddress(tagTo);
            BigInteger balance = 0;
            var accts = new List<IAddress>();
            foreach (var tag in tagFrom)
            {
                balance += GetBalance(tag);
                var tagAccts = GetAddresses(tag);
                accts.AddRange(tagAccts);
            }
            List<Tuple<string, TransferTransaction>> signedSpendTxs;
            if (CreateSpendTxs(accts, to.Address, balance, feeUnit, feeMax, out signedSpendTxs, ignoreFees: true))
            {
                // send each raw signed transaction and get the txid
                foreach (var tx in signedSpendTxs)
                {
                    var output = node.Broadcast(tx.Item2);
                    logger.Debug(output);
                    var txid = tx.Item2.GenerateId();
                    txids.Add(txid);
                    // add to wallet data
                    AddOutgoingTx(tx.Item1, tx.Item2);
                }
            }
            return txids;
        }

        public override IEnumerable<ITransaction> GetUnacknowledgedTransactions(string tag)
        {
            var txs = new List<WavTransaction>();
            if (wd.Accounts.ContainsKey(tag))
                foreach (var addr in wd.Accounts[tag])
                    if (wd.Txs.ContainsKey(addr.Address))
                        foreach (var tx in wd.Txs[addr.Address])
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
