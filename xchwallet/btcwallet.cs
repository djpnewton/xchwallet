using System;
using System.IO;
using System.Collections.Generic;
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
        public BtcTransaction(string id, string from, string to, UInt64 amount, int confirmations) : base(id, from, to, amount, confirmations)
        {}
    }

    public class BtcWallet : IWallet
    {
        struct WalletData
        {
            public Addrs Addresses;
            public AddrTxs Txs;
        }

        readonly ExplorerClient client = null;
        readonly DirectDerivationStrategy pubkey = null;

        NBXplorer.Models.UTXOChanges utxo = null;
        WalletData wd = new WalletData{Addresses = new Addrs(), Txs = new AddrTxs()};

        public BtcWallet(string seedHex, string filename, Network network, Uri nbxplorerAddress, bool useLegacyAddrs=false)
        {
            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));
            // create extended public key
            var key = new BitcoinExtKey(new ExtKey(seedHex), network);
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
            // save data
            if (!string.IsNullOrWhiteSpace(filename))  
                File.WriteAllText(filename, JsonConvert.SerializeObject(wd, Formatting.Indented));
        }

        public bool IsMainnet()
        {
            return client.Network == Network.Main;
        }

        public bool ContainsAddress(string address)
        {
            foreach (var item in wd.Accounts)
            {
                foreach (var acct in item.Value)
                    if (acct.Address == address)
                        return true;
            }
            return false;
        }

        public IAddress NewAddress(string tag)
        {
            // create new address that is unused
            var keypathInfo = client.GetUnused(pubkey, DerivationFeature.Deposit, reserve: true);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network);
            var address = new BtcAddress(tag, keypathInfo.KeyPath.ToString(), addr.ToString());
            // add to address list
            if (wd.Addresses.ContainsKey(tag))
                wd.Addresses[tag].Add(address);
            else
            {
                var list = new List<BtcAddress>();
                list.Add(address);
                wd.Addresses[tag] = list;
            }
            return address;
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
            var tx = new BtcTransaction(id.ToString(), "", to.ToString(), (ulong)utxo.Value.Satoshi, utxo.Confirmations);
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
            utxo = client.Sync(pubkey, utxo, noWait);
            if (utxo.HasChanges)
            {
                foreach (var item in utxo.Unconfirmed.UTXOs)
                    processUtxo(item);
                foreach (var item in utxo.Confirmed.UTXOs)
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

        public IEnumerable<ITransaction> GetTransactions(string address)
        {
            UpdateTxs();
            if (wd.Txs.ContainsKey(address))
                return wd.Txs[address];
            return new List<ITransaction>(); 
        }
    }
}
