using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using NBitcoin;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using xchwallet;

namespace fiat
{
    class Program
    {
        [Verb("new", HelpText = "New fiat wallet")]
        class NewOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }
            [Option('t', "type", Required = true, HelpText = "Fiat type (eg. NZD, USD etc..)")]
            public string Type { get; set; }
        }

        [Verb("show", HelpText = "Show wallet details")]
        class ShowOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }
        }

        [Verb("newdeposit", HelpText = "Create a new pending deposit")]
        class NewDepositOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new deposit")]
            public string Tag { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount of deposit")]
            public long Amount { get; set; }
        }

        [Verb("updatedeposit", HelpText = "Update deposit - add bank tx")]
        class UpdateDepositOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('d', "depositCode", Required = true, HelpText = "Deposit code")]
            public string DepositCode { get; set; }

            [Option('D', "date", Required = true, HelpText = "Date")]
            public long Date { get; set; }

            [Option('b', "bankmetadata", Required = true, HelpText = "Bank metadata")]
            public string BankMetadata { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount in cents")]
            public long Amount { get; set; }
        }

        [Verb("newwithdrawal", HelpText = "Create a new pending withdrawal")]
        class NewWithdrawalOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new withdrawal")]
            public string Tag { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount of deposit")]
            public long Amount { get; set; }
        }

        [Verb("updatewithdrawal", HelpText = "Update withdrawal - add bank tx")]
        class UpdateWithdrawalOptions
        { 
            [Option('f', "filename", Required = true, HelpText = "Wallet filename")]
            public string Filename { get; set; }

            [Option('d', "depositCode", Required = true, HelpText = "Deposit code")]
            public string DepositCode { get; set; }

            [Option('D', "date", Required = true, HelpText = "Date")]
            public long Date { get; set; }

            [Option('b', "bankmetadata", Required = true, HelpText = "Bank metadata")]
            public string BankMetadata { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount in cents")]
            public long Amount { get; set; }
        }

        static ILogger _logger = null;
        static ILogger GetLogger()
        {
            if (_logger == null)
            {
                var factory = new LoggerFactory().AddConsole(LogLevel.Debug).AddFile("fiat.log", minimumLevel: LogLevel.Debug);
                _logger = factory.CreateLogger("main");
            }
            return _logger;
        }

        static void PrintWallet(IFiatWallet wallet)
        {
            foreach (var tag in wallet.GetTags())
            {
                Console.WriteLine($"{tag.Tag}:");
                Console.WriteLine("  txs:");
                foreach (var tx in tag.Txs)
                    Console.WriteLine($"    {tx}");
            }
        }

        static string GetWalletType(string filename)
        {
            using (var db = BaseContext.CreateSqliteWalletContext<FiatWalletContext>(filename))
                return Util.GetWalletType(db);
        }

        static IFiatWallet CreateWallet(string filename, string walletType)
        {
            GetLogger().LogDebug("Creating wallet ({0}) using file: '{1}'", walletType, filename);

            var db = BaseContext.CreateSqliteWalletContext<FiatWalletContext>(filename);
            var account = new BankAccount{ BankName="Example Bank Inc.", BankAddress="1 Banking Street\nBanktown\n3245\nBankcountry", AccountName="Wallets Inc.", AccountNumber="22-4444-7777777-22"};
            return new FiatWallet(GetLogger(), db, walletType, account);
        }

        static int RunNewAndReturnExitCode(NewOptions opts)
        {
            var wallet = CreateWallet(opts.Filename, opts.Type);
            wallet.Save();
            return 0;
        }

        static int RunShowAndReturnExitCode(ShowOptions opts)
        {
            var walletType = GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            PrintWallet(wallet);
            wallet.Save();
            return 0;
        }

        static int RunNewDepositAndReturnExitCode(NewDepositOptions opts)
        {
            var walletType = GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            Console.WriteLine(wallet.RegisterPendingDeposit(opts.Tag, opts.Amount));
            wallet.Save();
            return 0;
        }

        static int RunUpdateDepositAndReturnExitCode(UpdateDepositOptions opts)
        {
            var walletType = GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            wallet.UpdateDeposit(opts.DepositCode, opts.Date, opts.Amount, opts.BankMetadata);
            wallet.Save();
            return 0;
        }

        static int RunNewWithdrawalAndReturnExitCode(NewWithdrawalOptions opts)
        {
            var walletType = GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            Console.WriteLine(wallet.RegisterPendingWithdrawal(opts.Tag, opts.Amount));
            wallet.Save();
            return 0;
        }

        static int RunUpdateWithdrawalAndReturnExitCode(UpdateWithdrawalOptions opts)
        {
            var walletType = GetWalletType(opts.Filename);
            var wallet = CreateWallet(opts.Filename, walletType);
            if (wallet == null)
            {
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
                return 1;
            }
            wallet.UpdateWithdrawal(opts.DepositCode, opts.Date, opts.Amount, opts.BankMetadata);
            wallet.Save();
            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<NewOptions, ShowOptions,
                NewDepositOptions, UpdateDepositOptions, NewWithdrawalOptions, UpdateWithdrawalOptions>(args)
                .MapResult(
                (NewOptions opts) => RunNewAndReturnExitCode(opts),
                (ShowOptions opts) => RunShowAndReturnExitCode(opts),
                (NewDepositOptions opts) => RunNewDepositAndReturnExitCode(opts),
                (UpdateDepositOptions opts) => RunUpdateDepositAndReturnExitCode(opts),
                (NewWithdrawalOptions opts) => RunNewWithdrawalAndReturnExitCode(opts),
                (UpdateWithdrawalOptions opts) => RunUpdateWithdrawalAndReturnExitCode(opts),
                errs => 1);
        }
    }
}
