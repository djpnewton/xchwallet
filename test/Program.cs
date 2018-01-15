using System;
using System.IO;
using NBitcoin;
using xchwallet;

namespace test
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet_btc.json");
            var wallet = new BtcWallet("12345678901234567890123456789012", filename, Network.TestNet, new Uri("http://127.0.0.1:24444"), true);
            Console.WriteLine(wallet.NewAddress("blah"));
            Console.WriteLine(wallet.NewAddress("blah2"));
            var txs = wallet.GetTransactions("mnXgjQSEs1oRiYsyzDuVd1CntdDiZR52Mt");
            foreach (var tx in txs)
                Console.WriteLine($"  {tx}");
            wallet.Save(filename);
            */

            var filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet_eth.json");
            var wallet = new EthWallet("12345678901234567890123456789012", filename, false, "https://ropsten.infura.io", "http://localhost:5001");
            //Console.WriteLine(wallet.NewAddress("blah"));
            //Console.WriteLine(wallet.NewAddress("blah2"));
            var txs = wallet.GetTransactions("0x64E43D4f6023b6c8a644D06Ff60765F9C0ceF2Ec");
            foreach (var tx in txs)
                Console.WriteLine($"  {tx}");
            wallet.Save(filename);
        }
    }
}
