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
            var dbtype = System.Environment.GetEnvironmentVariable("DB_TYPE");
            var connection = System.Environment.GetEnvironmentVariable("CONNECTION_STRING");
            if (string.IsNullOrEmpty(dbtype) || string.IsNullOrEmpty(connection))
                throw new System.ArgumentException($"empty environment variable DB_TYPE '{dbtype}' or CONNECTION_STRING '{connection}'");

            var optionsBuilder = new DbContextOptionsBuilder<WalletContext>();
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
        public DbSet<ChainTx> ChainTxs { get; set; }
        public DbSet<WalletAddr> WalletAddrs { get; set; }
        public DbSet<WalletTx> WalletTxs { get; set; }

        public WalletContext(DbContextOptions<WalletContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var bigIntConverter = new ValueConverter<BigInteger, string>(
                v => v.ToString(),
                v => BigInteger.Parse(v));
                
            builder.Entity<ChainTx>()
                .Property(r => r.Amount)
                .HasConversion(bigIntConverter);
            builder.Entity<ChainTx>()
                .Property(r => r.Fee)
                .HasConversion(bigIntConverter);
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

    public class WalletAddr
    {
        public int Id { get; set; }
        public string Tag { get; set; }
        public string Path { get; set; }
        public string Address { get; set; }

        public WalletAddr()
        {
            this.Tag = null;
            this.Path = null;
            this.Address = null;
        }

        public WalletAddr(string tag, string path, string address)
        {
            this.Tag = tag;
            this.Path = path;
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
        public ChainTx ChainTx { get; set; }

        public int WalletAddrId { get; set; }
        public WalletAddr Address { get; set; }

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