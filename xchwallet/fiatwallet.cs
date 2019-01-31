using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using WavesCS;
using Microsoft.Extensions.Logging;

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
        IEnumerable<FiatWalletTx> GetTransactions(string tag);
        FiatWalletTx RegisterPendingDeposit(string tag, long amount);
        FiatWalletTx UpdateDeposit(string depositCode, long date, long amount, string bankMetadata);
        FiatWalletTx RegisterPendingWithdrawal(string tag, long amount);
        FiatWalletTx UpdateWithdrawal(string txid, long date, long amount, string bankMetadata);
        FiatWalletTx GetTx(string depositCode);
        string AmountToString(long value);
        long StringToAmount(string value);
        BankAccount GetAccount();

        void Save();
    }

    public class FiatWallet : IFiatWallet
    {
        protected ILogger logger = null;
        protected FiatWalletContext db = null;
        protected string type = null;
        protected BankAccount account = null;

        void CheckType()
        {
            var type = Util.GetWalletType(db);
            if (type == null)
                // newly initialised wallet
                return;
            if (type != Type())
                throw new Exception($"Type found in db ({type}) does not match this wallet class ({Type()})");
        }

        string CreateDepositCode(int length=10, bool allowLetters=false)
        {
            var code = new StringBuilder();
            var rng = new RNGCryptoServiceProvider();
            var rnd = new byte[1];
            int n = 0;
            while (n < length) {
                rng.GetBytes(rnd);
                var c = (char)rnd[0];
                if ((Char.IsDigit(c) || (allowLetters && Char.IsLetter(c))) && rnd[0] < 127) {
                    ++n;
                    code.Append(char.ToUpper(c));
                }
            }
            return code.ToString();
        }

        public FiatWallet(ILogger logger, FiatWalletContext db, string type, BankAccount account)
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

        public IEnumerable<FiatWalletTag> GetTags()
        {
            return db.WalletTags;
        }

        public IEnumerable<FiatWalletTx> GetTransactions(string tag)
        {
            return db.TxsGet(tag);
        }

        public FiatWalletTx RegisterPendingDeposit(string tag, long amount)
        {
            var depositCode = CreateDepositCode();
            while (db.TxGet(depositCode) != null)
                depositCode = CreateDepositCode();
            var _tag = db.TagGetOrCreate(tag);
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tx = new FiatWalletTx { DepositCode=depositCode, Tag=_tag, Date=date, Direction=WalletDirection.Incomming, Amount=amount };
            db.WalletTxs.Add(tx);
            return tx;
        }

        public FiatWalletTx UpdateDeposit(string depositCode, long date, long amount, string bankMetadata)
        {
            var tx = db.TxGet(depositCode);
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

        public FiatWalletTx RegisterPendingWithdrawal(string tag, long amount)
        {
            var depositCode = CreateDepositCode();
            while (db.TxGet(depositCode) != null)
                depositCode = CreateDepositCode();
            var _tag = db.TagGetOrCreate(tag);
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tx = new FiatWalletTx { DepositCode=depositCode, Tag=_tag, Date=date, Direction=WalletDirection.Outgoing, Amount=amount };
            db.WalletTxs.Add(tx);
            return tx;
        }

        public FiatWalletTx UpdateWithdrawal(string depositCode, long date, long amount, string bankMetadata)
        {
            var tx = db.TxGet(depositCode);
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

        public BankAccount GetAccount()
        {
            return account;
        }

        public string AmountToString(long value)
        {
            var _scale = new decimal(1, 0, 0, false, 2);
            return ((decimal)value * _scale).ToString();
        }

        public long StringToAmount(string value)
        {
            var _scale = new decimal(1, 0, 0, false, 2);
            var d = decimal.Parse(value) / _scale;
            return (long)d;
        }
    }
}
