using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace xchwallet
{
    public class BankAccount
    {
        public string BankName { get; set; }
        public string BankAddress { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }

        public BankAccount()
        {}
    }

    public interface IFiatWallet
    {
        string Type();
        IEnumerable<FiatWalletTag> GetTags();
        bool HasTag(string tag);
        FiatWalletTag NewTag(string tag);
        IEnumerable<FiatWalletTx> GetTransactions();
        IEnumerable<FiatWalletTx> GetTransactions(string tag);
        long GetBalance(string tag);
        FiatWalletTx RegisterPendingDeposit(string tag, long amount);
        FiatWalletTx UpdateDeposit(string depositCode, long date, long amount, string bankMetadata);
        FiatWalletTx RegisterPendingWithdrawal(string tag, long amount, BankAccount account);
        FiatWalletTx UpdateWithdrawal(string depositCode, long date, long amount, string bankMetadata);
        FiatWalletTx GetTx(string depositCode);
        IEnumerable<FiatWalletTx> GetPendingDeposits();
        IEnumerable<FiatWalletTx> GetPendingWithdrawals();
        long AmountToLong(decimal value);
        decimal AmountToDecimal(long value);
        string AmountToString(long value);
        string AmountToString(decimal value);
        long StringToAmount(string value);
        BankAccount GetAccount();

        IDbContextTransaction BeginDbTransaction();
        void Save();
    }

    public interface IFiatWallet2
    {
        long GetBalance();
        long GetBalance(string tag);
        long GetBalance(IEnumerable<string> tags);
    }

    public abstract class BaseFiatWallet
    {
        readonly protected ILogger logger = null;
        readonly protected FiatWalletContext db = null;
        readonly protected string type = null;
        readonly protected BankAccount account = null;

        void CheckType()
        {
            var type = Util.GetWalletType(db);
            if (type == null)
                // newly initialised wallet
                return;
            if (type != Type())
                throw new Exception($"Type found in db ({type}) does not match this wallet class ({Type()})");
        }

        public BaseFiatWallet(ILogger logger, FiatWalletContext db, string type, BankAccount account)
        {
            this.logger = logger;
            this.db = db;
            this.type = type;
            this.account = account;
            CheckType();
        }

        public void Save()
        {
            if (Type() == null)
                throw new Exception("Wallet type must be initialised before saving");
            db.CfgSet(Util.TYPE_KEY, Type());
            db.SaveChanges();
        }

        public string Type()
        {
            return type;
        }
    }

    public class FiatWallet : BaseFiatWallet, IFiatWallet
    {
        public FiatWallet(ILogger logger, FiatWalletContext db, string type, BankAccount account) : base(logger, db, type, account)
        {
            System.Diagnostics.Debug.Assert(db.LazyLoading == true);
        }

        public IEnumerable<FiatWalletTag> GetTags()
        {
            return db.WalletTags;
        }

        public bool HasTag(string tag)
        {
            return db.WalletTags.Any(t => t.Tag == tag);
        }

        public FiatWalletTag NewTag(string tag)
        {
            var tag_ = new FiatWalletTag{ Tag = tag };
            db.WalletTags.Add(tag_);
            return tag_;
        }

        public IEnumerable<FiatWalletTx> GetTransactions()
        {
            return db.TxsGet();
        }

        public IEnumerable<FiatWalletTx> GetTransactions(string tag)
        {
            return db.TxsGet(tag);
        }

        public long GetBalance(string tag)
        {
            long total = 0;
            foreach (var tx in GetTransactions(tag))
            {
                if (tx.BankTx != null)
                {
                    if (tx.Direction == WalletDirection.Incomming)
                        total += tx.BankTx.Amount;
                    else
                        total -= tx.BankTx.Amount;
                }
            }
            return total;
        }

        public FiatWalletTx RegisterPendingDeposit(string tag, long amount)
        {
            var depositCode = Utils.CreateDepositCode();
            while (db.TxGet(depositCode) != null)
                depositCode = Utils.CreateDepositCode();
            var _tag = db.TagGet(tag);
            Util.WalletAssert(_tag != null, $"Tag '{tag}' does not exist");
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tx = new FiatWalletTx { DepositCode=depositCode, Tag=_tag, Date=date, Direction=WalletDirection.Incomming, Amount=amount };
            db.WalletTxs.Add(tx);
            return tx;
        }

        public FiatWalletTx UpdateDeposit(string depositCode, long date, long amount, string bankMetadata)
        {
            var tx = db.TxGet(depositCode);
            if (tx == null)
                throw new Exception("Tx not found");
            if (tx.Direction != WalletDirection.Incomming)
                throw new Exception("Tx is not deposit");
            if (tx.BankTx != null)
                throw new Exception("BankTx already set");
            var bankTx = new BankTx(bankMetadata, date, amount);
            if (tx.Amount != bankTx.Amount)
                throw new Exception("Amount must match");
            tx.BankTx = bankTx;
            db.BankTxs.Add(bankTx);
            db.WalletTxs.Update(tx);
            return tx;
        }

        public FiatWalletTx RegisterPendingWithdrawal(string tag, long amount, BankAccount account)
        {
            var depositCode = Utils.CreateDepositCode();
            while (db.TxGet(depositCode) != null)
                depositCode = Utils.CreateDepositCode();
            var _tag = db.TagGetOrCreate(tag);
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tx = new FiatWalletTx { DepositCode=depositCode, Tag=_tag, Date=date, Direction=WalletDirection.Outgoing, Amount=amount,
                BankName=account.BankName, BankAddress=account.BankAddress, AccountName=account.AccountName, AccountNumber=account.AccountNumber };
            db.WalletTxs.Add(tx);
            return tx;
        }

        public FiatWalletTx UpdateWithdrawal(string depositCode, long date, long amount, string bankMetadata)
        {
            var tx = db.TxGet(depositCode);
            if (tx == null)
                throw new Exception("Tx not found");
            if (tx.Direction != WalletDirection.Outgoing)
                throw new Exception("Tx is not withdrawal");
            if (tx.BankTx != null)
                throw new Exception("BankTx already set");
            var bankTx = new BankTx(bankMetadata, date, amount);
            if (tx.Amount != bankTx.Amount)
                throw new Exception("Amount must match");
            tx.BankTx = bankTx;
            db.BankTxs.Add(bankTx);
            db.WalletTxs.Update(tx);
            return tx;
        }

        public FiatWalletTx GetTx(string depositCode)
        {
            return db.TxGet(depositCode);
        }

        public IEnumerable<FiatWalletTx> GetPendingDeposits()
        {
            return db.TxsGet(WalletDirection.Incomming, false);
        }

        public IEnumerable<FiatWalletTx> GetPendingWithdrawals()
        {
            return db.TxsGet(WalletDirection.Outgoing, false);
        }

        public BankAccount GetAccount()
        {
            return account;
        }

        public long AmountToLong(decimal value)
        {
            return Convert.ToInt64(decimal.Round(value * 100));
        }

        public decimal AmountToDecimal(long value)
        {
            return Convert.ToDecimal(value) / 100;
        }

        public string AmountToString(long value)
        {
            var _scale = new decimal(1, 0, 0, false, 2);
            return ((decimal)value * _scale).ToString();
        }

        public string AmountToString(decimal value)
        {
            return value.ToString("#0.00");
        }

        public long StringToAmount(string value)
        {
            var _scale = new decimal(1, 0, 0, false, 2);
            var d = decimal.Parse(value) / _scale;
            return (long)d;
        }

        public IDbContextTransaction BeginDbTransaction()
        {
            return db.Database.BeginTransaction();
        }
    }

    public class FastFiatWallet : BaseFiatWallet, IFiatWallet2
    {
        public FastFiatWallet(ILogger logger, FiatWalletContext db, string type, BankAccount account) : base(logger, db, type, account)
        {
            System.Diagnostics.Debug.Assert(db.LazyLoading == false);
        }

        private static long TxsBalance(IQueryable<FiatWalletTx> txs)
        {
            long balance = 0;
            foreach (var tx in txs)
                balance += tx.Direction == WalletDirection.Incomming ? (tx.BankTx != null ? tx.Amount : 0) : -tx.Amount;
            return balance;
        }

        public long GetBalance()
        {
            var allTxs = db.WalletTags.SelectMany(t => t.Txs)
                .Include(tx => tx.BankTx);
            return TxsBalance(allTxs);
        }

        public long GetBalance(string tag)
        {
            var txs = db.WalletTags.Where(t => t.Tag == tag).SelectMany(t => t.Txs)
                .Include(tx => tx.BankTx);
            return TxsBalance(txs);
        }

        public long GetBalance(IEnumerable<string> tags)
        {
            var txs = db.WalletTags.Where(t => tags.Contains(t.Tag)).SelectMany(t => t.Txs)
                .Include(tx => tx.BankTx);
            return TxsBalance(txs);
        }
    }
}
