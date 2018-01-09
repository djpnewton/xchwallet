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
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "wallet.json");
            var wallet = new BtcWallet("12345678901234567890123456789012", filename, Network.TestNet, new Uri("http://127.0.0.1:24444"), true);
            Console.WriteLine(wallet.NewAddress("blah"));
            Console.WriteLine(wallet.NewAddress("blah2"));
            var txs = wallet.GetTransactions("mnXgjQSEs1oRiYsyzDuVd1CntdDiZR52Mt");
            foreach (var tx in txs)
                Console.WriteLine($"  {tx}");
            wallet.Save(filename);
        }
    }
}
