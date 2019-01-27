using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

// for 'dotnet ef migrations add InitiialCreate --project <project path with dbcontext>'
// see https://docs.microsoft.com/en-us/ef/core/miscellaneous/cli/dotnet#target-project-and-startup-project

namespace xchwallet
{
    public class WalletContextFactory : IDesignTimeDbContextFactory<WalletContext>
    {
        public WalletContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<WalletContext>();
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

            return new WalletContext(optionsBuilder.Options);
        }
    }

    public class WalletContext : DbContext
    {
        public DbSet<WalletCfg> WalletCfgs { get; set; }
        public DbSet<ChainTx> ChainTxs { get; set; }
        public DbSet<WalletTag> WalletTags { get; set; }
        public DbSet<WalletAddr> WalletAddrs { get; set; }
        public DbSet<WalletTx> WalletTxs { get; set; }

        public int LastPathIndex
        {
            get { return CfgGetInt("LastPathIndex", 0); }
            set { CfgSetInt("LastPathIndex", value); }
        }

        public static WalletContext CreateSqliteWalletContext(string filename)
        {
            var optionsBuilder = new DbContextOptionsBuilder<WalletContext>();
            optionsBuilder.UseSqlite($"Data Source={filename}");
            return new WalletContext(optionsBuilder.Options);
        }

         public WalletContext(DbContextOptions<WalletContext> options) : base(options)
        { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<WalletCfg>()
                .HasIndex(s => s.Key)
                .IsUnique();

            builder.Entity<ChainTx>()
                .HasIndex(t => t.TxId)
                .IsUnique();

            var bigIntConverter = new ValueConverter<BigInteger, string>(
                v => v.ToString(),
                v => BigInteger.Parse(v));
            builder.Entity<ChainTx>()
                .Property(t => t.Amount)
                .HasConversion(bigIntConverter);
            builder.Entity<ChainTx>()
                .Property(t => t.Fee)
                .HasConversion(bigIntConverter);
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

        public WalletTag TagGet(string tag)
        {
            return WalletTags.SingleOrDefault(t => t.Tag == tag);
        }

        public WalletTag TagGetOrCreate(string tag)
        {
            var _tag = TagGet(tag);
            if (_tag == null)
            {
                _tag = new WalletTag{ Tag = tag };
                WalletTags.Add(_tag);
            }
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

        public WalletTx TxGet(ChainTx tx)
        {
            return WalletTxs.SingleOrDefault(t => t.ChainTxId == tx.Id);
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
        public long Confirmations { get; set; }

        public ChainTx()
        {
            this.TxId = null;
            this.Date = 0;
            this.From = null;
            this.To = null;
            this.Amount = 0;
            this.Fee = 0;
            this.Confirmations = 0;
        }

        public ChainTx(string txid, long date, string from, string to, BigInteger amount, BigInteger fee, long confirmations)
        {
            this.TxId = txid;
            this.Date = date;
            this.From = from;
            this.To = to;
            this.Amount = amount;
            this.Fee = fee;
            this.Confirmations = confirmations;
        }

        public override string ToString()
        {
            return $"<{TxId} {Date} {From} {To} {Amount} {Fee} {Confirmations}>";
        }
    }

    public class WalletTag
    {
        public int Id { get; set; }
        public virtual ICollection<WalletAddr> Addrs { get; set; }

        public string Tag { get; set; }

        public override string ToString()
        {
            return $"{Tag} ({Addrs.Count})";
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
        public string Note { get; set; }
        public long WalletId { get; set; }
        public string TagOnBehalfOf { get; set; }
        
        public override string ToString()
        {
            return $"<{ChainTx} {Address} {Direction} {Acknowledged} {Note} {WalletId} {TagOnBehalfOf}>";
        }
    }
}