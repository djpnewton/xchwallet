using System;
using System.Collections.Generic;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;

namespace xchwallet
{
    public class BtcAddress : BaseAddress
    {
        public BtcAddress(int id, string tag, string address) : base(id, tag, address)
        {}
    }

    public class BtcTransaction : BaseTransaction
    {
    }

    public class BtcWallet : IWallet
    {
        readonly ExplorerClient client = null;
        readonly DirectDerivationStrategy pubKey = null;

        public BtcWallet(string seedHex, Network network, Uri nbxplorerAddress, bool useLegacyAddrs=false)
        {
            var key = new ExtKey(seedHex);
            var xpubkey = $"{key.Neuter().ToString(network)}";
            if (useLegacyAddrs)
                xpubkey = xpubkey + "-[legacy]";
            pubKey = (DirectDerivationStrategy)new DerivationStrategyFactory(network).Parse(xpubkey);

            client = new ExplorerClient(network, nbxplorerAddress);
            client.Track(pubKey);
        }

        public bool IsMainnet()
        {
            return client.Network == Network.Main;
        }

        public IAddress NewAddress(int id, string tag)
        {
            var keypathInfo = client.GetUnused(pubKey, DerivationFeature.Deposit, id);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network);
            return new BtcAddress(id, tag, addr.ToString());
        }

        public List<ITransaction> GetTransactions(string address)
        {
            throw new Exception("not yet impletmented");
        }
    }
}
