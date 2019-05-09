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
        class CommonOptions
        {
            [Option("showsql", Default = false, HelpText = "Show SQL commands")]
            public bool ShowSql { get; set; }
            [Option('n', "dbname", Required = true, HelpText = "Wallet database name")]
            public string DbName { get; set; }
        }

        [Verb("new", HelpText = "New fiat wallet")]
        class NewOptions : CommonOptions
        { 
            [Option('t', "type", Required = true, HelpText = "Fiat type (eg. NZD, USD etc..)")]
            public string Type { get; set; }
        }

        [Verb("show", HelpText = "Show wallet details")]
        class ShowOptions : CommonOptions
        {}

        [Verb("newdeposit", HelpText = "Create a new pending deposit")]
        class NewDepositOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new deposit")]
            public string Tag { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount of deposit")]
            public long Amount { get; set; }
        }

        [Verb("updatedeposit", HelpText = "Update deposit - add bank tx")]
        class UpdateDepositOptions : CommonOptions
        {
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
        class NewWithdrawalOptions : CommonOptions
        {
            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new withdrawal")]
            public string Tag { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount of deposit")]
            public long Amount { get; set; }
        }

        [Verb("updatewithdrawal", HelpText = "Update withdrawal - add bank tx")]
        class UpdateWithdrawalOptions : CommonOptions
        {
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

        static string GetWalletType(string dbName)
        {
            using (var db = BaseContext.CreateSqliteWalletContext<FiatWalletContext>("localhost", dbName, false))
                return Util.GetWalletType(db);
        }

        static IFiatWallet CreateWallet(string dbName, string walletType, bool showSql)
        {
            GetLogger().LogDebug("Creating wallet ({0}) for testnet using db: '{1}'", walletType, dbName);

            // create db context and apply migrations
            var db = BaseContext.CreateMySqlWalletContext<FiatWalletContext>("localhost", dbName, showSql);
            db.Database.Migrate();
            // add dummy bank account
            var account = new BankAccount{ BankName="Example Bank Inc.", BankAddress="1 Banking Street\nBanktown\n3245\nBankcountry", AccountName="Wallets Inc.", AccountNumber="22-4444-7777777-22"};

            return new FiatWallet(GetLogger(), db, walletType, account);
        }

        static FiatWalletTag EnsureTagExists(IFiatWallet wallet, string tag)
        {
            var tags = wallet.GetTags();
            foreach (var tag_ in tags)
                if (tag_.Tag == tag)
                    return tag_;
            var tag__ = wallet.NewTag(tag);
            wallet.Save();
            return tag__;
        }

        static IFiatWallet OpenWallet(CommonOptions opts)
        {
            var walletType = GetWalletType(opts.DbName);
            var wallet = CreateWallet(opts.DbName, walletType, opts.ShowSql);
            if (wallet == null)
                Console.WriteLine("Unable to determine wallet type (%s)", walletType);
            return wallet;
        }

        static int RunNew(NewOptions opts)
        {
            var wallet = CreateWallet(opts.DbName, opts.Type, opts.ShowSql);
            wallet.Save();
            return 0;
        }

        static int RunShow(ShowOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            PrintWallet(wallet);
            wallet.Save();
            return 0;
        }

        static int RunNewDeposit(NewDepositOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            EnsureTagExists(wallet, opts.Tag);
            Console.WriteLine(wallet.RegisterPendingDeposit(opts.Tag, opts.Amount));
            wallet.Save();
            return 0;
        }

        static int RunUpdateDeposit(UpdateDepositOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            wallet.UpdateDeposit(opts.DepositCode, opts.Date, opts.Amount, opts.BankMetadata);
            wallet.Save();
            return 0;
        }

        static int RunNewWithdrawal(NewWithdrawalOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            EnsureTagExists(wallet, opts.Tag);
            var account = new BankAccount{ BankName="Example Bank Inc.", BankAddress="1 Banking Street\nBanktown\n3245\nBankcountry", AccountName="John Smith", AccountNumber="12-1234-1234567-12"};
            Console.WriteLine(wallet.RegisterPendingWithdrawal(opts.Tag, opts.Amount, account));
            wallet.Save();
            return 0;
        }

        static int RunUpdateWithdrawal(UpdateWithdrawalOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            wallet.UpdateWithdrawal(opts.DepositCode, opts.Date, opts.Amount, opts.BankMetadata);
            wallet.Save();
            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<NewOptions, ShowOptions,
                NewDepositOptions, UpdateDepositOptions, NewWithdrawalOptions, UpdateWithdrawalOptions>(args)
                .MapResult(
                (NewOptions opts) => RunNew(opts),
                (ShowOptions opts) => RunShow(opts),
                (NewDepositOptions opts) => RunNewDeposit(opts),
                (UpdateDepositOptions opts) => RunUpdateDeposit(opts),
                (NewWithdrawalOptions opts) => RunNewWithdrawal(opts),
                (UpdateWithdrawalOptions opts) => RunUpdateWithdrawal(opts),
                errs => 1);
        }
    }
}
