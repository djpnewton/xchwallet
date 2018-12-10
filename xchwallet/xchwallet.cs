using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace xchwallet
{
    public static class Util
    {
        struct WalletType
        {
            public string Type;
            public WalletType(string type)
            {
                this.Type = type;
            }
        }
        public static string GetWalletType(string filename)
        {
            if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
            {
                var wt = JsonConvert.DeserializeObject<WalletType>(File.ReadAllText(filename));
                return wt.Type;
            }
            return null;
        }
    }

    public interface IAddress
    {
        string Tag { get; }
        string Path { get; }
        string Address { get; }
    }

    public enum WalletDirection
    {
        Incomming, Outgoing
    }

    public class TxMetadata
    {
        public bool Acknowledged;
        public string Note;
        public long Id;
        public string TagOnBehalfOf;

        public void SetAck(bool value)
        {
            Acknowledged = value;
        }

        public void SetNote(string value)
        {
            Note = value;
        }

        public void SetId(long value)
        {
            Id = value;
        }
        
        public void SetTagOnBehalfOf(string value)
        {
            TagOnBehalfOf = value;
        }
    }

    public interface ITransaction
    {
        string Id { get; }
        long Date { get; }
        string From { get; }
        string To { get; }
        WalletDirection Direction { get; }
        BigInteger Amount { get; }
        BigInteger Fee { get; }
        long Confirmations { get; }
        // TODO: maybe this should be in its own stucture.. or the wallet backend should just ensure it is preserved when rescanning the blockchain
        TxMetadata WalletDetails { get; set; }
    }

    public enum WalletError
    {
        Success,
        MaxFeeBreached,
        InsufficientFunds,
        FailedBroadcast, // A wallet which can compete an operation with a single transaction might return this error when trying to broadcast it
        PartialBroadcast, // A wallet which might require multiple transactions might return this error
    }

    public interface IWallet
    {
        string Type();
        bool IsMainnet();
        IEnumerable<string> GetTags();
        IAddress NewAddress(string tag);
        IAddress NewOrUnusedAddress(string tag);
        IEnumerable<IAddress> GetAddresses(string tag);
        IEnumerable<ITransaction> GetTransactions(string tag);
        IEnumerable<ITransaction> GetAddrTransactions(string address);
        BigInteger GetBalance(string tag);
        BigInteger GetAddrBalance(string address);
        // feeUnit is wallet specific, in BTC it is satoshis per byte, in ETH it is GWEI per gas, in Waves it is a fixed transaction fee
        WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        IEnumerable<ITransaction> GetUnacknowledgedTransactions(string tag);
        void AcknowledgeTransactions(string tag, IEnumerable<ITransaction> txs);
        void AddNote(string tag, IEnumerable<string> txids, string note);
        void AddNote(string tag, string txid, string note);
        void SetTagOnBehalfOf(string tag, IEnumerable<string> txids, string tagOnBehalfOf);
        void SetTagOnBehalfOf(string tag, string txid, string tagOnBehalfOf);
        void SetTxWalletId(string tag, string txid, long id);
        long GetNextTxWalletId(string tag);
        bool ValidateAddress(string address);
        string AmountToString(BigInteger value);
        BigInteger StringToAmount(string value);

        void Save(string filename);
    }

    public class BaseAddress : IAddress
    {
        public string Tag { get; }
        public string Path { get; }
        public string Address { get; }

        public BaseAddress(string tag, string path, string address)
        {
            this.Tag = tag;
            this.Path = path;
            this.Address = address;
        }

        public override string ToString()
        {
            return $"'{Tag}' - {Path} - {Address}";
        }
    }

    public class BaseTransaction : ITransaction
    {
        public string Id { get; }
        public long Date { get; }
        public string From { get; }
        public string To { get; }
        public WalletDirection Direction { get; }
        public BigInteger Amount { get; }
        public BigInteger Fee { get; }
        public long Confirmations { get; }
        public TxMetadata WalletDetails { get; set; }

        public BaseTransaction(string id, long date, string from, string to, WalletDirection direction, BigInteger amount, BigInteger fee, long confirmations)
        {
            this.Id = id;
            this.Date = date;
            this.From = from;
            this.To = to;
            this.Direction = direction;
            this.Amount = amount;
            this.Fee = fee;
            this.Confirmations = confirmations;
            this.WalletDetails = new TxMetadata();
        }

        public override string ToString()
        {
            return $"<{Id} {Date} {From} {To} {Amount} {Confirmations} {Direction}>";
        }
    }

    public abstract class BaseWallet : IWallet
    {
        public abstract string Type();
        public abstract bool IsMainnet();
        public abstract IEnumerable<string> GetTags();
        public abstract IAddress NewAddress(string tag);
        public abstract IEnumerable<IAddress> GetAddresses(string tag);
        public abstract IEnumerable<ITransaction> GetTransactions(string tag);
        public abstract IEnumerable<ITransaction> GetAddrTransactions(string address);
        public abstract BigInteger GetBalance(string tag);
        public abstract BigInteger GetAddrBalance(string address);
        public abstract WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        public abstract WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        public abstract IEnumerable<ITransaction> GetAddrUnacknowledgedTransactions(string address);
        public abstract string AmountToString(BigInteger value);
        public abstract BigInteger StringToAmount(string value);
        public abstract void Save(string filename);
        public abstract bool ValidateAddress(string address);

        public IAddress NewOrUnusedAddress(string tag)
        {
            foreach (var addr in GetAddresses(tag))
            {
                var txs = GetAddrTransactions(addr.Address);
                if (!txs.Any())
                    return addr;
            }
            return NewAddress(tag);
        }

        public IEnumerable<ITransaction> GetUnacknowledgedTransactions(string tag)
        {
            var txs = new List<ITransaction>();
            foreach (var addr in GetAddresses(tag))
            {
                var addrTxs = GetAddrUnacknowledgedTransactions(addr.Address);
                txs.AddRange(addrTxs);
            }
            return txs;
        }

        public void AcknowledgeTransactions(string tag, IEnumerable<ITransaction> txs)
        {
            foreach (var tx in txs)
                tx.WalletDetails.SetAck(true);
        }

        public void AddNote(string tag, IEnumerable<string> txids, string note)
        {
            foreach (var tx in GetTransactions(tag))
                if (txids.Contains(tx.Id))
                    tx.WalletDetails.SetNote(note);
        }

        public void AddNote(string tag, string txid, string note)
        {
            foreach (var tx in GetTransactions(tag))
                if (tx.Id == txid)
                {
                    tx.WalletDetails.SetNote(note);
                    break;
                }
        }

        public void SetTagOnBehalfOf(string tag, IEnumerable<string> txids, string tagOnBehalfOf)
        {
            foreach (var tx in GetTransactions(tag))
                if (txids.Contains(tx.Id))
                    tx.WalletDetails.SetTagOnBehalfOf(tagOnBehalfOf);
        }

        public void SetTagOnBehalfOf(string tag, string txid, string tagOnBehalfOf)
        {
            foreach (var tx in GetTransactions(tagOnBehalfOf))
                if (tx.Id == txid)
                {
                    tx.WalletDetails.SetTagOnBehalfOf(tagOnBehalfOf);
                    break;
                }
        }

        public void SetTxWalletId(string tag, IEnumerable<string> txids, long id)
        {
            foreach (var tx in GetTransactions(tag))
                if (txids.Contains(tx.Id))
                    tx.WalletDetails.SetId(id);
        }

        public void SetTxWalletId(string tag, string txid, long id)
        {
            foreach (var tx in GetTransactions(tag))
                if (tx.Id == txid)
                {
                    tx.WalletDetails.SetId(id);
                    break;
                }
        }
                
        public long GetNextTxWalletId(string tag)
        {
            long res = 0;
            foreach (var tx in GetTransactions(tag))
                if (tx.WalletDetails.Id > res)
                    res = tx.WalletDetails.Id;
            return res + 1;
        }
    }
}
