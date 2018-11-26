﻿using System;
using System.IO;
using System.Text;
using WavesCS;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace WavesCSTests
{
    [TestClass]
    public class TransactionTest
    {
        private static readonly decimal Amount = 105m;
        private static readonly decimal Fee = 0.001m;

        private static readonly JsonSerializer serializer = new JsonSerializer();
        
        public TestContext TestContext { get; set; }
        

        [TestMethod]
        public void TestSmoke()
        {
            // doesn't validate transactions, just checks that all methods run to completion, no buffer overflows occur etc
            var account = PrivateKeyAccount.CreateFromPrivateKey("CMLwxbMZJMztyTJ6Zkos66cgU7DybfFJfyJtTVpme54t", AddressEncoding.TestNet);
            var recipient = "3N9gDFq8tKFhBDBTQxR3zqvtpXjw5wW3syA";
            var asset = Assets.MONERO;
            var transactionId = "TransactionTransactionTransactio";

            var recipients = new List<MassTransferItem>
            {
                new MassTransferItem(recipient, Amount),
                new MassTransferItem(recipient, Amount)
            };

            Dump("alias", new AliasTransaction(account.PublicKey, "daphnie", AddressEncoding.TestNet, Fee));
            Dump("burn", new BurnTransaction(account.PublicKey, asset, Amount, Fee));
            Dump("issue", new IssueTransaction(account.PublicKey, "Pure Gold", "Gold backed asset", Amount, 8, true, 'T', Fee));
            Dump("reissue", new ReissueTransaction(account.PublicKey, asset, Amount, false, Fee));
            Dump("lease", new LeaseTransaction(account.PublicKey, recipient, Amount, Fee));
            Dump("lease cancel", new CancelLeasingTransaction(account.PublicKey, transactionId, Fee));
            Dump("xfer", new TransferTransaction(account.PublicKey, recipient, asset, Amount, "Shut up & take my money"));
            Dump("massxfer", new MassTransferTransaction(account.PublicKey, asset, recipients, "Shut up & take my money", Fee));
        }

        private void Dump(String header, Transaction transaction)
        {
            TestContext.WriteLine("*** " + header + " ***");

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            serializer.Serialize(sw, transaction);
            var json = sb.ToString();
            TestContext.WriteLine("Transaction data: " + json);

            TestContext.WriteLine("");
        }
    }
}

