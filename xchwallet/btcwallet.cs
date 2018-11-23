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
        public BtcTransaction(string id, string from, string to, WalletDirection direction, BigInteger amount, BigInteger fee, long confirmations) :
            base(id, from, to, direction, amount, fee, confirmations)
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
            NBXplorerNetwork nbxnetwork;
            if (network == Network.Main)
                nbxnetwork = new NBXplorerNetworkProvider(NetworkType.Mainnet).GetFromCryptoCode("BTC");
            else if (network == Network.TestNet)
                nbxnetwork = new NBXplorerNetworkProvider(NetworkType.Testnet).GetFromCryptoCode("BTC");
            else 
                throw new Exception("unsupported network");
            client = new ExplorerClient(nbxnetwork, nbxplorerAddress);
            client.Track(pubkey);
            UpdateTxs();
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
            return client.Network.NBitcoinNetwork == Network.Main;
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
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
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
			return pubkey.Derive(path).PubKey.Hash.GetAddress(client.Network.NBitcoinNetwork);
        }

        private void processUtxo(NBXplorer.Models.UTXO utxo)
        {
            //var addr = AddressOf(pubkey.Root, utxo.KeyPath);
            var to = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            var id = utxo.Outpoint.Hash;
            var tx = new BtcTransaction(id.ToString(), "", to.ToString(), WalletDirection.Incomming, utxo.Value.Satoshi, -1, utxo.Confirmations);
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

        private void UpdateTxs()
        {
            var utxos = client.GetUTXOs(pubkey);
            foreach (var item in utxos.Unconfirmed.UTXOs)
                processUtxo(item);
            foreach (var item in utxos.Confirmed.UTXOs)
                processUtxo(item);
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
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            var address = new BtcAddress(tag, keypathInfo.KeyPath.ToString(), addr.ToString());
            // add to address list
            AddAddress(tag, address);
            return addr;
        }

        void AddOutgoingTx(string txid, string from, string to, BigInteger amount, BigInteger fee)
        {
            if (!wd.Txs.ContainsKey(from))
                wd.Txs[from] = new List<BtcTransaction>();
            Console.WriteLine("{0}, {1}", amount, fee);
            wd.Txs[from].Add(new BtcTransaction(txid, from, to, WalletDirection.Outgoing,
                amount, fee, 0));
        }

        FeeRate GetFeeRate(Transaction tx, List<Key> toBeSpentKeys, List<Coin> toBeSpent)
        {
            // sign tx before calculating fee so it includes signatures
            tx.Sign(toBeSpentKeys.ToArray(), toBeSpent.ToArray());
            return tx.GetFeeRate(toBeSpent.ToArray());
        }

        public IEnumerable<string> Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnitPerGasOrByte)
        {
            // create tx template with destination as first output
            var tx = Transaction.Create(client.Network.NBitcoinNetwork);
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to, client.Network.NBitcoinNetwork);
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on UTXOs from the selected tag
            var addrs = GetAddresses(tag);
            var candidates = new List<Tuple<Coin, string>>();
            var utxos = client.GetUTXOs(pubkey);
            foreach (var utxo in utxos.Confirmed.UTXOs)
            {
                var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork).ToString();
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
                tx.Inputs.Add(new TxIn(candidate.Item1.Outpoint));
                toBeSpent.Add(candidate.Item1);
                totalInput += candidate.Item1.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Item2)).PrivateKey;
                toBeSpentKeys.Add(privateKey);
                // check if we have enough inputs
                if (totalInput >= amount)
                    break;
                //TODO: take into account fees......
            }
            // check we have enough inputs
            if (totalInput < amount)
                return new List<string>(); //TODO: error codes?
            // check fee rate
            var feeRate = GetFeeRate(tx, toBeSpentKeys, toBeSpent);
            var currentSatsPerByte = feeRate.FeePerK / 1024;
            if (currentSatsPerByte > feeUnitPerGasOrByte)
            {
                // create a change address
                var changeAddress = AddChangeAddress(tagChange);
                // calculate the target fee
                var currentFee = feeRate.GetFee(tx.GetVirtualSize());
                var targetFee = tx.GetVirtualSize() * (long)feeUnitPerGasOrByte;
                var changeOutput = new TxOut(currentFee - targetFee, changeAddress);
                targetFee += output.GetSerializedSize() * (long)feeUnitPerGasOrByte;
                // add the change output
                changeOutput = tx.Outputs.Add(currentFee - targetFee, changeAddress);
            }
            // sign inputs (after adding a change output)
            tx.Sign(toBeSpentKeys.ToArray(), toBeSpent.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(toBeSpent.ToArray());
            if (fee.Satoshi > feeMax)
                return new List<string>(); //TODO: error codes?
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                AddOutgoingTx(tx.GetHash().ToString(), tag, to, amount, fee.Satoshi);
                return new List<string>() {tx.GetHash().ToString()};
            }
            else
                Console.WriteLine("ERROR: {0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
            return new List<string>(); 
        }

        public IEnumerable<string> Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnitPerGasOrByte)
        {
            // generate new address to send to
            var to = NewAddress(tagTo);
            // calc amount in tagFrom
            BigInteger amount = 0;
            foreach (var tag in tagFrom)
                amount += this.GetBalance(tag);
            // create tx template with destination as first output
            var tx = Transaction.Create(client.Network.NBitcoinNetwork);
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to.Address, client.Network.NBitcoinNetwork);
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on UTXOs from the selected tags
            var candidates = new List<Tuple<Coin, string>>();
            var utxos = client.GetUTXOs(pubkey);
            foreach (var tag in tagFrom)
            {
                var addrs = GetAddresses(tag);
                foreach (var utxo in utxos.Confirmed.UTXOs)
                {
                    var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork).ToString();
                    foreach (var addr in addrs)
                        if (addrStr == addr.Address)
                        {
                            candidates.Add(new Tuple<Coin, string>(utxo.AsCoin(), addr.Path));
                            break;
                        }
                }
            }
            // add all inputs so we can satisfy our output
            BigInteger totalInput = 0;
            var toBeSpent = new List<Coin>();
            var toBeSpentKeys = new List<Key>(); 
            foreach (var candidate in candidates)
            {
                // add to list of coins and private keys to spend
                tx.Inputs.Add(new TxIn(candidate.Item1.Outpoint));
                toBeSpent.Add(candidate.Item1);
                totalInput += candidate.Item1.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Item2)).PrivateKey;
                toBeSpentKeys.Add(privateKey);
            }
            // check we have enough inputs
            if (totalInput < amount)
                return new List<string>(); //TODO: error codes?
            // adjust fee rate by reducing the output incrementally
            var feeRate = new FeeRate(new Money(0));
            Money currentSatsPerByte = 0;
            while (currentSatsPerByte < feeUnitPerGasOrByte)
            {
                tx.Outputs[0].Value -= 1;

                feeRate = GetFeeRate(tx, toBeSpentKeys, toBeSpent);
                currentSatsPerByte = feeRate.FeePerK / 1024;
            }
            // sign inputs
            tx.Sign(toBeSpentKeys.ToArray(), toBeSpent.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(toBeSpent.ToArray());
            if (fee.Satoshi > feeMax)
                return new List<string>(); //TODO: error codes?
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                AddOutgoingTx(tx.GetHash().ToString(), tagTo, to.Address, amount, fee.Satoshi);
                return new List<string>() {tx.GetHash().ToString()};
            }
            else
                Console.WriteLine("ERROR: {0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
            return new List<string>(); 
        }

        public IEnumerable<ITransaction> GetUnacknowledgedTransactions(string tag)
        {
            var txs = new List<BtcTransaction>();
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
