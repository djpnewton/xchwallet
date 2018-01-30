using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using Newtonsoft.Json;

namespace xchwallet
{
    using Addrs = Dictionary<string, List<BtcAddress>>;
    using AddrTxs = Dictionary<string, List<BtcTransaction>>;

    public class BtcAddress : BaseAddress
    {
        public BtcAddress(string tag, string path, string address) : base(tag, path, address)
        {}
    }

    public class BtcTransaction : BaseTransaction
    {
        public BtcTransaction(string id, string from, string to, BigInteger amount, long confirmations) : base(id, from, to, amount, confirmations)
        {}
    }

    public class BtcWallet : IWallet
    {
        public const string TYPE = "BTC";
        struct WalletData
        {
            public string Type;
            public Addrs Addresses;
            public AddrTxs Txs;
        }

        readonly BitcoinExtKey key = null;
        readonly ExplorerClient client = null;
        readonly DirectDerivationStrategy pubkey = null;

        NBXplorer.Models.UTXOChanges utxoChanges = null;
        WalletData wd = new WalletData{Addresses = new Addrs(), Txs = new AddrTxs()};

        public BtcWallet(string seedHex, string filename, Network network, Uri nbxplorerAddress, bool useLegacyAddrs=false)
        {
            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));
            // create extended key
            key = new BitcoinExtKey(new ExtKey(seedHex), network);
            var strpubkey = $"{key.Neuter().ToString()}";
            if (useLegacyAddrs)
                strpubkey = strpubkey + "-[legacy]";
            pubkey = (DirectDerivationStrategy)new DerivationStrategyFactory(network).Parse(strpubkey);
            // create NBXplorer client
            client = new ExplorerClient(network, nbxplorerAddress);
            client.Track(pubkey);
            UpdateTxs(noWait: true);
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
            return client.Network == Network.Main;
        }

        public IEnumerable<string> GetTags()
        {
            return wd.Addresses.Keys;
        }

        void AddAddress(string tag, BtcAddress address)
        {
            // add to address list
            if (wd.Addresses.ContainsKey(tag))
                wd.Addresses[tag].Add(address);
            else
            {
                var list = new List<BtcAddress>();
                list.Add(address);
                wd.Addresses[tag] = list;
            }
        }

        public IAddress NewAddress(string tag)
        {
            // create new address that is unused
            var keypathInfo = client.GetUnused(pubkey, DerivationFeature.Deposit, reserve: true);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network);
            var address = new BtcAddress(tag, keypathInfo.KeyPath.ToString(), addr.ToString());
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

        private BitcoinAddress AddressOf(ExtPubKey pubkey, KeyPath path)
		{
			return pubkey.Derive(path).PubKey.Hash.GetAddress(client.Network);
        }

        private void processUtxo(NBXplorer.Models.UTXO utxo)
        {
            //var addr = AddressOf(pubkey.Root, utxo.KeyPath);
            var to = utxo.ScriptPubKey.GetDestinationAddress(client.Network);
            var id = utxo.Outpoint.Hash;
            var tx = new BtcTransaction(id.ToString(), "", to.ToString(), utxo.Value.Satoshi, utxo.Confirmations);
            List<BtcTransaction> txs = null;
            if (wd.Txs.ContainsKey(tx.To))
                txs = wd.Txs[tx.To];
            else
                txs = new List<BtcTransaction>();
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

        private void UpdateTxs(bool noWait = false)
        {
            utxoChanges = client.Sync(pubkey, utxoChanges, noWait);
            if (utxoChanges.HasChanges)
            {
                foreach (var item in utxoChanges.Unconfirmed.UTXOs)
                    processUtxo(item);
                foreach (var item in utxoChanges.Confirmed.UTXOs)
                    processUtxo(item);
            }
        }

        public void XXXPrintUtxos()
        {
            var utxo = client.Sync(pubkey, null, null);
            Console.WriteLine($"Has changes: {utxo.HasChanges},  Current height: {utxo.CurrentHeight}, Confirmed: {utxo.Confirmed}, unconfirmed utxo count: {utxo.Unconfirmed.UTXOs.Count}, confirmed utxo count: {utxo.Confirmed.UTXOs.Count}");
            foreach (var item in utxo.Confirmed.UTXOs)
            {
                var coin = item.AsCoin();
                Console.WriteLine($"  -C:\n      {AddressOf(pubkey.Root, item.KeyPath)}");
                Console.WriteLine($"      {coin.Amount} '{coin.ScriptPubKey}' {coin.Outpoint} {item.KeyPath}");
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
            UpdateTxs();
            var txs = new List<ITransaction>(); 
            if (wd.Addresses.ContainsKey(tag))
                foreach (var item in wd.Addresses[tag])
                    AddTxs(txs, item.Address);
            return txs;
        }

        public IEnumerable<ITransaction> GetAddrTransactions(string address)
        {
            UpdateTxs();
            if (wd.Txs.ContainsKey(address))
                return wd.Txs[address];
            return new List<ITransaction>(); 
        }

        public BigInteger GetBalance(string tag)
        {
            UpdateTxs();
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
            UpdateTxs();
            if (wd.Txs.ContainsKey(address))
            {
                BigInteger total = 0;
                foreach (var tx in wd.Txs[address])
                    total += tx.Amount;
                return total;
            }
            return 0;
        }

        public BitcoinAddress AddChangeAddress(string tag)
        {
            // create new address that is unused
            var keypathInfo = client.GetUnused(pubkey, DerivationFeature.Change, reserve: false);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network);
            var address = new BtcAddress(tag, keypathInfo.KeyPath.ToString(), addr.ToString());
            // add to address list
            AddAddress(tag, address);
            return addr;
        }

        public IEnumerable<string> Spend(string tag, string tagChange, string to, BigInteger amount)
        {
            // create tx template with destination as first output
            var tx = new Transaction();
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to, client.Network);
            var output = tx.AddOutput(money, toaddr);
            // create list of candidate coins to spend based on UTXOs from the selected tag
            var addrs = GetAddresses(tag);
            var candidates = new List<Tuple<Coin, string>>();
            var utxos = client.Sync(pubkey, null);
            foreach (var utxo in utxos.Confirmed.UTXOs)
            {
                var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network).ToString();
                foreach (var addr in addrs)
                    if (addrStr == addr.Address)
                    {
                        candidates.Add(new Tuple<Coin, string>(utxo.AsCoin(), addr.Path));
                        break;
                    }
            }
            // add inputs until we can satisfy our output
            BigInteger totalInput = 0;
            var toBeSpent = new List<Coin>();
            var toBeSpentKeys = new List<Key>(); 
            foreach (var candidate in candidates)
            {
                // add to list of coins and private keys to spend
                tx.AddInput(new TxIn(candidate.Item1.Outpoint));
                toBeSpent.Add(candidate.Item1);
                totalInput += candidate.Item1.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Item2)).PrivateKey;
                toBeSpentKeys.Add(privateKey);
                // check if we have enough inputs
                if (totalInput >= amount)
                    break;
            }
            // check fee rate
            var fee = tx.GetFee(toBeSpent.ToArray());
            var targetFee = new Money(0.0001m, MoneyUnit.BTC); //TODO: fix fixed fee size, allow custom (max total?) and satoshis per byte
            if (fee > targetFee)
            {
                // add a change output
                var changeAddress = AddChangeAddress(tagChange);
                tx.AddOutput(fee - targetFee, changeAddress);
            }
            // sign inputs (after adding a change output)
            tx.Sign(toBeSpentKeys.ToArray(), toBeSpent.ToArray());
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
                return new List<string>() {tx.GetHash().ToString()};
            else
                Console.WriteLine("ERROR: {0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
            return new List<string>(); 
        }
    }
}
