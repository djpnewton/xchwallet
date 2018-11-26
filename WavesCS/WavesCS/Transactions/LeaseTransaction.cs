﻿using System.IO;
using DictionaryObject = System.Collections.Generic.Dictionary<string, object>;

namespace WavesCS
{
    public class LeaseTransaction : Transaction
    {
        public string Recipient { get; }
        public decimal Amount { get; }
        public bool IsActive { get; }

        public LeaseTransaction(byte[] senderPublicKey, string recipient, decimal amount, decimal fee = 0.001m) : 
            base(senderPublicKey)
        {
            Recipient = recipient;
            Amount = amount;
            Fee = fee;
        }

        public LeaseTransaction(DictionaryObject tx) : base(tx)
        {
            Recipient = tx.GetString("recipient");
            Amount = Assets.WAVES.LongToAmount(tx.GetLong("amount"));
            Fee = Assets.WAVES.LongToAmount(tx.GetLong("fee"));
            IsActive = tx.GetString("status") == "active";
        }

        public override byte[] GetBody()
        {
            
            using(var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(TransactionType.Lease);
                writer.Write(SenderPublicKey);
                writer.Write(Recipient.FromBase58());
                writer.WriteLong(Assets.WAVES.AmountToLong(Amount));
                writer.WriteLong(Assets.WAVES.AmountToLong(Fee));
                writer.WriteLong(Timestamp.ToLong());
                return stream.ToArray();
            }
        }

        public override byte[] GetIdBytes()
        {
            return GetBody();
        }

        public override DictionaryObject GetJson()
        {
            return new DictionaryObject {
                {"type", (byte) TransactionType.Lease},
                {"senderPublicKey", SenderPublicKey.ToBase58()},                
                {"sender", Sender},
                {"recipient", Recipient},
                {"amount", Assets.WAVES.AmountToLong(Amount)},
                {"fee", Assets.WAVES.AmountToLong(Fee)},
                {"timestamp", Timestamp.ToLong()}                
            };        
        }

        protected override bool SupportsProofs()
        {
            return false;
        }
    }
}