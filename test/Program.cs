using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using xchwallet;

namespace test
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

        class MinConfOptions : CommonOptions
        {
            [Option('c', "confirmations", Default = 0, HelpText = "Minimum number of confirmations (default: 0)")]
            public int MinimumConfirmations { get; set; } 
        }

        [Verb("new", HelpText = "New wallet")]
        class NewOptions : CommonOptions
        { 
            [Option('t', "type", Required = true, HelpText = "Currency type (BTC, WAVES etc..)")]
            public string Type { get; set; }
        }

        [Verb("show", HelpText = "Show wallet details")]
        class ShowOptions : MinConfOptions
        { }

        [Verb("balance", HelpText = "Show total balance of wallet tags")]
        class BalanceOptions : MinConfOptions
        {
            [Option('t', "tags", Required = true, HelpText = "The tags to get the total balance of")]
            public string Tags { get; set; }  
        }

        [Verb("balance_exclude", HelpText = "Show total balance of wallet all tags excluding one specified")]
        class BalanceExcludeOptions : MinConfOptions
        {
            [Option('t', "tagexclude", Required = true, HelpText = "The tag to exclude")]
            public string TagExclude { get; set; } 
        }

        [Verb("newaddress", HelpText = "Create a new wallet address")]
        class NewAddrOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new address")]
            public string Tag { get; set; }
        }

        [Verb("pendingspend", HelpText = "Create pending spend")]
        class PendingSpendOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag to spend from")]
            public string Tag { get; set; }

            [Option('T', "to", Required = true, HelpText = "Recipient address")]
            public string To { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount in satoshis")]
            public ulong Amount { get; set; }
        }

        [Verb("showpending", HelpText = "Show all pending spends")]
        class ShowPendingOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag to spend from")]
            public string Tag { get; set; }
        }

        [Verb("actionpending", HelpText = "Action a pending spend")]
        class ActionPendingOptions : CommonOptions
        { 
            [Option('s', "spendcode", Required = true, HelpText = "Spend code to identify pending spend")]
            public string SpendCode { get; set; }
        }

        [Verb("cancelpending", HelpText = "Cancel a pending spend")]
        class CancelPendingOptions : CommonOptions
        { 
            [Option('s', "spendcode", Required = true, HelpText = "Spend code to identify pending spend")]
            public string SpendCode { get; set; }
        }

        [Verb("spend", HelpText = "Spend crypto - send funds from the wallet")]
        class SpendOptions : PendingSpendOptions
        { }

        [Verb("consolidate", HelpText = "Consolidate all funds from a range of tags")]
        class ConsolidateOptions : MinConfOptions
        { 
            [Option('t', "tags", Required = true, HelpText = "Wallet tag(s) to spend from")]
            public string Tags { get; set; }

            [Option('T', "tagTo", Required = true, HelpText = "Recipient tag")]
            public string TagTo { get; set; }
        }

        [Verb("consolidate_exclude", HelpText = "Consolidate funds from all tags except the one specified")]
        class ConsolidateExcludeOptions : MinConfOptions
        { 
            [Option('t', "tagExclude", Required = true, HelpText = "Wallet tag to exclude")]
            public string TagExclude { get; set; }

            [Option('T', "tagTo", Required = true, HelpText = "Recipient tag")]
            public string TagTo { get; set; }
        }

        [Verb("showunack", HelpText = "Show unacknowledged transactions")]
        class ShowUnAckOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag")]
            public string Tag { get; set; }
        }

        [Verb("ack", HelpText = "Acknowledge transactions")]
        class AckOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag")]
            public string Tag { get; set; }
        }

        [Verb("delete_tx", HelpText = "Delete wallet transactions")]
        class DeleteTxOptions : CommonOptions
        { 
            [Option('t', "txid", Required = true, HelpText = "TxId of transaction to delete")]
            public string TxId { get; set; }
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

        static void PrintWallet(IWallet wallet, int minConfs)
        {
            wallet.UpdateFromBlockchain();

            foreach (var tag in wallet.GetTags())
            {
                Console.WriteLine($"{tag.Tag}:");
                var addrs = tag.Addrs;
                Console.WriteLine("  addrs:");
                addrs = tag.Addrs;
                foreach (var addr in addrs)
                    Console.WriteLine($"    {addr.Address}");
                Console.WriteLine("  txs:");
                var txs = wallet.GetTransactions(tag.Tag);
                foreach (var tx in txs)
                    if (minConfs == 0 || tx.ChainTx.Confirmations >= minConfs)
                        Console.WriteLine($"    {tx}");
                var balance = wallet.GetBalance(tag.Tag, minConfs);
                Console.WriteLine($"  balance: {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
            }
        }

        static string GetWalletType(string dbName)
        {
            using (var db = BaseContext.CreateMySqlWalletContext<WalletContext>("localhost", dbName, false))
                return Util.GetWalletType(db);
        }

        static IWallet CreateWallet(string dbName, string walletType, bool showSql)
        {
            walletType = walletType.ToUpper();
            GetLogger().LogDebug("Creating wallet ({0}) for testnet using db: '{1}'", walletType, dbName);

            // create db context and apply migrations
            var db = BaseContext.CreateMySqlWalletContext<WalletContext>("localhost", dbName, showSql);
            db.Database.Migrate();

            if (walletType == BtcWallet.TYPE)
                return new BtcWallet(GetLogger(), db, false, new Uri("http://127.0.0.1:24444"), true);
            else if (walletType == EthWallet.TYPE)
                return new EthWallet(GetLogger(), db, false, "https://ropsten.infura.io", "http://localhost:5001");
            else if (walletType == WavWallet.TYPE)
                return new WavWallet(GetLogger(), db, false, new Uri("https://testnodes.wavesnodes.com"));
            else if (walletType == ZapWallet.TYPE)
                return new ZapWallet(GetLogger(), db, false, new Uri("https://testnodes.wavesnodes.com"));
            else
                throw new Exception("Wallet type not recognised");
        }

        static WalletTag EnsureTagExists(IWallet wallet, string tag)
        {
            var tags = wallet.GetTags();
            foreach (var tag_ in tags)
                if (tag_.Tag == tag)
                    return tag_;
            var tag__ = wallet.NewTag(tag);
            wallet.Save();
            return tag__;
        }

        static IWallet OpenWallet(CommonOptions opts)
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
            PrintWallet(wallet, opts.MinimumConfirmations);
            wallet.Save();
            return 0;
        }

        static int RunBalance(BalanceOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var tagList = opts.Tags.Split(',')
                .Select(m => { return m.Trim(); })
                .ToList();
            var balance = wallet.GetBalance(tagList, opts.MinimumConfirmations);
            Console.WriteLine($"  balance: {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
            return 0;
        }

        static int RunBalanceExclude(BalanceExcludeOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var tagList = wallet.GetTags().Where(t => t.Tag != opts.TagExclude).Select(t => t.Tag);
            var balance = wallet.GetBalance(tagList, opts.MinimumConfirmations);
            Console.WriteLine($"  balance: {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
            return 0;
        }

        static int RunNewAddr(NewAddrOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            EnsureTagExists(wallet, opts.Tag);
            Console.WriteLine(wallet.NewAddress(opts.Tag));
            wallet.Save();
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
                if (wallet is ZapWallet)
                {
                    //TODO: get fees from network
                    feeUnit = 1; // fee per tx
                    feeMax = feeUnit * 10;
                }
                else
                {
                    feeUnit = 100000; // fee per tx
                    feeMax = feeUnit * 10;
                }
            }
            else
                throw new Exception("fees not set!!");
        }

        static int RunPendingSpend(PendingSpendOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            EnsureTagExists(wallet, opts.Tag);
            var spend = wallet.RegisterPendingSpend(opts.Tag, opts.Tag, opts.To, opts.Amount);
            Console.WriteLine(spend);
            wallet.Save();
            return 0;
        }

        static int RunShowPending(ShowPendingOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var spends = wallet.PendingSpendsGet(opts.Tag, new PendingSpendState[] { PendingSpendState.Pending, PendingSpendState.Error });
            foreach (var spend in spends)
                Console.WriteLine(spend);
            return 0;
        }

        static int RunActionPending(ActionPendingOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            WalletTx wtx;
            var res = wallet.PendingSpendAction(opts.SpendCode, feeMax, feeUnit, out wtx);
            Console.WriteLine(res);
            if (wtx != null)
                Console.WriteLine(wtx.ChainTx.TxId);
            wallet.Save();
            return 0;
        }

        static int RunCancelPending(CancelPendingOptions opts)
        {
             var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            wallet.PendingSpendCancel(opts.SpendCode);
            wallet.Save();
            return 0;
        }

        static int RunSpend(SpendOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            WalletTx wtx;
            EnsureTagExists(wallet, opts.Tag);
            var res = wallet.Spend(opts.Tag, opts.Tag, opts.To, opts.Amount, feeMax, feeUnit, out wtx);
            Console.WriteLine(res);
            if (wtx != null)
                Console.WriteLine(wtx.ChainTx.TxId);
            wallet.Save();
            return 0;
        }

        static int RunConsolidate(ConsolidateOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            var tagList = opts.Tags.Split(',')
                    .Select(m => { return m.Trim(); })
                    .ToList();
            IEnumerable<WalletTx> wtxs;
            EnsureTagExists(wallet, opts.TagTo);
            var res = wallet.Consolidate(tagList, opts.TagTo, feeMax, feeUnit, out wtxs, opts.MinimumConfirmations);
            Console.WriteLine(res);
            foreach (var wtx in wtxs)
                Console.WriteLine(wtx.ChainTx.TxId);
            wallet.Save();
            return 0;
        }

        static int RunConsolidateExclude(ConsolidateExcludeOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            var tagList = wallet.GetTags().Where(t => t.Tag != opts.TagExclude).Select(t => t.Tag);
            IEnumerable<WalletTx> wtxs;
            EnsureTagExists(wallet, opts.TagTo);
            var res = wallet.Consolidate(tagList, opts.TagTo, feeMax, feeUnit, out wtxs, opts.MinimumConfirmations);
            Console.WriteLine(res);
            foreach (var wtx in wtxs)
                Console.WriteLine(wtx.ChainTx.TxId);
            wallet.Save();
            return 0;
        }

        static int RunShowUnAck(ShowUnAckOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            foreach (var tx in wallet.GetUnacknowledgedTransactions(opts.Tag))
                Console.WriteLine(tx);
            return 0;
        }

        static int RunAck(AckOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            var txs = wallet.GetUnacknowledgedTransactions(opts.Tag);
            foreach (var tx in txs)
                Console.WriteLine(tx);
            wallet.AcknowledgeTransactions(opts.Tag, txs);
            wallet.Save();
            return 0;
        }

        static int RunDeleteTx(DeleteTxOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            wallet.DeleteTransaction(opts.TxId);
            wallet.Save();
            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<NewOptions, ShowOptions, BalanceOptions, BalanceExcludeOptions, NewAddrOptions,
                PendingSpendOptions, ShowPendingOptions, ActionPendingOptions, CancelPendingOptions,
                SpendOptions, ConsolidateOptions, ConsolidateExcludeOptions, ShowUnAckOptions, AckOptions, DeleteTxOptions>(args)
                .MapResult(
                (NewOptions opts) => RunNew(opts),
                (ShowOptions opts) => RunShow(opts),
                (BalanceOptions opts) => RunBalance(opts),
                (BalanceExcludeOptions opts) => RunBalanceExclude(opts),
                (NewAddrOptions opts) => RunNewAddr(opts),
                (PendingSpendOptions opts) => RunPendingSpend(opts),
                (ShowPendingOptions opts) => RunShowPending(opts),
                (ActionPendingOptions opts) => RunActionPending(opts),
                (CancelPendingOptions opts) => RunCancelPending(opts),
                (SpendOptions opts) => RunSpend(opts),
                (ConsolidateOptions opts) => RunConsolidate(opts),
                (ConsolidateExcludeOptions opts) => RunConsolidateExclude(opts),
                (ShowUnAckOptions opts) => RunShowUnAck(opts),
                (AckOptions opts) => RunAck(opts),
                (DeleteTxOptions opts) => RunDeleteTx(opts),
                errs => 1);
        }
    }
}
