using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NBitcoin;
using CommandLine;
using CommandLine.Text;
using xchwallet;

namespace test
{
    class Program
    {

        [Verb("show", HelpText = "Show wallet details")]
        class ShowOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }
        }

        [Verb("newaddress", HelpText = "Create a new wallet address")]
        class NewAddrOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new address")]
            public string Tag { get; set; }
        }

        [Verb("spend", HelpText = "Spend crypto - send funds from the wallet")]
        class SpendOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tag", Required = true, HelpText = "Wallet tag to spend from")]
            public string Tag { get; set; }

            [Option('T', "to", Required = true, HelpText = "Recipient address")]
            public string To { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount in satoshis")]
            public ulong Amount { get; set; }
        }

        static void PrintWallet(IWallet wallet)
        {
            var tags = wallet.GetTags();
            foreach (var tag in tags)
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
        }

        static IWallet CreateWallet(string filename, string walletType)
        {
            const string seed = "12345678901234567890123456789012";
            if (walletType == BtcWallet.TYPE)
                return new BtcWallet(seed, filename, Network.TestNet, new Uri("http://127.0.0.1:24444"), true);
            else if (walletType == EthWallet.TYPE)
                return new EthWallet(seed, filename, false, "https://ropsten.infura.io", "http://localhost:5001");
            return null;
        }

        static int RunShowAndReturnExitCode(ShowOptions opts)
        {
            var walletType = Util.GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            PrintWallet(wallet);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int RunNewAddrAndReturnExitCode(NewAddrOptions opts)
        {
            var walletType = Util.GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            Console.WriteLine(wallet.NewAddress(opts.Tag));
            wallet.Save(opts.Filename);
            return 0;
        }

        static int RunSpendAndReturnExitCode(SpendOptions opts)
        {
            var walletType = Util.GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            var feeUnitPerGasOrByte = new BigInteger(0);
            var feeMax = new BigInteger(0);
            if (wallet is BtcWallet)
            {
                feeUnitPerGasOrByte = 20; // sats per byte
                feeMax = 10000;
            }
            if (wallet is EthWallet)
            {
                feeUnitPerGasOrByte = 20000000000; // gas price (wei) 
                feeMax = 21000 * feeUnitPerGasOrByte * 10;
            }
            var txids = wallet.Spend(opts.Tag, opts.Tag, opts.To, opts.Amount, feeMax, feeUnitPerGasOrByte);
            foreach (var txid in txids)
                Console.WriteLine(txid);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<ShowOptions, NewAddrOptions, SpendOptions>(args)
                .MapResult(
                (ShowOptions opts) => RunShowAndReturnExitCode(opts),
                (NewAddrOptions opts) => RunNewAddrAndReturnExitCode(opts),
                (SpendOptions opts) => RunSpendAndReturnExitCode(opts),
                errs => 1);
        }
    }
}
