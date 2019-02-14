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
                    optionsBuilder.UseMySql(connection);
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
            return new WalletContext(optionsBuilder.Options, false);
        }
    }

    public class FiatWalletContextFactory : BaseContextFactory, IDesignTimeDbContextFactory<FiatWalletContext>
    {
        public FiatWalletContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FiatWalletContext>();
            initOptionsBuilder(optionsBuilder);
            return new FiatWalletContext(optionsBuilder.Options, false);
        }
    }

    public abstract class BaseContext : DbContext
    {
        public DbSet<WalletCfg> WalletCfgs { get; set; }

        public static T CreateSqliteWalletContext<T>(string filename, bool logToConsole)
            where T : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            optionsBuilder.UseSqlite($"Data Source={filename}");
            return (T)Activator.CreateInstance(typeof(T), new object[] { optionsBuilder.Options, logToConsole });
        }

        bool logToConsole;

        public BaseContext(DbContextOptions options, bool logToConsole) : base(options)
        {
            this.logToConsole = logToConsole;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (logToConsole)
            {
                var loggerFactory = new LoggerFactory()
                    .AddDebug((categoryName, logLevel) => (logLevel == LogLevel.Information) && (categoryName == DbLoggerCategory.Database.Command.Name))
                    .AddConsole((categoryName, logLevel) => (logLevel == LogLevel.Information) && (categoryName == DbLoggerCategory.Database.Command.Name))
                    ;
                optionsBuilder.UseLoggerFactory(loggerFactory);
            }
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
        public DbSet<ChainTx> ChainTxs { get; set; }
        public DbSet<WalletTag> WalletTags { get; set; }
        public DbSet<WalletAddr> WalletAddrs { get; set; }
        public DbSet<WalletTx> WalletTxs { get; set; }
        public DbSet<WalletPendingSpend> WalletPendingSpends { get; set; }
        public DbSet<WalletTxMeta> WalletTxMetas { get; set; }

        public int LastPathIndex
        {
            get { return CfgGetInt("LastPathIndex", 0); }
            set { CfgSetInt("LastPathIndex", value); }
        }

        public WalletContext(DbContextOptions<WalletContext> options, bool logToConsole) : base(options, logToConsole)
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
                
            builder.Entity<ChainTx>()
                .HasIndex(t => t.TxId)
                .IsUnique();
            builder.Entity<ChainTx>()
                .Property(t => t.Amount)
                .HasConversion(bigIntConverter);
            builder.Entity<ChainTx>()
                .Property(t => t.Fee)
                .HasConversion(bigIntConverter);

            builder.Entity<WalletPendingSpend>()
                .HasIndex(t => t.SpendCode)
                .IsUnique();
            builder.Entity<WalletPendingSpend>()
                .Property(s => s.Amount)
                .HasConversion(bigIntConverter);
        }

        public WalletTag TagGet(string tag)
        {
            return WalletTags.SingleOrDefault(t => t.Tag == tag);
        }

        public WalletTag TagCreate(string tag)
        {
            var _tag = new WalletTag{ Tag = tag };
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

        public WalletTx TxGet(WalletAddr addr, ChainTx tx)
        {
            return WalletTxs.SingleOrDefault(t => t.WalletAddrId == addr.Id && t.ChainTxId == tx.Id);
        }

        public IEnumerable<WalletTx> TxsUnAckedGet(string address)
        {
            var addr = AddrGet(address);
            if (addr != null)
                return WalletTxs.Where(t => t.WalletAddrId == addr.Id && t.Acknowledged == false);
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

        public WalletPendingSpend PendingSpendGet(string spendCode)
        {
            return WalletPendingSpends.SingleOrDefault(s => s.SpendCode == spendCode);
        }

        public WalletTxMeta WalletTxAddMeta(WalletTx wtx)
        {
            if (wtx.Meta != null)
                throw new WalletException("WalletTx already has a Meta field");
            var meta = new WalletTxMeta();
            wtx.Meta = meta;
            WalletTxMetas.Add(meta);
            return meta;
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

    public class ChainTx
    {
        public int Id { get; set; }
        public string TxId { get; set; }
        public long Date { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        [Column(TypeName = "string")]
        public BigInteger Amount { get; set; }
        [Column(TypeName = "string")]
        public BigInteger Fee { get; set; }
        public long Height { get; set; }
        public long Confirmations { get; set; }

        public ChainTx()
        {
            this.TxId = null;
            this.Date = 0;
            this.From = null;
            this.To = null;
            this.Amount = 0;
            this.Fee = 0;
            this.Height = -1;
            this.Confirmations = 0;
        }

        public ChainTx(string txid, long date, string from, string to, BigInteger amount, BigInteger fee, long height, long confirmations)
        {
            this.TxId = txid;
            this.Date = date;
            this.From = from;
            this.To = to;
            this.Amount = amount;
            this.Fee = fee;
            this.Height = height;
            this.Confirmations = confirmations;
        }

        public override string ToString()
        {
            return $"<{TxId} {Date} {From} {To} {Amount} {Fee} {Height} {Confirmations}>";
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
        public bool Acknowledged { get; set; }

        public int WalletTxMetaId { get; set; }
        public virtual WalletTxMeta Meta { get; set; }
        
        public override string ToString()
        {
            return $"<{ChainTx} {Address} {Direction} {Acknowledged} {Meta?.Id} {Meta?.Note} {Meta?.TagOnBehalfOf}>";
        }
    }

    public class WalletTxMeta
    {
        public int Id { get; set; }

        public string Note { get; set; }
        public string TagOnBehalfOf { get; set; }
        
        public override string ToString()
        {
            return $"<{Id} '{Note}' '{TagOnBehalfOf}'>";
        }
    }

    public class WalletPendingSpend
    {
        public int Id { get; set; }

        public int TagId { get; set; }
        public virtual WalletTag Tag { get; set; }

        public int TagChangeId { get; set; }
        public virtual WalletTag TagChange { get; set; }

        public int? WalletTxId { get; set; }
        public virtual WalletTx Tx { get; set; }

        public long Date { get; set; }
        public string SpendCode { get; set; }
        public PendingSpendState State { get; set; }
        public WalletError Error { get; set; }
        public string ErrorMessage { get; set; }
        public string To { get; set; }
        public BigInteger Amount { get; set; }

        public int WalletTxMetaId { get; set; }
        public virtual WalletTxMeta Meta { get; set; }
        
        public override string ToString()
        {
            return $"<{SpendCode} {Date} {To} {Amount} {State} {Tag} {TagChange} {Tx}>";
        }
    }

    public class FiatWalletContext : BaseContext
    {
        public DbSet<BankTx> BankTxs { get; set; }
        public DbSet<FiatWalletTag> WalletTags { get; set; }
        public DbSet<FiatWalletTx> WalletTxs { get; set; }

        public FiatWalletContext(DbContextOptions<FiatWalletContext> options, bool logToConsole) : base(options, logToConsole)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<WalletTag>()
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

        public IEnumerable<FiatWalletTx> TxsGet(string tag)
        {
            var _tag = TagGet(tag);
            if (_tag != null)
                return _tag.Txs;
            return new List<FiatWalletTx>();
        }

        public FiatWalletTx TxGet(string depositCode)
        {
            return WalletTxs.SingleOrDefault(t => t.DepositCode == depositCode);
        }
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