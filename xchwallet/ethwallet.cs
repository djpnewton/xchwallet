using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Net;
using Nethereum.Web3;
using Nethereum.HdWallet;
using Newtonsoft.Json;

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
        public EthTransaction(string id, string from, string to, UInt64 amount, int confirmations) : base(id, from, to, amount, confirmations)
        {}
    }

    public class EthWallet : IWallet
    {
        struct WalletData
        {
            public Accts Accounts;
            public AcctTxs Txs;
            public int LastPathIndex;
        }

        const string PATH = "m/44'/60'/0'/x";

        Web3 web3 = null;
        string gethTxScanAddress = null;
        WebClient scanClient = null;
        Wallet wallet = null;

        WalletData wd = new WalletData{Accounts = new Accts(), Txs = new AcctTxs(), LastPathIndex = 0};
        bool mainNet;

        byte[] ParseHexString(string hex)
        {
            if ((hex.Length % 2) != 0)
                throw new ArgumentException("Invalid length: " + hex.Length);

            if (hex.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                hex = hex.Substring(2);

            int arrayLength = hex.Length / 2;
            byte[] byteArray = new byte[arrayLength];
            for (int i = 0; i < arrayLength; i++)
                byteArray[i] = byte.Parse(hex.Substring(i*2, 2), NumberStyles.HexNumber);

            return byteArray;
        }

        public EthWallet(string seedHex, string filename, bool mainNet, string gethAddress, string gethTxScanAddress)
        {
            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));
            // create HD wallet from seed
            wallet = new Wallet(ParseHexString(seedHex), PATH);

            // create web3 client
            web3 = new Web3(gethAddress);

            // create http client
            this.gethTxScanAddress = gethTxScanAddress;
            scanClient = new WebClient();

            this.mainNet = mainNet;
            /*TODO:!!!
            if (mainNet)
                if (!web3.version.network == 1)
                    throw new Exception("client is on wrong network");
            if (!mainNet)
                if (!web3.version.network == 3)
                    throw new Exception("client is on wrong network");
            */
        }

        public void Save(string filename)
        {
            // save data
            if (!string.IsNullOrWhiteSpace(filename))  
                File.WriteAllText(filename, JsonConvert.SerializeObject(wd, Formatting.Indented));
        }

        public bool IsMainnet()
        {
            return mainNet;
        }

        public bool ContainsAddress(string address)
        {
            foreach (var item in wd.Accounts)
            {
                foreach (var acct in item.Value)
                if (acct.Address==address)
                {
                        return true;
                }
            }
            return false;
        }

        public IAddress NewAddress(string tag)
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

        struct scantx
        {
            public string txid;
            public ulong value;
        }

        void UpdateTxs(string address)
        {
            var json = scanClient.DownloadString(gethTxScanAddress + "/list_transactions/" + address);
            var scantxs = JsonConvert.DeserializeObject<List<scantx>>(json);
            foreach (var scantx in scantxs)
            {
                var tx = new EthTransaction(scantx.txid, "", address, scantx.value, -1);
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

        public IEnumerable<ITransaction> GetTransactions(string address)
        {
            UpdateTxs(address);
            if (wd.Txs.ContainsKey(address))
                return wd.Txs[address];
            return new List<ITransaction>(); 
        }
    }
}
