using System;
using System.IO;
using NBitcoin;
using xchwallet;

namespace test
{
    class Program
    {
        static void PrintAddress(IWallet wallet, string address)
        {
            Console.WriteLine("txs:");
            var txs = wallet.GetTransactions(address);
            foreach (var tx in txs)
                Console.WriteLine($"  {tx}");
            var balance = wallet.GetBalance(address);
            Console.WriteLine($"balance: {balance}");

        }

        static void Main(string[] args)
        {
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet_btc.json");
            var btcWallet = new BtcWallet("12345678901234567890123456789012", filename, Network.TestNet, new Uri("http://127.0.0.1:24444"), true);
            //Console.WriteLine(btcWallet.NewAddress("blah"));
            //Console.WriteLine(btcWallet.NewAddress("blah2"));
            PrintAddress(btcWallet, "mnXgjQSEs1oRiYsyzDuVd1CntdDiZR52Mt");
            btcWallet.Save(filename);

            filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet_eth.json");
            var ethWallet = new EthWallet("12345678901234567890123456789012", filename, false, "https://ropsten.infura.io", "http://localhost:5001");
            //Console.WriteLine(ethWallet.NewAddress("blah"));
            //Console.WriteLine(ethWallet.NewAddress("blah2"));
            PrintAddress(ethWallet, "0x64E43D4f6023b6c8a644D06Ff60765F9C0ceF2Ec");
            ethWallet.Save(filename);
        }
    }
}
