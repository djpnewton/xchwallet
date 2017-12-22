using System;
using NBitcoin;
using xchwallet;

namespace test
{
    class Program
    {
        static void Main(string[] args)
        {
            var wallet = new BtcWallet("12345678901234567890123456789012", Network.TestNet, new Uri("http://127.0.0.1:24444"));
            Console.WriteLine(wallet.NewAddress(0, "blah"));
            Console.WriteLine(wallet.NewAddress(1, "blah2"));
        }
    }
}
