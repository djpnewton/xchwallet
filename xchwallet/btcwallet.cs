using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using NBitcoin;
using NBXplorer;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

namespace xchwallet
{
    public class TxScan
    {
        public string TxId { get; set; }
        public string BlockHash { get; set; }
    }

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

        Network GetNetwork()
        {
            return IsMainnet() ? Network.Main : Network.TestNet; 
        }

        public BtcWallet(ILogger logger, WalletContext db, bool mainnet, Uri nbxplorerAddress, bool useLegacyAddrs=false) : base(logger, db, mainnet)
        {
            this.logger = logger;

            // create extended key
            var network = GetNetwork();
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

        public override LedgerModel LedgerModel { get { return LedgerModel.UTXO; } }
        
        public override WalletAddr NewAddress(string tag)
        {
            // create new address that is unused
            var _tag = db.TagGet(tag);
            Util.WalletAssert(_tag != null, $"Tag '{tag}' does not exist");
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
            //TODO - add attachment
            // - read OP_RETURN ?
            
            //var addr = AddressOf(pubkey.Root, utxo.KeyPath);
            var to = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            var id = utxo.Outpoint.Hash.ToString();
            var date = utxo.Timestamp.ToUnixTimeSeconds();
            var height = confirmed ? currentHeight - (utxo.Confirmations-1) : -1;

            logger.LogInformation($"processing UTXO - txid {id} - destination addr {to} - value {utxo.Value}");

            var address = db.AddrGet(to.ToString());
            if (address == null)
            {
                logger.LogError($"Address not found for {to}");
                return;
            }

            // add/update chain tx
            var ctx = db.ChainTxGet(id);
            if (ctx == null)
            {
                ctx = new ChainTx(id, date, -1, height, utxo.Confirmations);
                db.ChainTxs.Add(ctx);
            }
            else
            {
                ctx.Height = height;
                ctx.Confirmations = utxo.Confirmations;
                db.ChainTxs.Update(ctx);
            }
            // add update
            var up = db.BalanceUpdateGet(id, true, utxo.Outpoint.N);
            if (up == null)
            {
                up = new BalanceUpdate(id, true, utxo.Outpoint.N, utxo.Value.Satoshi);
                up.ChainTx = ctx;
                up.WalletAddr = address;
                db.BalanceUpdates.Add(up);
            }
            // add/update wallet tx
            var wtx = db.TxGet(address, ctx);
            if (wtx == null)
            {
                wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Incomming };
                db.WalletTxs.Add(wtx);
                db.WalletTxAddMeta(wtx);
            }
        }

        public override IDbContextTransaction UpdateFromBlockchain()
        {
            var dbTransaction = db.Database.BeginTransaction();
            UpdateTxs(dbTransaction);
            UpdateTxConfirmations(pubkey);
            db.SaveChanges();
            return dbTransaction;
        }

        private void UpdateTxs(IDbContextTransaction dbTransaction)
        {
            var utxos = client.GetUTXOs(pubkey);
            foreach (var item in utxos.Unconfirmed.UTXOs)
            {
                processUtxo(item, utxos.CurrentHeight, false);
                db.SaveChanges();
            }
            foreach (var item in utxos.Confirmed.UTXOs)
            {
                processUtxo(item, utxos.CurrentHeight, true);
                db.SaveChanges();
            }
        }

        private void UpdateTxConfirmations(GetTransactionsResponse txs)
        {
            foreach (var tx in txs.ConfirmedTransactions.Transactions)
            {
                var ctx = db.ChainTxGet(tx.TransactionId.ToString());
                if (ctx != null && ctx.Confirmations != tx.Confirmations)
                {
                    ctx.Confirmations = tx.Confirmations;
                    db.ChainTxs.Update(ctx);
                }
            }
        }

        private void UpdateTxConfirmations(WalletAddr addr)
        {
            var baddr = BitcoinAddress.Create(addr.Address, GetNetwork());
            var trackedSource = TrackedSource.Create(baddr);
            var txs = client.GetTransactions(trackedSource);
            UpdateTxConfirmations(txs);
        }

        private void UpdateTxConfirmations(DerivationStrategyBase derivationStrat)
        {
            var txs = client.GetTransactions(derivationStrat);
            UpdateTxConfirmations(txs);
        }

        public override IEnumerable<WalletTx> GetTransactions(string tag)
        {
            return db.TxsGet(tag);
        }

        public override IEnumerable<WalletTx> GetAddrTransactions(string address)
        {
            var addr = db.AddrGet(address);
            Util.WalletAssert(addr != null, $"Address '{address}' does not exist");
            return addr.Txs;
        }

        public override BigInteger GetBalance(string tag, int minConfs=0)
        {
            BigInteger total = 0;
            foreach (var addr in db.AddrsGet(tag))
                total += GetAddrBalance(addr, minConfs);
            return total;
        }

        public override BigInteger GetAddrBalance(string address, int minConfs=0)
        {
            var addr = db.AddrGet(address);
            Util.WalletAssert(addr != null, $"Address '{address}' does not exist");
            return GetAddrBalance(addr, minConfs);
        }

        public BitcoinAddress AddChangeAddress(WalletTag tag)
        {
            // get new unused address
            var keypathInfo = client.GetUnused(pubkey, DerivationFeature.Change, reserve: false);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork);
            // check that address is not already present in the db (we used 'reserve: false')
            var address = db.AddrGet(addr.ToString());
            if (address != null)
                return addr;
            // add to address list
            address = new WalletAddr(tag, keypathInfo.KeyPath.ToString(), 0, addr.ToString());
            db.WalletAddrs.Add(address);
            return addr;
        }

        WalletTx AddOutgoingTx(string txid, List<Tuple<WalletAddr, Coin, Key>> spent, string to, BigInteger amount, BigInteger fee, WalletTxMeta meta)
        {
            logger.LogDebug("outgoing tx: amount: {0}, fee: {1}", amount, fee);
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ctx = new ChainTx(txid, date, fee, -1, 0);
            db.ChainTxs.Add(ctx);
            uint n = 0;
            foreach ((var addr, var coin, var _) in spent)
            {
                var up = new BalanceUpdate(txid, false, n, coin.Amount.Satoshi);
                up.ChainTx = ctx;
                up.WalletAddr = addr;
                db.BalanceUpdates.Add(up);
                n++;
            }
            var wtx = new WalletTx{ChainTx=ctx, Address=spent.First().Item1, Direction=WalletDirection.Outgoing};
            db.WalletTxs.Add(wtx);
            if (meta != null)
                wtx.Meta = meta;
            else
                db.WalletTxAddMeta(wtx);
            return wtx;
        }

        FeeRate GetFeeRate(Transaction tx, List<Tuple<WalletAddr, Coin, Key>> toBeSpent)
        {
            // sign tx before calculating fee so it includes signatures
            var coins = from a in toBeSpent select a.Item2;
            var keys = from a in toBeSpent select a.Item3;
            tx.Sign(keys.ToArray(), coins.ToArray());
            return tx.GetFeeRate(coins.ToArray());
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out WalletTx wtx, WalletTxMeta meta = null)
        {
            wtx = null;
            var tagChange_ = db.TagGet(tagChange);
            Util.WalletAssert(tagChange_ != null, $"Tag '{tagChange}' does not exist");
            // create tx template with destination as first output
            var tx = Transaction.Create(client.Network.NBitcoinNetwork);
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to, client.Network.NBitcoinNetwork);
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on UTXOs from the selected tag
            var addrs = GetAddresses(tag);
            var candidates = new List<Tuple<WalletAddr, Coin>>();
            var utxos = client.GetUTXOs(pubkey);
            foreach (var utxo in utxos.Unconfirmed.UTXOs)
            {
                var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork).ToString();
                var addr = addrs.Where(a => a.Address == addrStr).FirstOrDefault();
                if (addr != null)
                    candidates.Add(new Tuple<WalletAddr, Coin>(addr, utxo.AsCoin()));
            }
            foreach (var utxo in utxos.Confirmed.UTXOs)
            {
                var addrStr = utxo.ScriptPubKey.GetDestinationAddress(client.Network.NBitcoinNetwork).ToString();
                var addr = addrs.Where(a => a.Address == addrStr).FirstOrDefault();
                if (addr != null)
                    candidates.Add(new Tuple<WalletAddr, Coin>(addr, utxo.AsCoin()));
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
            logger.LogDebug($"totalInput {totalInput}, amount: {amount}");
            if (totalInput < amount)
                return WalletError.InsufficientFunds;
            // check fee rate
            var feeRate = GetFeeRate(tx, toBeSpent);
            var currentSatsPerByte = feeRate.FeePerK / 1024;
            if (currentSatsPerByte > feeUnit)
            {
                // create a change address
                var changeAddress = AddChangeAddress(tagChange_);
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
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                wtx = AddOutgoingTx(tx.GetHash().ToString(), toBeSpent, to, amount, fee.Satoshi, meta);
                return WalletError.Success;
            }
            else
            {
                logger.LogError("{0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
                return WalletError.FailedBroadcast;
            }
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, int minConfs=0)
        {
            wtxs = new List<WalletTx>();
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
                            if (minConfs > 0 && utxo.Confirmations < minConfs)
                                continue;
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
            var feeRate = new FeeRate(new Money(0L));
            decimal currentSatsPerByte = 0;
            while (currentSatsPerByte < (decimal)feeUnit)
            {
                tx.Outputs[0].Value -= 1L;
                amount -= 1;

                feeRate = GetFeeRate(tx, toBeSpent);
                currentSatsPerByte = feeRate.SatoshiPerByte;
            }
            // sign inputs
            var coins = from a in toBeSpent select a.Item2;
            var keys = from a in toBeSpent select a.Item3;
            tx.Sign(keys.ToArray(), coins.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(coins.ToArray());
            if (fee.Satoshi > feeMax)
                return WalletError.MaxFeeBreached;
            // broadcast transaction
            var result = client.Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                var wtx = AddOutgoingTx(tx.GetHash().ToString(), toBeSpent, to.Address, amount, fee.Satoshi, null);
                ((List<WalletTx>)wtxs).Add(wtx);
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

        public bool RescanNBXplorer(IEnumerable<TxScan> txs)
        {
            var req = new RescanRequest();
            foreach (var tx in txs)
                req.Transactions.Add(new RescanRequest.TransactionToRescan { TransactionId = uint256.Parse(tx.TxId), BlockId = uint256.Parse(tx.BlockHash) });
            client.Rescan(req);
            return true;
        }
        /*
        (int type, int num) ParseKeyPath(string keyPath)
        {
            var parts = keyPath.Split(new[] { '/' });
            System.Diagnostics.Debug.Assert(parts.Length == 2);
            var type = int.Parse(parts[0]);
            var num = int.Parse(parts[1]);
            System.Diagnostics.Debug.Assert(type == 0 || type == 1);
            return (type, num);
        }

        public (KeyPathInformation deposit, KeyPathInformation change) ReserveAddressesInNBXplorer()
        {
            // init keypaths
            var deposit = client.GetUnused(pubkey, DerivationFeature.Deposit, reserve: true);
            var change = client.GetUnused(pubkey, DerivationFeature.Change, reserve: true);
            foreach (var addr in db.WalletAddrs)
            {
                (var type, var num) = ParseKeyPath(addr.Path);
                if (type == 0)
                {
                    // deposit
                    (var typeD, var numD) = ParseKeyPath(deposit.KeyPath.ToString());
                    System.Diagnostics.Debug.Assert(type == typeD);
                    while (num > numD)
                    {
                        // reserve another address
                        deposit = client.GetUnused(pubkey, DerivationFeature.Deposit, reserve: true);
                        (typeD, numD) = ParseKeyPath(deposit.KeyPath.ToString());
                    }
                    if (num == numD)
                        System.Diagnostics.Debug.Assert(addr.Address == deposit.Address);
                }
                else
                {
                    // change
                    (var typeC, var numC) = ParseKeyPath(change.KeyPath.ToString());
                    System.Diagnostics.Debug.Assert(type == typeC);
                    while (num > numC)
                    {
                        // reserve another address
                        change = client.GetUnused(pubkey, DerivationFeature.Change, reserve: true);
                        (typeC, numC) = ParseKeyPath(change.KeyPath.ToString());
                    }
                    if (num == numC)
                        System.Diagnostics.Debug.Assert(addr.Address == change.Address);
                }
            }
            return (deposit, change);
        }
        */
    }
}
