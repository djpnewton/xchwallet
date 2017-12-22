using System;
using System.Collections.Generic;

namespace xchwallet
{
    public interface IAddress
    {
        int Id { get; }
        string Tag { get; }
        string Address { get; }
    }

    public interface ITransaction
    {
        string From { get; }
        string To { get; }
        UInt64 Amount { get; }
        int Confirmations { get; }
    }

    public interface IWallet
    {
        bool IsMainnet();
        IAddress NewAddress(int id, string tag);
        List<ITransaction> GetTransactions(string address);
    }

    public class BaseAddress : IAddress
    {
        public int Id { get; }
        public string Tag { get; }
        public string Address { get; }

        public BaseAddress(int id, string tag, string address)
        {
            this.Id = id;
            this.Tag = tag;
            this.Address = address;
        }

        public override string ToString()
        {
            return $"{Id} - '{Tag}' - {Address}";
        }
    }

    public class BaseTransaction : ITransaction
    {
        public string From { get; }
        public string To { get; }
        public UInt64 Amount { get; }
        public int Confirmations { get; }
    }
}
