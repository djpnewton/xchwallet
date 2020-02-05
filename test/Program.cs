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
            [Option("showsql", HelpText = "Show SQL commands (can use 'SHOWSQL' environment variable)")]
            public bool? ShowSql { get; set; }
            [Option("mainnet", HelpText = "Testnet or Mainnet (can use 'MAINNET' environment variable)")]
            public bool? Mainnet { get; set; }
            [Option("node", Default = null, HelpText = "Specify node to connect to (can use 'NODE' environment variable)")]
            public string Node { get; set; }
            [Option('n', "dbname", Default = null, HelpText = "Wallet database name (prefix with 'conn|' to use a full connection string if you need extra parameters like remote server, username etc) (can use 'DBNAME' environment variable)")]
            public string DbName { get; set; }
        }

        class MinConfOptions : CommonOptions
        {
            [Option('c', "confirmations", Default = 0, HelpText = "Minimum number of confirmations")]
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
            [Option("recreate_txs", Default = false, HelpText = "Recreate tx history (only effects BTC)")]
            public bool RecreateTxs { get; set; }
            [Option("check_with_blockexplorer", Default = false, HelpText = "Double check balance with block explorer (only effects BTC)")]
            public bool CheckWithBlockExplorer { get; set; }

            [Option("update_io_amounts", Default = false, HelpText = "Check and update input and output amounts (only effects Waves tokens)")]
            public bool UpdateIoAmounts { get; set; }

            [Option('u', "update", Default = false, HelpText = "Update from blockchain")]
            public bool Update { get; set; }

            [Option('m', "maxtxs", Default = 10, HelpText = "Max txs to show per tag")]
            public int MaxTxs { get; set; }

            [Option('t', "txid", HelpText = "Display detail about a transaction")]
            public string TxId { get; set; }
        }

        [Verb("balance", HelpText = "Show total balance of wallet tags")]
        class BalanceOptions : MinConfOptions
        {
            [Option('t', "tags", HelpText = "The tags to get the total balance of (if omitted all tags are selected)")]
            public string Tags { get; set; }
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

            [Option('f', "for", Required = false, HelpText = "Wallet tag to spend on behalf of")]
            public string For { get; set; }
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

            [Option('a', "allstates", Required = false, HelpText = "Show spending spends in all states (even completed)")]
            public bool AllStates { get; set; }
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
        {
            [Option("replace_txid", HelpText = "Double spend at least one input of this tx (only effects BTC)")]
            public string ReplaceTxId { get; set; }

            [Option("fee_unit", Default=0, HelpText = "Specify the feeUnit to use")]
            public long FeeUnit { get; set; }

            [Option("fee_max", Default = 0, HelpText = "Specify the feeMax to use")]
            public long FeeMax { get; set; }
        }

        [Verb("consolidate", HelpText = "Consolidate all funds from a range of tags")]
        class ConsolidateOptions : MinConfOptions
        { 
            [Option('t', "tags", HelpText = "Wallet tag(s) to spend from")]
            public string Tags { get; set; }

            [Option('T', "tagTo", Required = true, HelpText = "Recipient tag")]
            public string TagTo { get; set; }

            [Option('a', "all", Default = false, HelpText = "Spend from all tags (except the recipient tag)")]
            public bool All { get; set; }

            [Option("replace_txid", HelpText = "Double spend at least one input of this tx (only effects BTC)")]
            public string ReplaceTxId { get; set; }

            [Option("fee_unit", Default = 0, HelpText = "Specify the feeUnit to use")]
            public long FeeUnit { get; set; }

            [Option("fee_max", Default = 0, HelpText = "Specify the feeMax to use")]
            public long FeeMax { get; set; }
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

        [Verb("setnetworkstatus", HelpText = "Set the network status of a transaction")]
        class SetNetworkStatusOptions : CommonOptions
        {
            [Option('t', "txid", Required = true, HelpText = "TxId of transaction to set")]
            public string TxId { get; set; }
            [Option('s', "status", Required = true, HelpText = "Status to set")]
            public ChainTxStatus Status { get; set; }
        }

        [Verb("load_test", HelpText = "Wallet load testing")]
        class LoadTestOptions : CommonOptions
        {
            [Option('p', "privateKey", Required = true, HelpText = "Private key to send from (hex)")]
            public string PrivateKey { get; set; }

            [Option('c', "count", Default = 1, HelpText = "Number of times to repeat")]
            public int Count { get; set; }
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

        static void PrintWallet(IWallet wallet, int minConfs, int MaxTxs)
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
                var txs = wallet.GetTransactions(tag.Tag).OrderByDescending(tx => tx.ChainTx.Date).Take(MaxTxs);
                foreach (var tx in txs)
                    if (minConfs == 0 || tx.ChainTx.Confirmations >= minConfs)
                    {
                        if (tx.Direction == WalletDirection.Outgoing)
                        {
                            var inputs = tx.AmountInputs();
                            var to = tx.ChainTx.OutputsAddrs();
                            Console.WriteLine($"    {tx.ChainTx.TxId}, {tx.Direction}, inputs {inputs} (to {to}) {tx.ChainTx.Fee} {tx.ChainTx.Status()}");
                        }
                        else
                        {
                            var outputs = tx.AmountOutputs();
                            var from = tx.ChainTx.InputsAddrs();
                            Console.WriteLine($"    {tx.ChainTx.TxId}, {tx.Direction}, outputs {outputs} (from {from}) {tx.ChainTx.Fee} {tx.ChainTx.Status()}");
                        }
                    }
                var balance = wallet.GetBalance(tag.Tag, minConfs);
                Console.WriteLine($"  balance: {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
                totalBalance += balance;
            }
            Console.WriteLine($"\n::total balance: {totalBalance} ({wallet.AmountToString(totalBalance)} {wallet.Type()})");
            Console.WriteLine($"::address count: {wallet.GetAddresses().Count()}");
            Console.WriteLine($"::chaintx count: {wallet.GetChainTxs().Count()}");
        }

        static void PrintTx(IWallet wallet, string txId)
        {
            var ctx = wallet.GetChainTxs().Where(t => t.TxId == txId).FirstOrDefault();
            if (ctx != null)
            {
                Console.WriteLine($"{txId}:");
                Console.WriteLine($"  fee: {ctx.Fee}, status: {ctx.Status()}");
                Console.WriteLine("  inputs:");
                foreach (var input in ctx.TxInputs)
                    Console.WriteLine($"    {input.Addr}, {input.N}, {input.Amount}");
                Console.WriteLine($"    total: {ctx.AmountInputs()}");
                Console.WriteLine("  outputs:");
                foreach (var output in ctx.TxOutputs)
                    Console.WriteLine($"    {output.Addr}, {output.N}, {output.Amount}");
                Console.WriteLine($"    total: {ctx.AmountOutputs()}");

                Console.WriteLine($"  network status:");
                Console.WriteLine($"    status: {ctx.NetworkStatus.Status}");
                Console.WriteLine($"    date last broadcast: {ctx.NetworkStatus.DateLastBroadcast}");
                if (ctx.NetworkStatus.TxBin != null)
                {
                    var hex = BitConverter.ToString(ctx.NetworkStatus.TxBin).Replace("-", "");
                    Console.WriteLine($"    txbin: {hex}");
                }
            }
        }

        struct WalletConfig
        {
            public string Node;
            public string DbName;
            public bool Mainnet;
            public bool ShowSQL;
        }

        static bool HasEnvVar(string name)
        {
            var vars = Environment.GetEnvironmentVariables();
            return vars.Contains(name);
        }

        static string GetRequiredConfig(string name, string cmdValue)
        {
            if (cmdValue == null)
            {
                if (HasEnvVar(name))
                    return Environment.GetEnvironmentVariable(name);
                else
                    GetLogger().LogError($"ERROR: no {name} specified");
            }
            GetLogger().LogInformation($"Required config {name}={cmdValue}");
            return cmdValue;
        }

        static WalletConfig GetWalletConfig(string node, bool? mainnet, string dbName, bool? showSql)
        {
            node = GetRequiredConfig("NODE", node);
            dbName = GetRequiredConfig("DBNAME", dbName);
            var mainnet_ = false;
            if (mainnet.HasValue)
                mainnet_ = mainnet.Value;
            else if (HasEnvVar("MAINNET"))
                mainnet_ = bool.Parse(Environment.GetEnvironmentVariable("MAINNET"));
            var showSql_ = false;
            if (showSql.HasValue)
                showSql_ = showSql.Value;
            else if (HasEnvVar("SHOWSQL"))
                showSql_ = bool.Parse(Environment.GetEnvironmentVariable("SHOWSQL"));
            return new WalletConfig { Node = node, DbName = dbName, Mainnet = mainnet_, ShowSQL = showSql_ };
        }

        static WalletContext GetWalletContext(string dbNameOrConnString, bool logToConsole, bool lazyLoading)
        {
            dbNameOrConnString = GetRequiredConfig("DBNAME", dbNameOrConnString);
            if (dbNameOrConnString == null)
                return null;

            if (dbNameOrConnString != null && dbNameOrConnString.StartsWith("conn|"))
            {
                var connString = dbNameOrConnString.Substring(5);
                return BaseContext.CreateMySqlWalletContext<WalletContext>(connString, logToConsole, true, lazyLoading);
            }
            else
                return BaseContext.CreateMySqlWalletContext<WalletContext>("localhost", dbNameOrConnString, logToConsole, true, lazyLoading);
        }

        static IWallet2 CreateWalletFast(WalletConfig cfg, string walletType)
        {
            walletType = walletType.ToUpper();
            var db = GetWalletContext(cfg.DbName, false, false);
            return new FastWallet(GetLogger(), db, cfg.Mainnet, walletType);
        }

        static string GetWalletType(string dbName)
        {
            dbName = GetRequiredConfig("DBNAME", dbName);
            using (var db = GetWalletContext(dbName, false, true))
            {
                if (db == null)
                    return null;
                return Util.GetWalletType(db);
            }
        }

        static IWallet CreateWallet(WalletConfig cfg, string walletType)
        {
            walletType = walletType.ToUpper();
            var network = cfg.Mainnet ? "mainnet" : "testnet";
            GetLogger().LogDebug($"Creating wallet ({walletType}) for {network} using db: '{cfg.DbName}', node: '{cfg.Node}'");

            // create db context and apply migrations
            var db = GetWalletContext(cfg.DbName, cfg.ShowSQL, true);
            db.Database.Migrate();

            if (walletType == BtcWallet.TYPE)
                return new BtcWallet(GetLogger(), db, cfg.Mainnet, new Uri(cfg.Node));
            else if (walletType == EthWallet.TYPE)
                return new EthWallet(GetLogger(), db, cfg.Mainnet, cfg.Node, "http://localhost:5001" /*TODO: get this from cmdline/env*/);
            else if (walletType == WavWallet.TYPE)
                return new WavWallet(GetLogger(), db, cfg.Mainnet, new Uri(cfg.Node));
            else if (walletType == ZapWallet.TYPE)
                return new ZapWallet(GetLogger(), db, cfg.Mainnet, new Uri(cfg.Node));
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

        static IWallet OpenWallet(CommonOptions opts, out IWallet2 walletFast)
        {
            var cfg = GetWalletConfig(opts.Node, opts.Mainnet, opts.DbName, opts.ShowSql);
            var walletType = GetWalletType(opts.DbName);
            var wallet = CreateWallet(cfg, walletType);
            if (wallet == null)
                Console.WriteLine($"Unable to determine wallet type ({walletType})");
            walletFast = CreateWalletFast(cfg, walletType);
            return wallet;
        }

        static int RunNew(NewOptions opts)
        {
            var cfg = GetWalletConfig(opts.Node, opts.Mainnet, opts.DbName, opts.ShowSql);
            var wallet = CreateWallet(cfg, opts.Type);
            if (wallet == null)
                return 1;
            wallet.Save();
            return 0;
        }

        static int RunShow(ShowOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
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
                    if (!Utils.ScanBtcTxs((BtcWallet)wallet))
                        return 1;
                }
                else
                {
                    Console.WriteLine("Error: not BTC wallet");
                    return 1;
                }
            }
            if (opts.RecreateTxs)
            {
                var dbtx = Utils.RecreateBtcTxs((BtcWallet)wallet);
                if (dbtx == null)
                    return 1;
                dbtx.Commit();
            }
            if (opts.CheckWithBlockExplorer)
            {
                if (wallet is BtcWallet)
                {
                    if (!Utils.CheckWithBtcBlockExplorer((BtcWallet)wallet))
                        return 1;
                }
                else
                {
                    Console.WriteLine("Error: not BTC wallet");
                    return 1;
                }
            }
            if (opts.UpdateIoAmounts)
            {
                if (wallet is WavWallet)
                {
                    if (!Utils.UpdateIoAmounts((WavWallet)wallet))
                        return 1;
                }
                else
                {
                    Console.WriteLine("Error: not Waves (or waves based token) wallet");
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
            if (string.IsNullOrEmpty(opts.TxId))
                PrintWallet(wallet, opts.MinimumConfirmations, opts.MaxTxs);
            else
                PrintTx(wallet, opts.TxId);
            return 0;
        }

        static int RunBalance(BalanceOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;

            IEnumerable<string> tagList;
            if (opts.Tags != null)
                tagList = opts.Tags.Split(',');
            else
                tagList = wallet.GetTags().Select(t => t.Tag);
            var balance = wallet.GetBalance(tagList, opts.MinimumConfirmations);
            Console.WriteLine($"  balance       : {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
            balance = walletFast.GetBalance(tagList, opts.MinimumConfirmations);
            Console.WriteLine($"  balance (fast): {balance} ({wallet.AmountToString(balance)} {wallet.Type()})");
            return 0;
        }

        static int RunNewAddr(NewAddrOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
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
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            EnsureTagExists(wallet, opts.Tag);
            var tagFor = opts.For != null ? wallet.GetTag(opts.For) : null;
            var spend = wallet.RegisterPendingSpend(opts.Tag, opts.Tag, opts.To, opts.Amount, tagFor);
            Console.WriteLine(spend);
            wallet.Save();
            return 0;
        }

        static int RunShowPending(ShowPendingOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            var states = new PendingSpendState[] { PendingSpendState.Pending, PendingSpendState.Error };
            if (opts.AllStates)
                states = null;
            var spends = wallet.PendingSpendsGet(opts.Tag, states);
            foreach (var spend in spends)
                Console.WriteLine(spend);
            return 0;
        }

        static int RunActionPending(ActionPendingOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
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
             var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            wallet.PendingSpendCancel(opts.SpendCode);
            wallet.Save();
            return 0;
        }

        static int RunSpend(SpendOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            if (opts.FeeUnit > 0)
                feeUnit = opts.FeeUnit;
            if (opts.FeeMax > 0)
                feeMax = opts.FeeMax;
            EnsureTagExists(wallet, opts.Tag);
            var tagFor = opts.For != null ? wallet.GetTag(opts.For) : null;
            var res = wallet.Spend(opts.Tag, opts.Tag, opts.To, opts.Amount, feeMax, feeUnit, out IEnumerable<WalletTx> wtxs, tagFor, opts.ReplaceTxId);
            if (res == WalletError.Success && opts.ReplaceTxId != null)
                wallet.SetTransactionStatus(opts.ReplaceTxId, ChainTxStatus.DoubleSpent);
            Console.WriteLine(res);
            foreach (var wtx in wtxs)
                Console.WriteLine(wtx.ChainTx.TxId);
            wallet.Save();
            return 0;
        }

        static int RunConsolidate(ConsolidateOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            var feeUnit = new BigInteger(0);
            var feeMax = new BigInteger(0);
            setFees(wallet, ref feeUnit, ref feeMax);
            if (opts.FeeUnit > 0)
                feeUnit = opts.FeeUnit;
            if (opts.FeeMax > 0)
                feeMax = opts.FeeMax;
            IEnumerable<string> tagList = new List<string>();
            if (opts.Tags != null)
                tagList = opts.Tags.Split(',')
                    .Select(m => { return m.Trim(); })
                    .ToList();
            if (opts.All)
                tagList = wallet.GetTags().Where(t => t.Tag != opts.TagTo).Select(t => t.Tag);
            Console.WriteLine(tagList.Count());
            EnsureTagExists(wallet, opts.TagTo);
            foreach (var tag in tagList)
                Console.WriteLine(tag);
            if (opts.ReplaceTxId != null)
                wallet.SetTransactionStatus(opts.ReplaceTxId, ChainTxStatus.Ignored);
            var res = wallet.Consolidate(tagList, opts.TagTo, feeMax, feeUnit, out IEnumerable<WalletTx> wtxs, opts.MinimumConfirmations, opts.ReplaceTxId);
            if (res == WalletError.Success && opts.ReplaceTxId != null)
                wallet.SetTransactionStatus(opts.ReplaceTxId, ChainTxStatus.DoubleSpent);
            Console.WriteLine(res);
            foreach (var wtx in wtxs)
                Console.WriteLine(wtx.ChainTx.TxId);
            wallet.Save();
            return 0;
        }

        static int RunShowUnAck(ShowUnAckOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            foreach (var tx in wallet.GetUnacknowledgedTransactions(opts.Tag))
                Console.WriteLine(tx);
            return 0;
        }

        static int RunAck(AckOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
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
            var wallet = OpenWallet(opts, out var walletFast);
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

        static int RunSetNetworkStatus(SetNetworkStatusOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
            if (wallet == null)
                return 1;
            wallet.SetTransactionStatus(opts.TxId, opts.Status);
            wallet.Save();
            return 0;
        }

        static int RunLoadTest(LoadTestOptions opts)
        {
            var wallet = OpenWallet(opts, out var walletFast);
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

            for (var n = 0; n < opts.Count; n++)
            {
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
                    (var origAddr_, var withoutErrors, var sentTxIds_) = LoadTest.SendZapFunds(wallet.IsMainnet(), opts.PrivateKey, wallet.GetAddresses(one).First().Address, wallet.GetAddresses(two).First().Address);
                    originalAddr = origAddr_;
                    sentTxids = sentTxIds_;
                    fee = 1;
                    feeUnit = fee;
                    feeMax = fee * 2;
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
            }

            return 0;
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<NewOptions, ShowOptions, BalanceOptions, NewAddrOptions,
                PendingSpendOptions, ShowPendingOptions, ActionPendingOptions, CancelPendingOptions,
                SpendOptions, ConsolidateOptions, ShowUnAckOptions, AckOptions, DeleteTxOptions, SetNetworkStatusOptions, LoadTestOptions>(args)
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
                (SetNetworkStatusOptions opts) => RunSetNetworkStatus(opts),
                (LoadTestOptions opts) => RunLoadTest(opts),
                errs => 1);
        }
    }
}
