using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
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

        public EthWallet(string seedHex, string filename, bool mainNet, string gethAddress)
        {
            // load saved data
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                wd = JsonConvert.DeserializeObject<WalletData>(File.ReadAllText(filename));
            // create HD wallet from seed
            wallet = new Wallet(ParseHexString(seedHex), PATH);

            // create web3 client
            web3 = new Web3(gethAddress);

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

        public IAddress NewAddress(string tag)
        {
            // create new address that is unused
            wd.LastPathIndex++;
            var acct = wallet.GetAccount(wd.LastPathIndex);
            var address = new EthAccount(tag, PATH.Replace("x", wd.LastPathIndex.ToString()), acct.Address, wd.LastPathIndex);
            // add to address list
            if (wd.Accounts.ContainsKey(tag))
                wd.Accounts[tag].Add(address);
            else
            {
                var list = new List<EthAccount>();
                list.Add(address);
                wd.Accounts[tag] = list;
            }
            return address;
        }

        public IEnumerable<ITransaction> GetTransactions(string address)
        {
            //TODO:!! UpdateTxs();
            if (wd.Txs.ContainsKey(address))
                return wd.Txs[address];
            return new List<ITransaction>(); 
        }
    }
}
