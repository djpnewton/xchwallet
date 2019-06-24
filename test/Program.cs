using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
        {
            /*
            [Option("register_addrs", Default = false, HelpText = "Register addresses in NBXplorer (only effects BTC)")]
            public bool RegisterAddrs { get; set; }
            */
            [Option("scan_addrs", Default = false, HelpText = "Scan addresses in NBXplorer (only effects BTC)")]
            public bool ScanAddrs { get; set; }

            [Option("check_with_blockexplorer", Default = false, HelpText = "Double check balance with block explorer (only effects BTC)")]
            public bool CheckWithBlockExplorer { get; set; }

            [Option('u', "update", Default = false, HelpText = "Update from blockchain")]
            public bool Update { get; set; }
        }

        [Verb("balance", HelpText = "Show total balance of wallet tags")]
        class BalanceOptions : MinConfOptions
        {
            [Option('t', "tags", Required = true, HelpText = "The tags to get the total balance of")]
            public string Tags { get; set; }

            [Option('a', "allExcluding", Default = false, HelpText = "All excluding the tags specified")]
            public bool AllExcluding { get; set; }
        }

        [Verb("newaddress", HelpText = "Create a new wallet address")]
        class NewAddrOptions : CommonOptions
        { 
            [Option('t', "tag", Required = true, HelpText = "Wallet tag for new address")]
            public string Tag { get; set; }
        }

        class SpendOptionsBase : CommonOptions
        {
            [Option('t', "tag", Required = true, HelpText = "Wallet tag to spend from")]
            public string Tag { get; set; }

            [Option('T', "to", Required = true, HelpText = "Recipient address")]
            public string To { get; set; }

            [Option('a', "amount", Required = true, HelpText = "Amount in satoshis")]
            public ulong Amount { get; set; }
        }

        [Verb("pendingspend", HelpText = "Create pending spend")]
        class PendingSpendOptions : SpendOptionsBase
        {
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
        class SpendOptions : SpendOptionsBase
        { }

        [Verb("consolidate", HelpText = "Consolidate all funds from a range of tags")]
        class ConsolidateOptions : MinConfOptions
        { 
            [Option('t', "tags", Required = true, HelpText = "Wallet tag(s) to spend from")]
            public string Tags { get; set; }

            [Option('T', "tagTo", Required = true, HelpText = "Recipient tag")]
            public string TagTo { get; set; }

            [Option('a', "allExcluding", Default = false, HelpText = "All excluding the tags specified")]
            public bool AllExcluding { get; set; }
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

        [Verb("delete_tx", HelpText = "Delete wallet transaction")]
        class DeleteTxOptions : CommonOptions
        { 
            [Option('t', "txid", Required = true, HelpText = "TxId of transaction to delete, or 'ALL' to delete all transactions")]
            public string TxId { get; set; }
        }

        [Verb("load_test", HelpText = "Wallet load testing")]
        class LoadTestOptions : CommonOptions
        {
            [Option('p', "privateKey", Required = true, HelpText = "Private key to send from (hex)")]
            public string PrivateKey { get; set; }
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
            BigInteger totalBalance = 0;
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
                    {
                        if (tx.Direction == WalletDirection.Outgoing)
                        {
                            var inputs = tx.AmountInputs();
                            var to = tx.ChainTx.OutputsAddrs();
                            Console.WriteLine($"    {tx.ChainTx.TxId}, {tx.Direction}, inputs {inputs} (to {to}) {tx.ChainTx.Fee}");
                        }
                        else
                        {
                            var outputs = tx.AmountOutputs();
                            var from = tx.ChainTx.InputsAddrs();
                            Console.WriteLine($"    {tx.ChainTx.TxId}, {tx.Direction}, outputs {outputs} (from {from}) {tx.ChainTx.Fee}");
                        }
                    }
                var balance = wallet.GetBalance(tag.Tag, minConfs);
                Console.WriteLine($"  balance: {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
                totalBalance += balance;
            }
            Console.WriteLine($"\n::total balance: {totalBalance} ({wallet.AmountToString(totalBalance)} {wallet.Type()})");
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
                return new BtcWallet(GetLogger(), db, false, new Uri("http://10.50.1.100:24444"));
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
            var tag_ = wallet.GetTags().SingleOrDefault(t => t.Tag == tag);
            if (tag_ != null)
                return tag_;
            tag_ = wallet.NewTag(tag);
            wallet.Save();
            return tag_;
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
            /*
            if (opts.RegisterAddrs)
            {
                if (wallet is BtcWallet)
                {
                    (var deposit, var change) = ((BtcWallet)wallet).ReserveAddressesInNBXplorer();
                    Console.WriteLine($"New highest reserved deposit keypath {deposit.KeyPath} ({deposit.Address}");
                    Console.WriteLine($"New highest reserved change keypath {change.KeyPath} ({change.Address}");
                }
                else
                {
                    Console.WriteLine("Error: not BTC wallet");
                    return 1;
                }
            }
            */
            if (opts.ScanAddrs)
            {
                if (wallet is BtcWallet)
                {
                    var txs = wallet.GetChainTxs();
                    var txsScan = new List<TxScan>();
                    var count = 0;
                    foreach (var tx in txs)
                    {
                        var resp = Utils.BtcApsComRequest(wallet.IsMainnet(), $"/transaction/{tx.TxId}");
                        if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                            return 1;
                        }
                        dynamic res = JsonConvert.DeserializeObject(resp.Content);
                        txsScan.Add(new TxScan { TxId = tx.TxId, BlockHash = res.data.blockHash });
                        Console.WriteLine(count++);
                        System.Threading.Thread.Sleep(500);
                    }
                    ((BtcWallet)wallet).RescanNBXplorer(txsScan);
                }
                else
                {
                    Console.WriteLine("Error: not BTC wallet");
                    return 1;
                }
            }
            if (opts.CheckWithBlockExplorer)
            {
                if (wallet is BtcWallet)
                {
                    var count = 0;
                    BigInteger balance = 0;
                    foreach (var addr in wallet.GetAddresses())
                    {
                        var resp = Utils.BtcApsComRequest(wallet.IsMainnet(), $"/address/state/{addr.Address}");
                        if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                            return 1;
                        }
                        dynamic res = JsonConvert.DeserializeObject(resp.Content);
                        balance += (long)res.data.balance + (long)res.data.pendingReceivedAmount - (long)res.data.pendingSentAmount;
                        Console.WriteLine($"{count++} - {resp.ResponseUri}");
                        System.Threading.Thread.Sleep(500);
                    }
                    Console.WriteLine($"::block explorer balance: {balance}, ({wallet.AmountToString(balance)} {wallet.Type()})\n");
                }
                else
                {
                    Console.WriteLine("Error: not BTC wallet");
                    return 1;
                }
            }
            if (opts.Update)
            {
                var dbtx = wallet.BeginDbTransaction();
                wallet.UpdateFromBlockchain(dbtx);
                wallet.Save();
                dbtx.Commit();
            }
            PrintWallet(wallet, opts.MinimumConfirmations);
            return 0;
        }

        static int RunBalance(BalanceOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            IEnumerable<string> tagList = opts.Tags.Split(',')
                .Select(m => { return m.Trim(); })
                .ToArray();
            if (opts.AllExcluding)
                tagList = wallet.GetTags().Where(t => tagList.Any(t2 => t2 != t.Tag)).Select(t => t.Tag);
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
            var res = wallet.PendingSpendAction(opts.SpendCode, feeMax, feeUnit, out IEnumerable<WalletTx> wtxs);
            Console.WriteLine(res);
            foreach (var wtx in wtxs)
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
            EnsureTagExists(wallet, opts.Tag);
            var res = wallet.Spend(opts.Tag, opts.Tag, opts.To, opts.Amount, feeMax, feeUnit, out IEnumerable<WalletTx> wtxs);
            Console.WriteLine(res);
            foreach (var wtx in wtxs)
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
            IEnumerable<string> tagList = opts.Tags.Split(',')
                    .Select(m => { return m.Trim(); })
                    .ToList();
            if (opts.AllExcluding)
                tagList = wallet.GetTags().Where(t => tagList.Any(t2 => t2 != t.Tag)).Select(t => t.Tag);
            EnsureTagExists(wallet, opts.TagTo);
            var res = wallet.Consolidate(tagList, opts.TagTo, feeMax, feeUnit, out IEnumerable<WalletTx> wtxs, opts.MinimumConfirmations);
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
            wallet.AcknowledgeTransactions(txs);
            wallet.Save();
            return 0;
        }

        static int RunDeleteTx(DeleteTxOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;
            if (opts.TxId == "ALL")
                foreach (var tag in wallet.GetTags())
                    foreach (var tx in wallet.GetTransactions(tag.Tag))
                        wallet.DeleteTransaction(tx.ChainTx.TxId);
            else
                wallet.DeleteTransaction(opts.TxId);
            wallet.Save();
            return 0;
        }

        static int RunLoadTest(LoadTestOptions opts)
        {
            var wallet = OpenWallet(opts);
            if (wallet == null)
                return 1;

            // create tags/addresses
            Console.WriteLine("::create tags/addrs");
            var one = "1"; var two = "2"; var cons = "consolidate";
            var tags = new string[] { one, two, cons };
            foreach (var tag in tags)
            {
                Console.WriteLine($"  - {tag}");
                if (!wallet.HasTag(tag))
                {
                    wallet.NewTag(tag);
                    wallet.Save();
                }
                var addr = wallet.NewOrExistingAddress(tag);
                Console.WriteLine($"    {addr.Address}");
            }
            wallet.Save();
            Console.WriteLine();

            // send funds to wallet addresses
            BigInteger fee = 0;
            BigInteger feeUnit = 0;
            BigInteger feeMax = 0;
            List<string> sentTxids = null;
            string originalAddr = null;
            if (wallet is BtcWallet)
            {
                (var origAddr_, var withoutErrors, var sentTxIds_) = LoadTest.SendBtcFunds(wallet.IsMainnet(), opts.PrivateKey, wallet.GetAddresses(one).First().Address, wallet.GetAddresses(two).First().Address);
                originalAddr = origAddr_;
                sentTxids = sentTxIds_;
                fee = 113;
                feeUnit = 1;
                feeMax = 10000;
            }
            else if (wallet is ZapWallet)
            {
                throw new ApplicationException("TODO zap");
            }
            else if (wallet is WavWallet)
            {
                (var origAddr_, var withoutErrors, var sentTxIds_) = LoadTest.SendWavesFunds(wallet.IsMainnet(), opts.PrivateKey, wallet.GetAddresses(one).First().Address, wallet.GetAddresses(two).First().Address);
                originalAddr = origAddr_;
                sentTxids = sentTxIds_;
                fee = 100000;
                feeUnit = fee;
                feeMax = fee * 2;
            }
            else if (wallet is EthWallet)
            {
                throw new ApplicationException("TODO eth");
            }
            if (sentTxids.Count > 0)
                // sleep for a bit to wait for the tx to propagate
                foreach (var i in Enumerable.Range(1, 10))
                {
                    Console.Write(".");
                    System.Threading.Thread.Sleep(1000);
                }
            Console.WriteLine("\n");

            // consolidate
            Console.WriteLine("::consolidate");
            var dbtx = wallet.BeginDbTransaction();
            wallet.UpdateFromBlockchain(dbtx);
            wallet.Save();
            dbtx.Commit();
            var balance = wallet.GetBalance(new string[] { one, two });
            if (balance > 0)
            {
                var err = wallet.Consolidate(new string[] { one, two }, cons, feeMax, feeUnit, out var wtxs);
                foreach (var tx in wtxs)
                    Console.WriteLine($"  - {tx.ChainTx.TxId}");
                Console.WriteLine($"  {err}");
                wallet.Save();
                if (err != WalletError.Success)
                    return 1;
                // sleep for a bit to wait for the tx to propagate
                foreach (var i in Enumerable.Range(1, 10))
                {
                    Console.Write(".");
                    System.Threading.Thread.Sleep(1000);
                }
            }
            else
                Console.WriteLine("  no balance.. skipping");
            Console.WriteLine("\n");

            // send to original address
            Console.WriteLine("::send back to original address");
            dbtx = wallet.BeginDbTransaction();
            wallet.UpdateFromBlockchain(dbtx);
            wallet.Save();
            dbtx.Commit();
            balance = wallet.GetBalance(cons);
            if (balance > 0)
            {
                var err = wallet.Spend(cons, cons, originalAddr, balance - fee, feeMax, feeUnit, out var wtxs);
                foreach (var tx in wtxs)
                    Console.WriteLine($"  - {tx.ChainTx.TxId}");
                Console.WriteLine($"  {err}");
                wallet.Save();
                if (err != WalletError.Success)
                    return 1;
            }
            else
                Console.WriteLine("  no balance.. skipping");
            Console.WriteLine();

            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<NewOptions, ShowOptions, BalanceOptions, NewAddrOptions,
                PendingSpendOptions, ShowPendingOptions, ActionPendingOptions, CancelPendingOptions,
                SpendOptions, ConsolidateOptions, ShowUnAckOptions, AckOptions, DeleteTxOptions, LoadTestOptions>(args)
                .MapResult(
                (NewOptions opts) => RunNew(opts),
                (ShowOptions opts) => RunShow(opts),
                (BalanceOptions opts) => RunBalance(opts),
                (NewAddrOptions opts) => RunNewAddr(opts),
                (PendingSpendOptions opts) => RunPendingSpend(opts),
                (ShowPendingOptions opts) => RunShowPending(opts),
                (ActionPendingOptions opts) => RunActionPending(opts),
                (CancelPendingOptions opts) => RunCancelPending(opts),
                (SpendOptions opts) => RunSpend(opts),
                (ConsolidateOptions opts) => RunConsolidate(opts),
                (ShowUnAckOptions opts) => RunShowUnAck(opts),
                (AckOptions opts) => RunAck(opts),
                (DeleteTxOptions opts) => RunDeleteTx(opts),
                (LoadTestOptions opts) => RunLoadTest(opts),
                errs => 1);
        }
    }
}
