using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Newtonsoft.Json;
using WavesCS;

namespace xchwallet
{
    using Addrs = Dictionary<string, List<WavAddress>>;
    using AddrTxs = Dictionary<string, List<WavTransaction>>;

    public class WavAddress : BaseAddress
    {
        public WavAddress(string tag, string path, string address) : base(tag, path, address)
        {}
    }

    public class WavTransaction : BaseTransaction
    {
        public WavTransaction(string id, string from, string to, WalletDirection direction, BigInteger amount, BigInteger fee, long confirmations) :
            base(id, from, to, direction, amount, fee, confirmations)
        {}
    }

    public class WavWallet : IWallet
    {
        public const string TYPE = "WAVES";
        struct WalletData
        {
            public string Type;
            public Addrs Addresses;
            public AddrTxs Txs;
            public int Nonce;
        }

        WalletData wd = new WalletData{Addresses = new Addrs(), Txs = new AddrTxs(), Nonce = 0};
        string seedHex;
        bool mainNet;
        Node node;

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

        public WavWallet(string seedHex, string filename, bool mainNet, Uri nodeAddress)
        {
            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));

            this.seedHex = seedHex;
            this.mainNet = mainNet;
            this.node = new Node(nodeAddress.ToString(), ChainId());
        }

        public void Save(string filename)
        {
            wd.Type = TYPE;
            // save data
            if (!string.IsNullOrWhiteSpace(filename))  
                File.WriteAllText(filename, JsonConvert.SerializeObject(wd, Formatting.Indented));
        }

        public bool IsMainnet()
        {
            return mainNet;
        }

        public IEnumerable<string> GetTags()
        {
            return wd.Addresses.Keys;
        }

        void AddAddress(string tag, WavAddress address)
        {
            // add to address list
            if (wd.Addresses.ContainsKey(tag))
                wd.Addresses[tag].Add(address);
            else
            {
                var list = new List<WavAddress>();
                list.Add(address);
                wd.Addresses[tag] = list;
            }
        }

        public IAddress NewAddress(string tag)
        {
            var nonce = wd.Nonce;
            wd.Nonce++;
            var acct = CreateAccount(nonce);
            // create new address that is unused
            var address = new WavAddress(tag, nonce.ToString(), acct.Address);
            // add to address list
            AddAddress(tag, address);
            return address;
        }

        public IEnumerable<IAddress> GetAddresses(string tag)
        {
            if (wd.Addresses.ContainsKey(tag))
                return wd.Addresses[tag];
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

        public IEnumerable<ITransaction> GetTransactions(string tag)
        {
            var txs = new List<ITransaction>(); 
            if (wd.Addresses.ContainsKey(tag))
                foreach (var item in wd.Addresses[tag])
                {
                    UpdateTxs(item.Address);
                    AddTxs(txs, item.Address);
                }
            return txs;
        }

        public IEnumerable<ITransaction> GetAddrTransactions(string address)
        {
            UpdateTxs(address);
            if (wd.Txs.ContainsKey(address))
                return wd.Txs[address];
            return new List<ITransaction>(); 
        }

        public BigInteger GetBalance(string tag)
        {
            if (wd.Addresses.ContainsKey(tag))
            {
                BigInteger total = 0;
                foreach (var item in wd.Addresses[tag])
                    if (wd.Txs.ContainsKey(item.Address))
                        foreach (var tx in wd.Txs[item.Address])
                            total += tx.Amount;
                return total;
            }
            return 0;
        }

        public BigInteger GetAddrBalance(string address)
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

        public IEnumerable<string> Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnitPerGasOrByte)
        {
            throw new Exception("Not yet implemented");
        }

        public IEnumerable<string> Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnitPerGasOrByte)
        {
            throw new Exception("Not yet implemented");
        }

        public IEnumerable<ITransaction> GetUnacknowledgedTransactions(string tag)
        {
            var txs = new List<WavTransaction>();
            if (wd.Addresses.ContainsKey(tag))
                foreach (var addr in wd.Addresses[tag])
                    if (wd.Txs.ContainsKey(addr.Address))
                        foreach (var tx in wd.Txs[addr.Address])
                            if (!tx.Acknowledged)
                                txs.Add(tx);
            return txs;
        }

        public void AcknowledgeTransactions(string tag, IEnumerable<ITransaction> txs)
        {
            foreach (var tx in txs)
            {
                System.Diagnostics.Debug.Assert(tx is BaseTransaction);
                ((BaseTransaction)tx).Acknowledged = true;
            }
        }
    }
}
