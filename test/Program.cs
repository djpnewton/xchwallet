using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using NBitcoin;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using xchwallet;

namespace test
{
    class Program
    {
        [Verb("newbtcwallet", HelpText = "New bitcoin wallet")]
        class NewBtcWalletOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }
        }

        [Verb("newethwallet", HelpText = "New ethereum wallet")]
        class NewEthWalletOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }
        }

        [Verb("newwavwallet", HelpText = "New waves wallet")]
        class NewWavWalletOptions
        {
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }
        }

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

        [Verb("consolidate", HelpText = "Consolidate all funds from a range of tags")]
        class ConsolidateOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tags", Required = true, HelpText = "Wallet tag(s) to spend from")]
            public string Tags { get; set; }

            [Option('T', "tagTo", Required = true, HelpText = "Recipient tag")]
            public string TagTo { get; set; }
        }

        [Verb("showunack", HelpText = "Show unacknowledged transactions")]
        class ShowUnAckOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tag", Required = true, HelpText = "Wallet tag")]
            public string Tag { get; set; }
        }

        [Verb("ack", HelpText = "Acknowledge transactions")]
        class AckOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tag", Required = true, HelpText = "Wallet tag")]
            public string Tag { get; set; }
        }

        static ILogger _logger = null;
        static ILogger GetLogger()
        {
            if (_logger == null)
            {
                var factory = new LoggerFactory().AddConsole(LogLevel.Debug).AddFile("test.log", minimumLevel: LogLevel.Debug);
                _logger = factory.CreateLogger("main");
            }
            return _logger;
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
                Console.WriteLine($"  balance: {balance} ({wallet.AmountToHumanFriendly(balance)} {wallet.Type()})");
            }
        }

        static IWallet CreateWallet(string filename, string walletType)
        {
            GetLogger().LogDebug("Creating wallet ({0}) for testnet using file: '{1}'", walletType, filename);

            const string seed = "12345678901234567890123456789012";
            if (walletType == BtcWallet.TYPE)
                return new BtcWallet(GetLogger(), seed, filename, Network.TestNet, new Uri("http://127.0.0.1:24444"), true);
            else if (walletType == EthWallet.TYPE)
                return new EthWallet(GetLogger(), seed, filename, false, "https://ropsten.infura.io", "http://localhost:5001");
            else if (walletType == WavWallet.TYPE)
                return new WavWallet(GetLogger(), seed, filename, false, new Uri("https://testnodes.wavesnodes.com"));
            return null;
        }

        static int RunNewBtcWalletAndReturnExitCode(NewBtcWalletOptions opts)
        {
            var wallet = CreateWallet(opts.Filename, BtcWallet.TYPE);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int RunNewEthWalletAndReturnExitCode(NewEthWalletOptions opts)
        {
            var wallet = CreateWallet(opts.Filename, EthWallet.TYPE);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int RunNewWavWalletAndReturnExitCode(NewWavWalletOptions opts)
        {
            var wallet = CreateWallet(opts.Filename, WavWallet.TYPE);
            wallet.Save(opts.Filename);
            return 0;
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

        static void setFees(IWallet wallet, ref BigInteger feeUnit, ref BigInteger feeMax)
        {
            if (wallet is BtcWallet)
            {
                feeUnit = 20; // sats per byte
                feeMax = 10000;
            }
            else if (wallet is EthWallet)
            {
                feeUnit = 20000000000; // gas price (wei)
                feeMax = 21000 * feeUnit * 10;
            }
            else if (wallet is WavWallet)
            {
                feeUnit = 100000; // fee per tx
                feeMax = feeUnit * 10;
            }
            else
                throw new Exception("fees not set!!");
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
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            IEnumerable<string> txids;
            var res = wallet.Spend(opts.Tag, opts.Tag, opts.To, opts.Amount, feeMax, feeUnit, out txids);
            Console.WriteLine(res);
            foreach (var txid in txids)
                Console.WriteLine(txid);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int RunConsolidateAndReturnExitCode(ConsolidateOptions opts)
        {
            var walletType = Util.GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            var tagList = opts.Tags.Split(',')
                    .Select(m => { return m.Trim(); })
                    .ToList();
            IEnumerable<string> txids;
            var res = wallet.Consolidate(tagList, opts.TagTo, feeMax, feeUnit, out txids);
            Console.WriteLine(res);
            foreach (var txid in txids)
                Console.WriteLine(txid);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int RunShowUnAckAndReturnExitCode(ShowUnAckOptions opts)
        {
            var walletType = Util.GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            foreach (var tx in wallet.GetUnacknowledgedTransactions(opts.Tag))
                Console.WriteLine(tx);
            return 0;
        }

        static int RunAckAndReturnExitCode(AckOptions opts)
        {
            var walletType = Util.GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            var txs = wallet.GetUnacknowledgedTransactions(opts.Tag);
            foreach (var tx in txs)
                Console.WriteLine(tx);
            wallet.AcknowledgeTransactions(opts.Tag, txs);
            wallet.Save(opts.Filename);
            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<NewBtcWalletOptions, NewEthWalletOptions, NewWavWalletOptions, ShowOptions, NewAddrOptions, SpendOptions, ConsolidateOptions, ShowUnAckOptions, AckOptions>(args)
                .MapResult(
                (NewBtcWalletOptions opts) => RunNewBtcWalletAndReturnExitCode(opts),
                (NewEthWalletOptions opts) => RunNewEthWalletAndReturnExitCode(opts),
                (NewWavWalletOptions opts) => RunNewWavWalletAndReturnExitCode(opts),
                (ShowOptions opts) => RunShowAndReturnExitCode(opts),
                (NewAddrOptions opts) => RunNewAddrAndReturnExitCode(opts),
                (SpendOptions opts) => RunSpendAndReturnExitCode(opts),
                (ConsolidateOptions opts) => RunConsolidateAndReturnExitCode(opts),
                (ShowUnAckOptions opts) => RunShowUnAckAndReturnExitCode(opts),
                (AckOptions opts) => RunAckAndReturnExitCode(opts),
                errs => 1);
        }
    }
}
