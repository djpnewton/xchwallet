using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using xchwallet;
using Newtonsoft.Json;
using NBitcoin;
using WavesCS;

namespace test
{
    public static class LoadTest
    {
        public static (string, bool, List<string>) SendBtcFunds(bool mainnet, string privKey, string addr1, string addr2)
        {
            var sentTxIds = new List<string>();
            Console.WriteLine($"::send btc funds");
            var network = Network.TestNet;
            var privKeyBytes = Enumerable.Range(0, privKey.Length / 2).Select(x => Convert.ToByte(privKey.Substring(x * 2, 2), 16)).ToArray();
            var key = new Key(privKeyBytes);
            var secret = key.GetBitcoinSecret(network);
            var addr = secret.PubKey.GetAddress(ScriptPubKeyType.Legacy, network);
            var addrStr = addr.ToString();
            Console.WriteLine($"  ::privkey: {privKey}");
            Console.WriteLine($"  ::addr:    {addr}");
            var resp = Utils.SmartBtcComAuRequest(mainnet, $"/address/{addr}");
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                return (addrStr, false, sentTxIds);
            }
            dynamic res = JsonConvert.DeserializeObject(resp.Content);
            var balance = (long)res.address.total.balance_int;
            Console.WriteLine($"  ::balance: {balance} sats");

            if (balance > 0)
            {
                // find utxos from address transactions
                var coins = new List<Coin>();
                long coinsAmount = 0;
                while (true)
                {
                    foreach (var tx in res.address.transactions)
                    {
                        var txHash = new uint256(tx.txid.Value);
                        foreach (var output in tx.outputs)
                        {
                            var hasAddr = false;
                            foreach (var outaddr in output.addresses)
                                if (outaddr == addr)
                                    hasAddr = true;
                            if (hasAddr && output.spend_txid == null)
                            {
                                var value = (long)output.value_int;
                                var n = (uint)output.n;
                                var outpoint = new OutPoint(txHash, n);
                                coins.Add(new Coin(txHash, n, new Money(value), addr.ScriptPubKey));
                                Console.WriteLine($"    + {tx.txid} we have {value} sats unspent");
                                coinsAmount += value;
                            }
                        }
                    }
                    if (coinsAmount >= balance)
                        break;
                    if (res.address.transaction_paging.next != null)
                    {
                        resp = Utils.BtcApsComRequest(mainnet, $"/address/{addr}?next={res.address.transaction_paging.next}");
                        if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                            return (addrStr, false, sentTxIds);
                        }
                        res = JsonConvert.DeserializeObject(resp.Content);
                    }
                    else
                        break;
                }
                if (coinsAmount == 0)
                {
                    Console.WriteLine("  no balance (coinsAmount == 0).. skipping");
                    return (addrStr, false, sentTxIds);
                }
                // create transaction
                var minSatPerByte = 1;
                var absoluteFee = 100;
                var transaction = NBitcoin.Transaction.Create(network);
                long totalIn = 0;
                while (true)
                {
                    // reset transaction
                    transaction.Inputs.Clear();
                    transaction.Outputs.Clear();
                    totalIn = 0;
                    // add inputs
                    foreach (var coin in coins)
                    {
                        totalIn += coin.Amount.Satoshi;
                        transaction.Inputs.Add(new TxIn()
                        {
                            PrevOut = coin.Outpoint,
                            ScriptSig = coin.TxOut.ScriptPubKey,
                        });
                    }
                    // add outputs
                    var outputAmounts = (totalIn - absoluteFee) / 3;
                    var outputTargets = new string[] { addr1, addr2, addr1 };
                    foreach (var target in outputTargets)
                    {
                        var targetAddr = BitcoinAddress.Create(target, network);
                        var txout = new TxOut(new Money(outputAmounts, MoneyUnit.Satoshi), targetAddr);
                        transaction.Outputs.Add(txout);
                    }
                    var message = "Long live NBitcoin and its makers!";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(message);
                    transaction.Outputs.Add(new TxOut()
                    {
                        Value = Money.Zero,
                        ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
                    });
                    // sign transaction
                    transaction.Sign(secret, coins.ToArray());
                    // check fee rate
                    if (transaction.GetFeeRate(coins.ToArray()).SatoshiPerByte >= minSatPerByte)
                        break;
                    // increment absolute fee
                    absoluteFee += 100;
                }
                //Console.WriteLine(transaction);
                // print total in, fee rate and size
                Console.WriteLine($"  ::total in - {totalIn}");
                var feeRate = transaction.GetFeeRate(coins.ToArray());
                var size = transaction.GetSerializedSize();
                var vsize = transaction.GetVirtualSize();
                Console.WriteLine($"  ::feerate - {feeRate.SatoshiPerByte} sats/byte\n  ::size    - {size} bytes\n  ::vsize   - {vsize} bytes");
                // broadcast transaction
                Console.WriteLine($"  ::broadcast tx - {transaction.GetHash()}");
                var txhex = transaction.ToHex();
                resp = Utils.SmartBtcComAuPushTx(mainnet, txhex);
                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"Error: http status code {resp.StatusCode} ({resp.ResponseUri})");
                    Console.WriteLine($"  {resp.Content}");
                    return (addrStr, false, sentTxIds);
                }
                Console.WriteLine($"  {resp.Content}");
                sentTxIds.Add(transaction.GetHash().ToString());
            }
            return (addrStr, true, sentTxIds);
        }

        public static (string, bool, List<string>) SendWavesFunds(bool mainnet, string privKey, string addr1, string addr2)
        {
            var sentTxIds = new List<string>();
            Console.WriteLine($"::send waves funds");
            var chainId = Node.TestNetChainId;
            var nodeAddr = "https://testnodes.wavesnodes.com";
            if (mainnet)
            {
                chainId = Node.MainNetChainId;
                nodeAddr = "https://nodes.wavesnodes.com/";
            }
            var node = new Node(nodeAddr, chainId);
            var seed = xchwallet.Utils.ParseHexString(privKey);
            var key = PrivateKeyAccount.CreateFromSeed(seed, chainId, 0);
            var addr = key.Address;
            Console.WriteLine($"  ::privkey: {privKey}");
            Console.WriteLine($"  ::addr:    {addr}");
            var balance = node.GetBalance(addr, Assets.WAVES);
            Console.WriteLine($"  ::balance: {balance} waves");
            if (balance > 0)
            {
                var fee = 0.001M;
                var massTxFee = fee + 0.0005M * 2;
                var amount = (balance - fee - massTxFee) / 3;
                var tx = new TransferTransaction(chainId, key.PublicKey, addr1, Assets.WAVES, amount, fee);
                tx.Sign(key);
                var transfers = new List<MassTransferItem>
                {
                    new MassTransferItem(addr1, amount),
                    new MassTransferItem(addr2, amount),
                };
                var massTx = new MassTransferTransaction(chainId, key.PublicKey, Assets.WAVES, transfers, "Shut up & take my money");
                System.Diagnostics.Debug.Assert(massTx.Fee == massTxFee);
                massTx.Sign(key);
                // send the raw signed transactions and get the txids
                try
                {
                    var output = node.BroadcastAndWait(tx);
                    Console.WriteLine($"  {output}");
                    sentTxIds.Add(tx.GenerateId());
                    output = node.BroadcastAndWait(massTx);
                    Console.WriteLine($"  {output}");
                    sentTxIds.Add(tx.GenerateId());
                }
                catch (System.Net.WebException ex)
                {
                    Console.WriteLine($"  ERROR: {ex}");
                    return (addr, false, sentTxIds);
                }
            }
            return (addr, true, sentTxIds);
        }
    }
}
