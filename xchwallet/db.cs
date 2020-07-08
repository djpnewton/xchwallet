using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

// for 'dotnet ef migrations add InitiialCreate --project <project path with dbcontext>'
// see https://docs.microsoft.com/en-us/ef/core/miscellaneous/cli/dotnet#target-project-and-startup-project

namespace xchwallet
{
    public class BaseContextFactory
    {
        protected void initOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            // choose db source
            var dbtype = Environment.GetEnvironmentVariable("DB_TYPE");
            var connection = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            if (string.IsNullOrEmpty(dbtype) || string.IsNullOrEmpty(connection))
                throw new System.ArgumentException($"empty environment variable DB_TYPE '{dbtype}' or CONNECTION_STRING '{connection}'");
            switch (dbtype.ToLower())
            {
                case "sqlite":
                    optionsBuilder.UseSqlite(connection);
                    break;
                case "mysql":
                    optionsBuilder.UseMySql(connection, b => b.CharSetBehavior(Pomelo.EntityFrameworkCore.MySql.Infrastructure.CharSetBehavior.NeverAppend));
                    break;
                default:
                    throw new System.ArgumentException($"unknown DB_TYPE '{dbtype}'");
            }
        }
    }
    public class WalletContextFactory : BaseContextFactory, IDesignTimeDbContextFactory<WalletContext>
    {
        public WalletContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<WalletContext>();
            initOptionsBuilder(optionsBuilder);
            return new WalletContext(optionsBuilder.Options, false, true);
        }
    }

    public class FiatWalletContextFactory : BaseContextFactory, IDesignTimeDbContextFactory<FiatWalletContext>
    {
        public FiatWalletContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FiatWalletContext>();
            initOptionsBuilder(optionsBuilder);
            return new FiatWalletContext(optionsBuilder.Options, false, true);
        }
    }

    public abstract class BaseContext : DbContext
    {
        public DbSet<WalletCfg> WalletCfgs { get; set; }

        public static T CreateSqliteWalletContext<T>(string filename, bool logToConsole)
            where T : DbContext
        {
            // SHIT
            throw new Exception("dont use! the migrations have db specific annotations");
            // SHIT

            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseSqlite($"Data Source={filename}");
            return (T)Activator.CreateInstance(typeof(T), new object[] { optionsBuilder.Options, logToConsole });
        }

        public static T CreateMySqlWalletContext<T>(string connString, bool logToConsole, bool sensitiveLogging, bool lazyLoading)
            where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseMySql(connString, b => b.CharSetBehavior(Pomelo.EntityFrameworkCore.MySql.Infrastructure.CharSetBehavior.NeverAppend));
            optionsBuilder.EnableSensitiveDataLogging(sensitiveLogging);
            return (T)Activator.CreateInstance(typeof(T), new object[] { optionsBuilder.Options, logToConsole, lazyLoading });
        }

        public static T CreateMySqlWalletContext<T>(string host, string dbName, string uid, string pwd, bool logToConsole, bool sensitiveLogging, bool lazyLoading)
            where T : DbContext
        {
            var connString = $"host={host};database={dbName};uid={uid};password={pwd};";
            return CreateMySqlWalletContext<T>(connString, logToConsole, sensitiveLogging, lazyLoading);
        }

        public static T CreateMySqlWalletContext<T>(string host, string dbName, bool logToConsole, bool sensitiveLogging, bool lazyLoading)
            where T : DbContext
        {
            var connString = $"host={host};database={dbName};";
            return CreateMySqlWalletContext<T>(connString, logToConsole, sensitiveLogging, lazyLoading);
        }

        readonly bool logToConsole;
        readonly bool lazyLoading;

        public bool LazyLoading { get { return lazyLoading; } }

        public BaseContext(DbContextOptions options, bool logToConsole, bool lazyLoading) : base(options)
        {
            this.logToConsole = logToConsole;
            this.lazyLoading = lazyLoading;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (logToConsole)
            {
                var factory = LoggerFactory.Create(builder => {
                    builder.AddConsole().AddFilter(level => level >= LogLevel.Debug);
                });
                optionsBuilder.UseLoggerFactory(factory);
            }
            if (lazyLoading)
                optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<WalletCfg>()
                .HasIndex(s => s.Key)
                .IsUnique();
        }

        public bool CfgExists(string key)
        {
            return CfgGet(key) != null;
        }

        public WalletCfg CfgGet(string key)
        {
            return WalletCfgs.SingleOrDefault(a => a.Key == key);
        }

        public int CfgGetInt(string key, int def)
        {
            var setting = CfgGet(key);
            if (setting == null)
                return def;
            return int.Parse(setting.Value);
        }

        public bool CfgGetBool(string key, bool def)
        {
            var setting = CfgGet(key);
            if (setting == null)
                return def;
            return bool.Parse(setting.Value);
        }

        public void CfgSet(string key, string value)
        {
            var setting = CfgGet(key);
            if (setting == null)
            {
                setting = new WalletCfg { Key = key, Value = value };
                WalletCfgs.Add(setting);
            }
            else
            {
                setting.Value = value;
                WalletCfgs.Update(setting);
            }
        }

        public void CfgSetInt(string key, int value)
        {
            CfgSet(key, value.ToString());
        }

        public void CfgSetBool(string key, bool value)
        {
            CfgSet(key, value.ToString());
        }
    }

    public class WalletContext : BaseContext
    {
        public DbSet<TxOutput> TxOutputs { get; set; }
        public DbSet<TxInput> TxInputs { get; set; }
        public DbSet<ChainTx> ChainTxs { get; set; }
        public DbSet<ChainAttachment> ChainAttachments { get; set; }
        public DbSet<ChainTxNetworkStatus> ChainTxNetworkStatus { get; set; }
        public DbSet<WalletTag> WalletTags { get; set; }
        public DbSet<WalletAddr> WalletAddrs { get; set; }
        public DbSet<WalletTx> WalletTxs { get; set; }
        public DbSet<WalletPendingSpend> WalletPendingSpends { get; set; }
        public DbSet<TxOutputForTag> TxOutputsForTag { get; set; }
        public DbSet<PendingSpendForTag> PendingSpendsForTag { get; set; }

        public int LastPathIndex
        {
            get { return CfgGetInt("LastPathIndex", 0); }
            set { CfgSetInt("LastPathIndex", value); }
        }

        public WalletContext(DbContextOptions<WalletContext> options, bool logToConsole, bool lazyLoading) : base(options, logToConsole, lazyLoading)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var bigIntConverter = new ValueConverter<BigInteger, string>(
                v => v.ToString(),
                v => BigInteger.Parse(v));

            builder.Entity<WalletTag>()
                .HasIndex(t => t.Tag)
                .IsUnique();

            builder.Entity<WalletAddr>()
                .HasIndex(a => a.Address)
                .IsUnique();

            builder.Entity<TxOutput>()
                .HasAlternateKey(t => new { t.TxId, t.N });
            builder.Entity<TxOutput>()
                .Property(t => t.Amount)
                .HasConversion(bigIntConverter);

            builder.Entity<TxInput>()
                .HasAlternateKey(t => new { t.TxId, t.N });
            builder.Entity<TxInput>()
                .Property(t => t.Amount)
                .HasConversion(bigIntConverter);

            builder.Entity<ChainTx>()
                .HasIndex(t => t.TxId)
                .IsUnique();
            builder.Entity<ChainTx>()
                .Property(t => t.Fee)
                .HasConversion(bigIntConverter);

            builder.Entity<WalletPendingSpend>()
                .HasIndex(s => s.SpendCode)
                .IsUnique();
            builder.Entity<WalletPendingSpend>()
                .Property(s => s.Amount)
                .HasConversion(bigIntConverter);

            builder.Entity<ChainAttachment>()
                .HasIndex(a => a.ChainTxId)
                .IsUnique();

            builder.Entity<ChainTxNetworkStatus>()
                .HasIndex(d => d.ChainTxId)
                .IsUnique();

            builder.Entity<TxOutputForTag>()
                .HasIndex(to => to.TxOutputId)
                .IsUnique();
            builder.Entity<TxOutputForTag>()
                .HasKey(to => new { to.TxOutputId, to.TagId });

            builder.Entity<PendingSpendForTag>()
                .HasIndex(ps => ps.PendingSpendId)
                .IsUnique();
            builder.Entity<PendingSpendForTag>()
                .HasKey(ps => new { ps.PendingSpendId, ps.TagId });
        }

        public WalletTag TagGet(string tag)
        {
            return WalletTags.SingleOrDefault(t => t.Tag == tag);
        }

        public WalletTag TagCreate(string tag)
        {
            var _tag = new WalletTag { Tag = tag };
            WalletTags.Add(_tag);
            return _tag;
        }

        public IEnumerable<WalletAddr> AddrsGet(string tag)
        {
            var _tag = TagGet(tag);
            if (_tag != null)
                return _tag.Addrs;
            return new List<WalletAddr>();
        }

        public IEnumerable<WalletTx> TxsGet(string tag)
        {
            var _tag = TagGet(tag);
            if (_tag != null)
            {
                var txs = new List<WalletTx>();
                foreach (var addr in _tag.Addrs)
                    txs.AddRange(addr.Txs);
                return txs;
            }
            return new List<WalletTx>();
        }

        public WalletTx TxGet(WalletAddr addr, ChainTx tx, WalletDirection dir)
        {
            return WalletTxs.SingleOrDefault(t => t.WalletAddrId == addr.Id && t.ChainTxId == tx.Id && t.Direction == dir);
        }

        public void TxDelete(string txid)
        {
            var ctx = ChainTxs.SingleOrDefault(t => t.TxId == txid);
            if (ctx != null)
                ChainTxs.Remove(ctx);
        }

        public IEnumerable<WalletTx> TxsUnAckedGet(string address)
        {
            var addr = AddrGet(address);
            if (addr != null)
                return WalletTxs.Where(t => t.WalletAddrId == addr.Id && t.State != WalletTxState.Ack);
            return new List<WalletTx>();
        }

        public WalletAddr AddrGet(string addr)
        {
            return WalletAddrs.SingleOrDefault(a => a.Address == addr);
        }

        public ChainTx ChainTxGet(string txid)
        {
            return ChainTxs.SingleOrDefault(t => t.TxId == txid);
        }

        public TxOutput TxOutputGet(string txid, uint n)
        {
            return TxOutputs.SingleOrDefault(o => o.TxId == txid && o.N == n);
        }

        public TxInput TxInputGet(string txid, uint n)
        {
            return TxInputs.SingleOrDefault(i => i.TxId == txid && i.N == n);
        }

        public WalletPendingSpend PendingSpendGet(string spendCode)
        {
            return WalletPendingSpends.SingleOrDefault(s => s.SpendCode == spendCode);
        }
    }

    public class WalletCfg
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return $"<{Key} {Value}>";
        }
    }

    public abstract class TxOutputBase
    {
        public int Id { get; set; }
        public int ChainTxId { get; set; }
        public int? WalletAddrId { get; set; }
        public virtual ChainTx ChainTx { get; set; }
        public virtual WalletAddr WalletAddr { get; set; }
        public string TxId { get; set; }
        public string Addr { get; set; }
        public uint N { get; set; }
        [Column(TypeName = "varchar(255)")]
        public BigInteger Amount { get; set; }

        public TxOutputBase()
        {
            this.TxId = null;
            this.Addr = null;
            this.N = 0;
            this.Amount = 0;
        }

        public TxOutputBase(string txid, string addr, uint n, BigInteger amount)
        {
            this.TxId = txid;
            this.Addr = addr;
            this.N = n;
            this.Amount = amount;
        }

        public override string ToString()
        {
            return $"<{N} {Amount}>";
        }
    }

    public class TxOutput : TxOutputBase
    {
        public virtual WalletTag TagFor { get; set; }

        public TxOutput() : base()
        { }
        public TxOutput(string txid, string addr, uint n, BigInteger amount) : base(txid, addr, n, amount)
        { }
   }

    public class TxInput : TxOutputBase
    {
        public TxInput() : base()
        { }
        public TxInput(string txid, string addr, uint n, BigInteger amount) : base(txid, addr, n, amount)
        { }
    }

    public class ChainTx
    {
        public int Id { get; set; }
        public string TxId { get; set; }
        public long Date { get; set; }
        public virtual IEnumerable<TxOutput> TxOutputs { get; set; }
        public virtual IEnumerable<TxInput> TxInputs { get; set; }
        [Column(TypeName = "varchar(255)")]
        public BigInteger Fee { get; set; }
        public long Height { get; set; }
        public long Confirmations { get; set; }
        public virtual ChainAttachment Attachment { get; set; }
        public virtual ChainTxNetworkStatus NetworkStatus { get; set; }

        public ChainTx()
        {
            this.TxId = null;
            this.Date = 0;
            this.Fee = 0;
            this.Height = -1;
            this.Confirmations = 0;
        }

        public ChainTx(string txid, long date, BigInteger fee, long height, long confirmations)
        {
            this.TxId = txid;
            this.Date = date;
            this.Fee = fee;
            this.Height = height;
            this.Confirmations = confirmations;
        }

        public string InputsAddrs()
        {
            if (TxInputs != null)
                return string.Join(",", TxInputs.Select(i => i.Addr).Distinct());
            return "";
        }

        public string OutputsAddrs()
        {
            if (TxOutputs != null)
                return string.Join(",", TxOutputs.Select(o => o.Addr).Distinct());
            return "";
        }

        public BigInteger AmountInputs(string addr=null)
        {
            BigInteger amount = 0;
            var inputs = TxInputs;
            if (addr != null)
                inputs = inputs.Where(i => i.WalletAddr.Address == addr);
            foreach (var i in inputs)
                amount += i.Amount;
            return amount;
        }

        public BigInteger AmountOutputs(string addr = null)
        {
            BigInteger amount = 0;
            var outputs = TxOutputs;
            if (addr != null)
                outputs = outputs.Where(o => o.WalletAddr.Address == addr);
            foreach (var o in outputs)
                amount += o.Amount;
            return amount;
        }

        public ChainTxStatus Status()
        {
            if (NetworkStatus != null)
                return NetworkStatus.Status;
            return ChainTxStatus.Unknown;
        }

        public bool Invalid()
        {
            var status = Status();
            return status == ChainTxStatus.DoubleSpent || status == ChainTxStatus.Expired || status == ChainTxStatus.Invalid || status == ChainTxStatus.Ignored;
        }

        public override string ToString()
        {
            string att = null;
            if (Attachment != null)
                att = System.Text.Encoding.UTF8.GetString(Attachment.Data);
            return $"<{TxId} {Date} +{AmountInputs()} -{AmountOutputs()} {Fee} {Height} {Confirmations} '{att}'>";
        }
    }

    public class ChainAttachment
    {
        public int Id { get; set; }
        public int ChainTxId { get; set; }
        public virtual ChainTx Tx { get; set; }

        public byte[] Data { get; set; }

        public ChainAttachment()
        {
            this.Tx = null;
            this.Data = null;
        }

        public ChainAttachment(ChainTx tx, byte[] data)
        {
            this.Tx = tx;
            this.Data = data;
        }

        public override string ToString()
        {
            return $"<'{Data}'>";
        }
    }

    public class ChainTxNetworkStatus
    {
        public int Id { get; set; }
        public int ChainTxId { get; set; }
        public virtual ChainTx Tx { get; set; }

        public ChainTxStatus Status { get; set; }
        public long DateLastBroadcast { get; set; }
        public byte[] TxBin { get; set; }

        public ChainTxNetworkStatus()
        {
            this.Tx = null;
            this.Status = ChainTxStatus.Unknown;
            this.DateLastBroadcast = 0;
            this.TxBin = null;
        }

        public ChainTxNetworkStatus(ChainTx tx, ChainTxStatus status, long dateLastBroadcast, byte[] txBin)
        {
            this.Tx = tx;
            this.Status = status;
            this.DateLastBroadcast = dateLastBroadcast;
            this.TxBin = txBin;
        }
    }

    public class WalletTag
    {
        public int Id { get; set; }
        public virtual ICollection<WalletAddr> Addrs { get; set; }

        public string Tag { get; set; }

        public WalletTag()
        {
            Addrs = new List<WalletAddr>();
        }

        public override string ToString()
        {
            return $"<{Tag} ({Addrs.Count})>";
        } 
    }

    public class WalletAddr
    {
        public int Id { get; set; }
        public int TagId { get; set; }
        public virtual WalletTag Tag { get; set; }
        public virtual IEnumerable<WalletTx> Txs { get; set; }
        public virtual IEnumerable<TxOutput> TxOutputs { get; set; }
        public virtual IEnumerable<TxInput> TxInputs { get; set; }

        public string Path { get; set; }
        public int PathIndex { get; set; }
        public string Address { get; set; }

        public WalletAddr()
        {
            this.Tag = null;
            this.Txs = new List<WalletTx>();
            this.Path = null;
            this.PathIndex = 0;
            this.Address = null;
        }

        public WalletAddr(WalletTag tag, string path, int pathIndex, string address)
        {
            this.Tag = tag;
            this.Path = path;
            this.PathIndex = pathIndex;
            this.Address = address;
        }

        public BigInteger AmountRecieved(int minConfs=0)
        {
            BigInteger amount = 0;
            if (TxOutputs != null)
                foreach (var o in TxOutputs)
                {
                    if (o.ChainTx.Invalid())
                        continue;
                    if (minConfs <= 0 || o.ChainTx.Confirmations >= minConfs)
                        amount += o.Amount;
                }
            return amount;
        }

        public BigInteger AmountSent()
        {
            BigInteger amount = 0;
            if (TxInputs != null)
                foreach (var i in TxInputs)
                {
                    if (i.ChainTx.Invalid())
                        continue;
                    amount += i.Amount;
                }
            return amount;
        }

        public BigInteger Balance(int minConfs=0)
        {
            return AmountRecieved(minConfs) - AmountSent();
        }

        public override string ToString()
        {
            return $"<'{Tag}' - {Path} - {Address}>";
        }
    }

    public class WalletTx
    {
        public int Id { get; set; }

        public int ChainTxId { get; set; }
        public virtual ChainTx ChainTx { get; set; }

        public int WalletAddrId { get; set; }
        public virtual WalletAddr Address { get; set; }

        public WalletDirection Direction { get; set; }
        public WalletTxState State { get; set; }

        public string InputsAddrs()
        {
            return string.Join(",", ChainTx.TxInputs.Where(i => i.WalletAddrId == WalletAddrId).Select(i => i.Addr).Distinct());
        }

        public string OutputsAddrs()
        {
            return string.Join(",", ChainTx.TxOutputs.Where(o => o.WalletAddrId == WalletAddrId).Select(o => o.Addr).Distinct());
        }

        /// <summary>
        /// Inputs fill transactions.
        /// Inputs drain addresses.
        /// </summary>
        public BigInteger AmountInputs()
        {
            BigInteger amount = 0;
            foreach (var i in ChainTx.TxInputs.Where(i => i.WalletAddrId == WalletAddrId))
                amount += i.Amount;
            return amount;
        }

        /// <summary>
        /// Outputs drain transactions.
        /// Outputs fill addresses.
        /// </summary>
        public BigInteger AmountOutputs()
        {
            BigInteger amount = 0;
            foreach (var o in ChainTx.TxOutputs.Where(o => o.WalletAddrId == WalletAddrId))
                    amount += o.Amount;
            return amount;
        }

        public override string ToString()
        {
            return $"<{ChainTx} {Address} {Direction} {State}>";
        }
    }

    public class WalletPendingSpend
    {
        public int Id { get; set; }

        public int TagId { get; set; }
        public virtual WalletTag Tag { get; set; }

        public int TagChangeId { get; set; }
        public virtual WalletTag TagChange { get; set; }

        public virtual WalletTag TagFor { get; set; }

        public long Date { get; set; }
        public string SpendCode { get; set; }
        public PendingSpendState State { get; set; }
        public WalletError Error { get; set; }
        public string ErrorMessage { get; set; }
        public string To { get; set; }
        [Column(TypeName = "varchar(255)")]
        public BigInteger Amount { get; set; }

        public string TxIds { get; set; }
        
        public override string ToString()
        {
            return $"<{SpendCode} {Date} {To} {Amount} {State} {Tag} {TagChange} {TagFor} {TxIds}>";
        }
    }

    public class TxOutputForTag
    {
        public int TxOutputId { get; set; }
        public virtual TxOutput TxOutput { get; set; }

        public int TagId { get; set; }
        public virtual WalletTag Tag { get; set; }
    }

    public class PendingSpendForTag
    {
        public int PendingSpendId { get; set; }
        public virtual WalletPendingSpend PendingSpend { get; set; }

        public int TagId { get; set; }
        public virtual WalletTag Tag { get; set; }
    }

    public class FiatWalletContext : BaseContext
    {
        public DbSet<RecipientParams> RecipientParams { get; set; }
        public DbSet<BankTx> BankTxs { get; set; }
        public DbSet<FiatWalletTag> WalletTags { get; set; }
        public DbSet<FiatWalletTx> WalletTxs { get; set; }

        public FiatWalletContext(DbContextOptions<FiatWalletContext> options, bool logToConsole, bool lazyLoading) : base(options, logToConsole, lazyLoading)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RecipientParams>()
                .HasIndex(t => t.FiatWalletTxId)
                .IsUnique();

            builder.Entity<FiatWalletTag>()
                .HasIndex(t => t.Tag)
                .IsUnique();

            builder.Entity<FiatWalletTx>()
                .HasIndex(t => t.DepositCode)
                .IsUnique();
        }

        public FiatWalletTag TagGet(string tag)
        {
            return WalletTags.SingleOrDefault(t => t.Tag == tag);
        }

        public FiatWalletTag TagGetOrCreate(string tag)
        {
            var _tag = TagGet(tag);
            if (_tag == null)
            {
                _tag = new FiatWalletTag{ Tag = tag };
                WalletTags.Add(_tag);
            }
            return _tag;
        }

        public IEnumerable<FiatWalletTx> TxsGet()
        {
            return WalletTxs;
        }

        public IEnumerable<FiatWalletTx> TxsGet(string tag)
        {
            var _tag = TagGet(tag);
            if (_tag != null)
                return _tag.Txs;
            return new List<FiatWalletTx>();
        }

        public IEnumerable<FiatWalletTx> TxsGet(WalletDirection direction, bool completed)
        {
            if (completed)
                return WalletTxs.Where(t => t.Direction == direction && t.BankTxId != null);
            else
                return WalletTxs.Where(t => t.Direction == direction && t.BankTxId == null);
        }

        public FiatWalletTx TxGet(string depositCode)
        {
            return WalletTxs.SingleOrDefault(t => t.DepositCode == depositCode);
        }

        public RecipientParams RecipientParamsGet(FiatWalletTx tx)
        {
            return RecipientParams.SingleOrDefault(r => r.FiatWalletTxId == tx.Id);
        }
    }

    public class RecipientParams
    {
        public int Id { get; set; }
        public int FiatWalletTxId { get; set; }
        public virtual FiatWalletTx FiatWalletTx { get; set; }
        public string Reference { get; set; }
        public string Code { get; set; }
        public string Particulars { get; set; }
    }

    public class BankTx
    {
        public int Id { get; set; }
        public string BankMetadata { get; set; }
        public long Date { get; set; }
        public long Amount { get; set; }

        public BankTx()
        {
            this.BankMetadata = null;
            this.Date = 0;
            this.Amount = 0;
        }

        public BankTx(string bankMetadata, long date, long amount)
        {
            this.BankMetadata = bankMetadata;
            this.Date = date;
            this.Amount = amount;
        }

        public override string ToString()
        {
            return $"<{BankMetadata} {Date} {Amount}>";
        }
    }

    public class FiatWalletTag
    {
        public int Id { get; set; }
        public virtual ICollection<FiatWalletTx> Txs { get; set; }

        public string Tag { get; set; }

        public FiatWalletTag()
        {
            Txs = new List<FiatWalletTx>();
        }

        public override string ToString()
        {
            return $"{Tag} ({Txs.Count})";
        } 
    }

    public class FiatWalletTx
    {
        public int Id { get; set; }

        public int? BankTxId { get; set; }
        public virtual BankTx BankTx { get; set; }

        public int FiatWalletTagId { get; set; }
        public virtual FiatWalletTag Tag { get; set; }

        public WalletDirection Direction { get; set; }
        public long Date { get; set; }
        public long Amount { get; set; }
        public string DepositCode { get; set; }

        public string BankName { get; set; }
        public string BankAddress { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }
        
        public override string ToString()
        {
            return $"<{BankTx} {DepositCode} {Direction} {Date} {Amount}>";
        }
    }
}