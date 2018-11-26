﻿using System;
using System.Collections.Generic;
using System.Linq;
using DictionaryObject = System.Collections.Generic.Dictionary<string, object>;

namespace WavesCS
{
    public class Node
    {
        public const string TestNetHost = "https://testnodes.wavesnodes.com";
        public const string MainNetHost = "https://nodes.wavesnodes.com";

        public const char TestNetChainId = 'T';
        public const char MainNetChainId = 'W';

        private readonly string _host;
        public readonly char ChainId;

        public static Node DefaultNode { get; private set; }
        private Dictionary<string, Asset> AssetsCache;

        public Node(string nodeHost = TestNetHost, char nodeChainId = TestNetChainId)
        {
            if (nodeHost.EndsWith("/", StringComparison.InvariantCulture))
                nodeHost = nodeHost.Substring(0, nodeHost.Length - 1);

            _host = nodeHost;

            if (_host == TestNetHost)
                ChainId = TestNetChainId;
            else if (_host == MainNetHost)
                ChainId = MainNetChainId;
            else
                ChainId = nodeChainId;

            if (DefaultNode == null)
                DefaultNode = this;

            AssetsCache = new Dictionary<string, Asset>();
        }

        public DictionaryObject GetObject(string url, params object[] args)
        {
            return Http.GetObject($"{_host}/{url}", args);
        }

        public IEnumerable<DictionaryObject> GetObjects(string url, params object[] args)
        {
            return Http.GetObjects($"{_host}/{url}", args);
        }

        public int GetHeight()
        {
            return GetObject("blocks/height").GetInt("height");
        }

        public decimal GetBalance(string address)
        {
            return GetObject($"addresses/balance/{address}").GetDecimal("balance", Assets.WAVES);
        }

        public decimal GetBalance(string address, int confirmations)
        {
            return GetObject($"addresses/balance/{address}/{confirmations}").GetDecimal("balance", Assets.WAVES);
        }

        public decimal GetBalance(string address, Asset asset)
        {
            if (asset == Assets.WAVES)
                return GetBalance(address);
            else
                return GetObject($"assets/balance/{address}/{asset.Id}").GetDecimal("balance", asset);
        }
        
        public Dictionary<Asset, decimal> GetAssetBalances(string address)
        {
            return GetObject($"assets/balance/{address}")
                .GetObjects("balances")
                .Select(o =>
                {
                    var asset = GetAsset(o.GetString("assetId"));
                    return new
                    {
                        Asset = asset,
                        Balance = o.GetDecimal("balance", asset)
                    };
                })
                .ToDictionary(o => o.Asset, o => o.Balance);
        }

        public int GetUnconfirmedPoolSize()
        {
            return GetObject("transactions/unconfirmed/size").GetInt("size");
        }

        static public object DataValue(DictionaryObject o)
        {
            switch (o.GetString("type"))
            {
                case "string": return (object)o.GetString("value");
                case "binary": return (object)o.GetString("value").FromBase64();
                case "integer": return (object)o.GetLong("value");
                case "boolean": return (object)o.GetBool("value");
                default: throw new Exception("Unknown value type");
            }
        }

        public DictionaryObject GetAddressData(string address)
        {
            return GetObjects("addresses/data/{0}", address)
                .ToDictionary(o => o.GetString("key"), DataValue);
        }

        public Asset GetAsset(string assetId)
        {
            if (assetId == Assets.WAVES.Id)
                return Assets.WAVES;
            
            Asset asset = null;

            if (AssetsCache.ContainsKey(assetId))
                asset = AssetsCache[assetId];
            else
            {
                var tx = GetObject($"transactions/info/{assetId}");
                if ((TransactionType)tx.GetByte("type") != TransactionType.Issue)
                    throw new ArgumentException("Wrong asset id (transaction type)");

                var assetDetails = GetObject($"assets/details/{assetId}?full=true");
                var scripted = assetDetails.GetBool("scripted");

                var script = scripted ? assetDetails.GetString("scriptDetails.script").FromBase64() : null;
                asset = new Asset(assetId, tx.GetString("name"), tx.GetByte("decimals"), script);
                AssetsCache[assetId] = asset;
            }

            return asset;
        }

        public Transaction[] GetTransactions(string address, int limit = 100)
        {
            return GetTransactionsByAddress(address, limit)
                .Select(tx => { tx["chainId"] = ChainId; return tx; })
                .Select(Transaction.FromJson)
                .ToArray();
        }

        public TransactionType TransactionTypeId(Type transactionType)
        {
            switch (transactionType.Name)
            {
                case nameof(IssueTransaction): return TransactionType.Issue;
                case nameof(TransferTransaction): return TransactionType.Transfer;
                case nameof(ReissueTransaction): return TransactionType.Reissue;
                case nameof(BurnTransaction): return TransactionType.Burn;
                case nameof(ExchangeTransaction): return TransactionType.Exchange;
                case nameof(LeaseTransaction): return TransactionType.Lease;
                case nameof(CancelLeasingTransaction): return TransactionType.LeaseCancel;
                case nameof(AliasTransaction): return TransactionType.Alias;
                case nameof(MassTransferTransaction): return TransactionType.MassTransfer;
                case nameof(DataTransaction): return TransactionType.DataTx;
                case nameof(SetScriptTransaction): return TransactionType.SetScript;
                case nameof(SponsoredFeeTransaction): return TransactionType.SponsoredFee;
                default: throw new Exception("Unknown transaction type");
            }
        }

        public T[] GetTransactions<T>(string address, int limit = 100) where T : Transaction
        {
            var typeId = TransactionTypeId(typeof(T));

            return GetTransactionsByAddress(address, limit)
                .Where(tx => (TransactionType)tx.GetByte("type") == typeId)
                .Select(tx => { tx["chainId"] = ChainId; return tx; })
                .Select(Transaction.FromJson)
                .Cast<T>()
                .ToArray();
        }


        public Transaction GetTransactionById(string transactionId)
        {
            var tx = Http.GetJson($"{_host}/transactions/info/{transactionId}")
                         .ParseJsonObject();

            tx["chainId"] = ChainId;

            return Transaction.FromJson(tx);
        }

        public string Transfer(PrivateKeyAccount sender, string recipient, Asset asset, decimal amount,
            string message = "")
        {
            var fee = 0.001m + (asset.Script != null ?  0.004m : 0) ;
            var tx = new TransferTransaction(sender.PublicKey, recipient, asset, amount, message);
            tx.Sign(sender);
            return Broadcast(tx);
        }

        public string Transfer(PrivateKeyAccount sender, string recipient, Asset asset, decimal amount,
                               decimal fee, Asset feeAsset = null, byte[] message = null)
        {
            var tx = new TransferTransaction(sender.PublicKey, recipient, asset, amount, fee, feeAsset, message);           
            tx.Sign(sender);
            return Broadcast(tx);
        }

        public string MassTransfer(PrivateKeyAccount sender, Asset asset, IEnumerable<MassTransferItem> transfers,
            string message = "")
        {
            var tx = new MassTransferTransaction(sender.PublicKey, asset, transfers, message);
            tx.Sign(sender);
            return Broadcast(tx);
        }

        public string Lease(PrivateKeyAccount sender, string recipient, decimal amount)
        {
            var tx = new LeaseTransaction(sender.PublicKey, recipient, amount);
            tx.Sign(sender);
            return Broadcast(tx);
        }
       
        public string CancelLease(PrivateKeyAccount account, string transactionId)
        {
            var tx = new CancelLeasingTransaction(account.PublicKey, transactionId);
            tx.Sign(account);
            return Broadcast(tx);
        }

        public Asset IssueAsset(PrivateKeyAccount account,
            string name, string description, decimal quantity, byte decimals, bool reissuable, byte[] script = null)
        {
            var tx = new IssueTransaction(account.PublicKey, name, description, quantity, decimals, reissuable, ChainId, 1, script);
            tx.Sign(account);
            var response = Broadcast(tx);
            var assetId = response.ParseJsonObject().GetString("id");
            return new Asset(assetId, name, decimals, script);
        }

        public string ReissueAsset(PrivateKeyAccount account, Asset asset, decimal quantity, bool reissuable)
        {
            var tx = new ReissueTransaction(account.PublicKey, asset, quantity, reissuable);
            tx.Sign(account);
            return Broadcast(tx);
        }

        public string BurnAsset(PrivateKeyAccount account, Asset asset, decimal amount)
        {
            var tx = new BurnTransaction(account.PublicKey, asset, amount).Sign(account);
            tx.Sign(account);
            return Broadcast(tx);
        }

        public string CreateAlias(PrivateKeyAccount account, string alias, char chainId)
        {
            var tx = new AliasTransaction(account.PublicKey, alias, chainId);
            tx.Sign(account);
            return Broadcast(tx);
        }

        public string SponsoredFeeForAsset(PrivateKeyAccount account, Asset asset, decimal minimalFeeInAssets)
        {
            var tx = new SponsoredFeeTransaction(account.PublicKey, asset, minimalFeeInAssets);
            tx.Sign(account);
            return Broadcast(tx);
        }

        public string PutData(PrivateKeyAccount account, DictionaryObject entries)
        {
            var tx = new DataTransaction(account.PublicKey, entries);
            tx.Sign(account);
            return Broadcast(tx);
        }

        public byte[] CompileScript(string script)
        {
            return Post("/utils/script/compile", script).ParseJsonObject().Get<string>("script").FromBase64();
        }

        public string Post(string url, string data)
        {
            return Http.Post(_host + url, data);
        }
        
        public string Broadcast(Transaction transaction)
        {
            return Http.Post($"{_host}/transactions/broadcast", transaction.GetJsonWithSignature());
        }
        
        public string Broadcast(DictionaryObject transaction)
        {
            return Http.Post($"{_host}/transactions/broadcast", transaction);
        }

        public string BatchBroadcast(IEnumerable<Transaction> transactions)
        {
            var data = transactions.Select(t => t.GetJsonWithSignature()).ToArray();
            return Http.Post($"{_host}/assets/broadcast/batch-transfer", data);
        }

        public DictionaryObject[] GetTransactionsByAddress(string address, int limit)
        {
            return Http.GetFlatObjects($"{_host}/transactions/address/{address}/limit/{limit}");
        }
    }
}
