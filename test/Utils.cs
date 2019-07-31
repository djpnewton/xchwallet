using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage;
using RestSharp;
using Newtonsoft.Json;
using NBitcoin;
using NBXplorer.Models;

using xchwallet;

namespace test
{
    public static class Utils
    {
        public static IRestResponse SmartBtcComAuPushTx(bool mainnet, string txHex)
        {
            var url = "https://api.smartbit.com.au/v1/blockchain";
            if (!mainnet)
                url = "https://testnet-api.smartbit.com.au/v1/blockchain";
            var client = new RestClient(url);
            var req = new RestRequest("/pushtx", DataFormat.Json);
            var body = string.Format("{{\"hex\": \"{0}\"}}", txHex);
            req.AddJsonBody(body);
            return client.Post(req);
        }

        public static IRestResponse SmartBtcComAuRequest(bool mainnet, string endpoint)
        {
            var url = "https://api.smartbit.com.au/v1/blockchain";
            if (!mainnet)
                url = "https://testnet-api.smartbit.com.au/v1/blockchain";
            var client = new RestClient(url);
            var req = new RestRequest(endpoint, DataFormat.Json);
            return client.Get(req);
        }

        public static IRestResponse BtcApsComRequest(bool mainnet, string endpoint)
        {
            var url = "https://api.bitaps.com/btc/v1/blockchain";
            if (!mainnet)
                url = "https://api.bitaps.com/btc/testnet/v1/blockchain";
            var client = new RestClient(url);
            var req = new RestRequest(endpoint, DataFormat.Json);
            return client.Get(req);
        }

        public static Dictionary<string, List<string>> GetTxIds(bool mainnet, IEnumerable<WalletAddr> addrs)
        {
            var txids = new Dictionary<string, List<string>>();
            foreach (var addr in addrs)
            {
                var addrTxids = new List<string>();
                var page = 1;
                var limit = 50;
                while (true)
                {
                    var resp = Utils.BtcApsComRequest(mainnet, $"/address/transactions/{addr.Address}?mode=brief&page={page}&limit={limit}");
                    if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                        return null;
                    }
                    dynamic res = JsonConvert.DeserializeObject(resp.Content);
                    foreach (var tx in res.data.list)
                        addrTxids.Add((string)(tx.txId));
                    page++;
                    if (page > (int)res.data.pages)
                        break;
                    System.Threading.Thread.Sleep(500);
                }
                txids[addr.Address] = addrTxids;
                Console.Write($"+{addrTxids.Count} txids..");
            }
            Console.WriteLine();
            return txids;
        }

        public static bool ScanBtcTxs(BtcWallet wallet)
        {
            var txids = GetTxIds(wallet.IsMainnet(), wallet.GetAddresses());
            var count = 0;
            var txsScan = new List<TxScan>();
            foreach (var kvPair in txids)
            {
                foreach (var txid in kvPair.Value)
                {
                    var resp = Utils.BtcApsComRequest(wallet.IsMainnet(), $"/transaction/{txid}");
                    if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                        return false;
                    }
                    dynamic res = JsonConvert.DeserializeObject(resp.Content);
                    txsScan.Add(new TxScan { TxId = txid, BlockHash = res.data.blockHash });
                    Console.WriteLine($"{count++} {txid} {res.data.blockHash}");
                    System.Threading.Thread.Sleep(500);
                }
            }
            wallet.RescanNBXplorer(txsScan);
            return true;
        }

        public static IDbContextTransaction RecreateBtcTxs(BtcWallet wallet)
        {
            var resp = Utils.BtcApsComRequest(wallet.IsMainnet(), $"/block/last");
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                return null;
            }
            dynamic res = JsonConvert.DeserializeObject(resp.Content);
            var currentHeight = (int)res.data.block.height;

            var utxos = new Dictionary<string, List<UTXO>>();
            var outgoingTxs = new Dictionary<string, OutgoingTx>();
            var txids = GetTxIds(wallet.IsMainnet(), wallet.GetAddresses());
            foreach (var kvPair in txids)
            {
                foreach (var txid in kvPair.Value)
                {
                    resp = Utils.BtcApsComRequest(wallet.IsMainnet(), $"/transaction/{txid}");
                    if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                        return null;
                    }
                    res = JsonConvert.DeserializeObject(resp.Content);

                    var tx = Transaction.Parse((string)res.data.rawTx, wallet.GetNetwork());
                    var fee = (long)res.data.fee;
                    utxos[txid] = new List<UTXO>();
                    var spends = new List<CoinSpend>();
                    var outputs = new List<CoinOutput>();

                    uint N = 0;
                    foreach (var i in res.data.vIn)
                    {
                        var from = (string)i.Value.address;
                        var addr = wallet.GetAddresses().Where(a => a.Address == from).SingleOrDefault();
                        var amount = new Money((long)i.Value.amount);
                        var scriptPubKey = Script.FromHex((string)i.Value.scriptPubKey);
                        var coin = new Coin(uint256.Parse(txid), N, amount, scriptPubKey);
                        var spend = new CoinSpend(from, addr, coin, null);
                        spends.Add(spend);
                        N++;
                    }
                    N = 0;
                    foreach (var o in res.data.vOut)
                    {
                        var amount = new Money((long)o.Value.value);
                        var scriptPubKey = Script.FromHex((string)o.Value.scriptPubKey);
                        var coin = new Coin(uint256.Parse(txid), N, amount, scriptPubKey);
                        var utxo = new UTXO(coin);
                        utxo.Confirmations = (int)res.data.confirmations;
                        utxo.Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)res.data.time);
                        utxos[txid].Add(utxo);
                        var coinOutput = new CoinOutput((string)o.Value.address, amount.Satoshi);
                        outputs.Add(coinOutput);
                        N++;
                    }
                    outgoingTxs[txid] = new OutgoingTx(tx, spends, outputs, fee);

                    Console.WriteLine($"{txid}, {spends.Count} inputs, {utxos[txid].Count} outputs");
                    System.Threading.Thread.Sleep(500);
                }
            }

            var dbtx = wallet.BeginDbTransaction();
            wallet.RecreateTxs(dbtx, utxos, outgoingTxs, currentHeight);
            return dbtx;
        }

        public static bool CheckWithBtcBlockExplorer(BtcWallet wallet)
        {
            var count = 0;
            BigInteger balance = 0;
            foreach (var addr in wallet.GetAddresses())
            {
                var resp = Utils.BtcApsComRequest(wallet.IsMainnet(), $"/address/state/{addr.Address}");
                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                    return false;
                }
                dynamic res = JsonConvert.DeserializeObject(resp.Content);
                balance += (long)res.data.balance + (long)res.data.pendingReceivedAmount - (long)res.data.pendingSentAmount;
                Console.WriteLine($"{count++} - {resp.ResponseUri}");
                System.Threading.Thread.Sleep(500);
            }
            Console.WriteLine($"::block explorer balance: {balance}, ({wallet.AmountToString(balance)} {wallet.Type()})\n");
            return true;
        }

    }
}
