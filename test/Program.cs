using System;
using System.IO;
using System.Linq;
using NBitcoin;
using xchwallet;

namespace test
{
    class Program
    {
        static void PrintWallet(IWallet wallet, string tag)
        {
            Console.WriteLine($"{tag}:");
            var addrs = wallet.GetAddresses(tag);
            if (!addrs.Any())
                wallet.NewAddress(tag);
            Console.WriteLine("  addrs:");
            addrs = wallet.GetAddresses(tag);
            foreach (var addr in addrs)
                Console.WriteLine($"    {addr.Address}");
            Console.WriteLine("  txs:");
            var txs = wallet.GetTransactions(tag);
            foreach (var tx in txs)
                Console.WriteLine($"    {tx}");
            var balance = wallet.GetBalance(tag);
            Console.WriteLine($"  balance: {balance}");
        }

        static void Main(string[] args)
        {
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet_btc.json");
            var btcWallet = new BtcWallet("12345678901234567890123456789012", filename, Network.TestNet, new Uri("http://127.0.0.1:24444"), true);
            PrintWallet(btcWallet, "blah");
            btcWallet.Save(filename);

            filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet_eth.json");
            var ethWallet = new EthWallet("12345678901234567890123456789012", filename, false, "https://ropsten.infura.io", "http://localhost:5001");
            PrintWallet(ethWallet, "blah");
            ethWallet.Save(filename);
        }
    }
}
