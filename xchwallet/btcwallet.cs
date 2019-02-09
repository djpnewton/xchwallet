using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace xchwallet
{
    public class BtcWallet : BaseWallet
    {
        public const string TYPE = "BTC";
        protected override string _type()
        {
            return TYPE;
        }

        readonly BitcoinExtKey key = null;
        readonly ExplorerClient client = null;
        readonly DirectDerivationStrategy pubkey = null;

        public BtcWallet(ILogger logger, WalletContext db, bool mainnet, Uri nbxplorerAddress, bool useLegacyAddrs=false) : base(logger, db, mainnet)
        {
            this.logger = logger;

            // create extended key
            var network = mainnet ? Network.Main : Network.TestNet;
            key = new BitcoinExtKey(new ExtKey(seedHex), network);
            var strpubkey = $"{key.Neuter().ToString()}";
            if (useLegacyAddrs)
                strpubkey = strpubkey + "-[legacy]";
            pubkey = (DirectDerivationStrategy)new DerivationStrategyFactory(network).Parse(strpubkey);
            // create NBXplorer client
            NBXplorerNetwork nbxnetwork;
            if (mainnet)
                nbxnetwork = new NBXplorerNetworkProvider(NetworkType.Mainnet).GetFromCryptoCode("BTC");
            else
                nbxnetwork = new NBXplorerNetworkProvider(NetworkType.Testnet).GetFromCryptoCode("BTC");
            client = new ExplorerClient(nbxnetwork, nbxplorerAddress);
            client.Track(pubkey);
        }

        public override bool IsMainnet()
        {
            return mainnet;
        }

        public override WalletAddr NewAddress(string tag)
        {
            // create new address that is unused
            var _tag = db.TagGetOrCreate(tag);
            var keypathInfo = client.GetUnused(pubkey, DerivationFeature.Deposit, reserve: true);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            var address = new WalletAddr(_tag, keypathInfo.KeyPath.ToString(), 0, addr.ToString());
            // add to address list
            db.WalletAddrs.Add(address);
            return address;
        }

        private BitcoinAddress AddressOf(ExtPubKey pubkey, KeyPath path)
		{
			return pubkey.Derive(path).PubKey.Hash.GetAddress(client.Network.NBitcoinNetwork);
        }

        private void processUtxo(NBXplorer.Models.UTXO utxo, int currentHeight, bool confirmed)
        {
            //var addr = AddressOf(pubkey.Root, utxo.KeyPath);
            var to = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            var id = utxo.Outpoint.Hash.ToString();
            var date = utxo.Timestamp.ToUnixTimeSeconds();
            var height = confirmed ? currentHeight - utxo.Confirmations : -1;

            logger.LogInformation($"processing UTXO - txid {id} - destination addr {to}");

            var address = db.AddrGet(to.ToString());
            if (address == null)
            {
                logger.LogError($"Address not found for {to}");
                return;
            }

            var ctx = db.ChainTxGet(id);
            if (ctx == null)
            {
                ctx = new ChainTx(id, date, "", to.ToString(),
                    utxo.Value.Satoshi, -1, height, utxo.Confirmations);
                db.ChainTxs.Add(ctx);
            }
            else
            {
                ctx.Height = height;
                ctx.Confirmations = utxo.Confirmations;
                db.ChainTxs.Update(ctx);
            }
            var wtx = db.TxGet(address, ctx);
            if (wtx == null)
            {
                wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Incomming };
                db.WalletTxs.Add(wtx);
            }
        }

        public override void UpdateFromBlockchain()
        {
            UpdateTxs();
        }

        private void UpdateTxs()
        {
            var utxos = client.GetUTXOs(pubkey);
            foreach (var item in utxos.Unconfirmed.UTXOs)
                processUtxo(item, utxos.CurrentHeight, false);
            foreach (var item in utxos.Confirmed.UTXOs)
                processUtxo(item, utxos.CurrentHeight, true);
        }

        public override IEnumerable<WalletTx> GetTransactions(string tag)
        {
            return db.TxsGet(tag);
        }

        public override IEnumerable<WalletTx> GetAddrTransactions(string address)
        {
            var addr = db.AddrGet(address);
            System.Diagnostics.Debug.Assert(addr != null);
            return addr.Txs;
        }

        public override BigInteger GetBalance(string tag)
        {
            BigInteger total = 0;
            foreach (var addr in db.AddrsGet(tag))
                total += GetAddrBalance(addr);
            return total;
        }

        public override BigInteger GetAddrBalance(string address)
        {
            var addr = db.AddrGet(address);
            System.Diagnostics.Debug.Assert(addr != null);
            return GetAddrBalance(addr);
        }

        public BigInteger GetAddrBalance(WalletAddr addr)
        {
            BigInteger total = 0;
            foreach (var tx in addr.Txs)
                if (tx.Direction == WalletDirection.Incomming)
                    total += tx.ChainTx.Amount;
                else if (tx.Direction == WalletDirection.Outgoing)
                {
                    total -= tx.ChainTx.Amount;
                    total -= tx.ChainTx.Fee;
                }
            return total;
        }

        public BitcoinAddress AddChangeAddress(string tag)
        {
            // create new address that is unused
            var keypathInfo = client.GetUnused(pubkey, DerivationFeature.Change, reserve: false);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            var address = new WalletAddr(db.TagGetOrCreate(tag), keypathInfo.KeyPath.ToString(), 0, addr.ToString());
            // add to address list
            db.WalletAddrs.Add(address);
            return addr;
        }

        void AddOutgoingTx(string txid, WalletAddr from, string to, BigInteger amount, BigInteger fee)
        {
            logger.LogDebug("outgoing tx: amount: {0}, fee: {1}", amount, fee);
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ctx = new ChainTx(txid, date, from.Address, to, amount, fee, -1, 0);
            db.ChainTxs.Add(ctx);
            var wtx = new WalletTx{ChainTx=ctx, Address=from, Direction=WalletDirection.Outgoing};
            db.WalletTxs.Add(wtx);
        }

        FeeRate GetFeeRate(Transaction tx, List<Tuple<WalletAddr, Coin, Key>> toBeSpent)
        {
            // sign tx before calculating fee so it includes signatures
            var coins = from a in toBeSpent select a.Item2;
            var keys = from a in toBeSpent select a.Item3;
            tx.Sign(keys.ToArray(), coins.ToArray());
            return tx.GetFeeRate(coins.ToArray());
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids)
        {
            txids = new List<string>();
            // create tx template with destination as first output
            var tx = Transaction.Create(client.Network.NBitcoinNetwork);
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to, client.Network.NBitcoinNetwork);
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on UTXOs from the selected tag
            var addrs = GetAddresses(tag);
            var candidates = new List<Tuple<WalletAddr, Coin>>();
            var utxos = client.GetUTXOs(pubkey);
            foreach (var utxo in utxos.Confirmed.UTXOs)
            {
                var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork).ToString();
                foreach (var addr in addrs)
                    if (addrStr == addr.Address)
                    {
                        candidates.Add(new Tuple<WalletAddr, Coin>(addr, utxo.AsCoin()));
                        break;
                    }
            }
            // add inputs until we can satisfy our output
            BigInteger totalInput = 0;
            var toBeSpent = new List<Tuple<WalletAddr, Coin, Key>>();
            foreach (var candidate in candidates)
            {
                // add to list of coins and private keys to spend
                tx.Inputs.Add(new TxIn(candidate.Item2.Outpoint));
                totalInput += candidate.Item2.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Item1.Path)).PrivateKey;
                toBeSpent.Add(new Tuple<WalletAddr, Coin, Key>(candidate.Item1, candidate.Item2, privateKey));
                // check if we have enough inputs
                if (totalInput >= amount)
                    break;
                //TODO: take into account fees......
            }
            // check we have enough inputs
            if (totalInput < amount)
                return WalletError.InsufficientFunds;
            // check fee rate
            var feeRate = GetFeeRate(tx, toBeSpent);
            var currentSatsPerByte = feeRate.FeePerK / 1024;
            if (currentSatsPerByte > feeUnit)
            {
                // create a change address
                var changeAddress = AddChangeAddress(tagChange);
                // calculate the target fee
                var currentFee = feeRate.GetFee(tx.GetVirtualSize());
                var targetFee = tx.GetVirtualSize() * (long)feeUnit;
                var changeOutput = new TxOut(currentFee - targetFee, changeAddress);
                targetFee += output.GetSerializedSize() * (long)feeUnit;
                // add the change output
                changeOutput = tx.Outputs.Add(currentFee - targetFee, changeAddress);
            }
            var coins = from a in toBeSpent select a.Item2;
            var keys = from a in toBeSpent select a.Item3;
            // sign inputs (after adding a change output)
            tx.Sign(keys.ToArray(), coins.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(coins.ToArray());
            if (fee.Satoshi > feeMax)
                return WalletError.MaxFeeBreached;
            // choose from address (TODO: handle multiple from addresses)
            var fromAddr = toBeSpent.First().Item1;
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                AddOutgoingTx(tx.GetHash().ToString(), fromAddr, to, amount, fee.Satoshi);
                ((List<string>)txids).Add(tx.GetHash().ToString());
                return WalletError.Success;
            }
            else
            {
                logger.LogError("{0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
                return WalletError.FailedBroadcast;
            }
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids)
        {
            txids = new List<string>();
            // generate new address to send to
            var to = NewOrUnusedAddress(tagTo);
            // calc amount in tagFrom
            BigInteger amount = 0;
            foreach (var tag in tagFrom)
                amount += this.GetBalance(tag);
            // create tx template with destination as first output
            var tx = Transaction.Create(client.Network.NBitcoinNetwork);
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to.Address, client.Network.NBitcoinNetwork);
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on UTXOs from the selected tags
            var candidates = new List<Tuple<WalletAddr, Coin>>();
            var utxos = client.GetUTXOs(pubkey);
            foreach (var tag in tagFrom)
            {
                var addrs = GetAddresses(tag);
                foreach (var utxo in utxos.Confirmed.UTXOs)
                {
                    var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork).ToString();
                    foreach (var addr in addrs)
                        if (addrStr == addr.Address)
                        {
                            candidates.Add(new Tuple<WalletAddr, Coin>(addr, utxo.AsCoin()));
                            break;
                        }
                }
            }
            // add all inputs so we can satisfy our output
            BigInteger totalInput = 0;
            var toBeSpent = new List<Tuple<WalletAddr, Coin, Key>>();
            foreach (var candidate in candidates)
            {
                // add to list of coins and private keys to spend
                tx.Inputs.Add(new TxIn(candidate.Item2.Outpoint));
                totalInput += candidate.Item2.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Item1.Path)).PrivateKey;
                toBeSpent.Add(new Tuple<WalletAddr, Coin, Key>(candidate.Item1, candidate.Item2, privateKey));
            }
            // check we have enough inputs
            if (totalInput < amount)
                return WalletError.InsufficientFunds;
            // adjust fee rate by reducing the output incrementally
            var feeRate = new FeeRate(new Money(0));
            Money currentSatsPerByte = 0;
            while (currentSatsPerByte < feeUnit)
            {
                tx.Outputs[0].Value -= 1;
                amount -= 1;

                feeRate = GetFeeRate(tx, toBeSpent);
                currentSatsPerByte = feeRate.FeePerK / 1024;
            }
            // sign inputs
            var coins = from a in toBeSpent select a.Item2;
            var keys = from a in toBeSpent select a.Item3;
            tx.Sign(keys.ToArray(), coins.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(coins.ToArray());
            if (fee.Satoshi > feeMax)
                return WalletError.MaxFeeBreached;
            // choose from address (TODO: handle multiple from addresses)
            var fromAddr = toBeSpent.First().Item1;
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                AddOutgoingTx(tx.GetHash().ToString(), fromAddr, to.Address, amount, fee.Satoshi);
                ((List<string>)txids).Add(tx.GetHash().ToString());
                return WalletError.Success;
            }
            else
            {
                logger.LogError("{0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
                return WalletError.FailedBroadcast;
            }
        }

        public override bool ValidateAddress(string address)
        {
            try
            {
                var network = IsMainnet() ? Network.Main : Network.TestNet;
                return Network.Parse<BitcoinAddress>(address, network) != null;
            }
            catch
            {
                return false;
            }
        }

        public override string AmountToString(BigInteger value)
        {
            var _scale = new decimal(1, 0, 0, false, 8);
            var d = (decimal)value * _scale;
            return d.ToString();
        }

        public override BigInteger StringToAmount(string value)
        {
            var _scale = new decimal(1, 0, 0, false, 8);
            var d = decimal.Parse(value) / _scale;
            return (BigInteger)d;
        }
    }
}
