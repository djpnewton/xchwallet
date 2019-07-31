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
    public class CoinCandidate
    {
        public WalletAddr Addr { get; set; }
        public Coin Coin { get; set; }
        public UTXO Utxo { get; set; }
        public CoinCandidate(WalletAddr addr, Coin coin, UTXO utxo)
        {
            Addr = addr;
            Coin = coin;
            Utxo = utxo;
        }
    }

    public class CoinSpend
    {
        public string From { get; set; }
        public WalletAddr Addr { get; set; }
        public Coin Coin { get; set; }
        public Key Key { get; set; }
        public CoinSpend(string from, WalletAddr addr, Coin coin, Key key)
        {
            From = from;
            Addr = addr;
            Coin = coin;
            Key = key;
        }
    }

    public class CoinOutput
    {
        public string To { get; set; }
        public BigInteger Amount { get; set; }
        public CoinOutput(string to, BigInteger amount)
        {
            To = to;
            Amount = amount;
        }
    }

    public class OutgoingTx
    {
        public Transaction Tx { get; set; }
        public List<CoinSpend> Spends { get; set; }
        public List<CoinOutput> Outputs { get; set; }
        public BigInteger Fee { get; set; }
        public OutgoingTx(Transaction tx, List<CoinSpend> spends, List<CoinOutput> outputs, BigInteger fee)
        {
            Tx = tx;
            Spends = spends;
            Outputs = outputs;
            Fee = fee;
        }
    }

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
        readonly DirectDerivationStrategy pubkey = null;
        readonly Uri nbxplorerAddress = null;
        ExplorerClient _client = null;

        NBXplorerNetwork GetNbxNetwork()
        {
            if (mainnet)
                return new NBXplorerNetworkProvider(NetworkType.Mainnet).GetFromCryptoCode("BTC");
            else
                return new NBXplorerNetworkProvider(NetworkType.Testnet).GetFromCryptoCode("BTC");
        }

        ExplorerClient GetClient()
        {
            if (_client == null)
            {
                // create NBXplorer client
                _client = new ExplorerClient(GetNbxNetwork(), nbxplorerAddress);
                _client.Track(pubkey);
            }
            return _client;
        }

        public Network GetNetwork()
        {
            return IsMainnet() ? Network.Main : Network.TestNet; 
        }

        public BtcWallet(ILogger logger, WalletContext db, bool mainnet, Uri nbxplorerAddress, bool useLegacyAddrs=false) : base(logger, db, mainnet)
        {
            this.logger = logger;
            this.nbxplorerAddress = nbxplorerAddress;

            // create extended key
            var network = GetNetwork();
            key = new BitcoinExtKey(new ExtKey(seedHex), network);
            var strpubkey = $"{key.Neuter().ToString()}";
            if (useLegacyAddrs)
                strpubkey = strpubkey + "-[legacy]";
            pubkey = (DirectDerivationStrategy)new DerivationStrategyFactory(network).Parse(strpubkey);
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
            var keypathInfo = GetClient().GetUnused(pubkey, DerivationFeature.Deposit, reserve: true);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(GetClient().Network.NBitcoinNetwork);
            var address = new WalletAddr(_tag, keypathInfo.KeyPath.ToString(), 0, addr.ToString());
            // add to address list
            db.WalletAddrs.Add(address);
            return address;
        }

        private BitcoinAddress AddressOf(ExtPubKey pubkey, KeyPath path)
		{
			return pubkey.Derive(path).PubKey.Hash.GetAddress(GetNetwork());
        }

        private void processUtxo(NBXplorer.Models.UTXO utxo, int currentHeight)
        {
            //TODO - add attachment
            // - read OP_RETURN ?
            
            //var addr = AddressOf(pubkey.Root, utxo.KeyPath);
            var to = utxo.ScriptPubKey.GetDestinationAddress(GetNetwork());
            var id = utxo.Outpoint.Hash.ToString();
            var date = utxo.Timestamp.ToUnixTimeSeconds();
            var height = utxo.Confirmations > 0 ? currentHeight - (utxo.Confirmations-1) : -1;

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
            var status = utxo.Confirmations > 0 ? ChainTxStatus.Confirmed : ChainTxStatus.Unconfirmed;
            if (ctx.NetworkStatus == null)
            {
                var txResult = GetClient().GetTransaction(utxo.Outpoint.Hash);
                var networkStatus= new ChainTxNetworkStatus(ctx, status, 0, txResult.Transaction.ToBytes());
                db.ChainTxNetworkStatus.Add(networkStatus);
            }
            else
            {
                // transaction update comes from our trusted node so we will override any status we have set manually
                ctx.NetworkStatus.Status = status;
                db.ChainTxNetworkStatus.Update(ctx.NetworkStatus);
            }
            // add output
            var o = db.TxOutputGet(id, utxo.Outpoint.N);
            if (o == null)
            {
                o = new TxOutput(id, address.Address, utxo.Outpoint.N, utxo.Value.Satoshi);
                o.ChainTx = ctx;
                o.WalletAddr = address;
                db.TxOutputs.Add(o);
            }
            else if (o.WalletAddr == null)
            {
                logger.LogInformation($"Updating output with no address: {id}, {o.N}, {address.Address}");
                o.WalletAddr = address;
                db.TxOutputs.Update(o);
            }
            // add/update wallet tx
            var wtx = db.TxGet(address, ctx, WalletDirection.Incomming);
            if (wtx == null)
            {
                wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Incomming, State=WalletTxState.None };
                db.WalletTxs.Add(wtx);
            }
        }

        public override void UpdateFromBlockchain(IDbContextTransaction dbtx)
        {
            System.Diagnostics.Debug.Assert(dbtx != null);
            UpdateTxs();
            UpdateTxConfirmations(pubkey);
            db.SaveChanges();
            return;
        }

        private void UpdateTxs()
        {
            var utxos = GetClient().GetUTXOs(pubkey);
            foreach (var item in utxos.Unconfirmed.UTXOs)
            {
                processUtxo(item, utxos.CurrentHeight);
                db.SaveChanges();
            }
            foreach (var item in utxos.Confirmed.UTXOs)
            {
                processUtxo(item, utxos.CurrentHeight);
                db.SaveChanges();
            }
        }

        private void UpdateTxConfirmations(GetTransactionsResponse txs)
        {
            foreach (var tx in txs.ConfirmedTransactions.Transactions)
            {
                var ctx = db.ChainTxGet(tx.TransactionId.ToString());
                if (ctx != null)
                {
                    if (ctx.Confirmations != tx.Confirmations)
                    {
                        ctx.Confirmations = tx.Confirmations;
                        db.ChainTxs.Update(ctx);
                        var status = tx.Confirmations > 0 ? ChainTxStatus.Confirmed : ChainTxStatus.Unconfirmed;
                        if (ctx.NetworkStatus != null && ctx.NetworkStatus.Status != status)
                        {
                            // transaction update comes from our trusted node so we will override any status we have set manually
                            ctx.NetworkStatus.Status = status;
                            db.ChainTxNetworkStatus.Update(ctx.NetworkStatus);
                        }
                    }
                }
                else
                    logger.LogError($"No wallet tx found for {tx.TransactionId}");
            }
        }

        private void UpdateTxConfirmations(WalletAddr addr)
        {
            var baddr = BitcoinAddress.Create(addr.Address, GetNetwork());
            var trackedSource = TrackedSource.Create(baddr);
            var txs = GetClient().GetTransactions(trackedSource);
            UpdateTxConfirmations(txs);
        }

        private void UpdateTxConfirmations(DerivationStrategyBase derivationStrat)
        {
            var txs = GetClient().GetTransactions(derivationStrat);
            UpdateTxConfirmations(txs);
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
            var keypathInfo = GetClient().GetUnused(pubkey, DerivationFeature.Change, reserve: false);
            var addr = keypathInfo.ScriptPubKey.GetDestinationAddress(GetNetwork());
            // check that address is not already present in the db (we used 'reserve: false')
            var address = db.AddrGet(addr.ToString());
            if (address != null)
                return addr;
            // add to address list
            address = new WalletAddr(tag, keypathInfo.KeyPath.ToString(), 0, addr.ToString());
            db.WalletAddrs.Add(address);
            return addr;
        }

        IEnumerable<WalletTx> AddOutgoingTx(Transaction tx, List<CoinSpend> spents, List<CoinOutput> outputs, BigInteger fee, WalletTag tagFor)
        {
            var txid = tx.GetHash().ToString();
            logger.LogDebug("outgoing tx: outputs: {0}, fee: {1}", outputs.Select(o=>o.Amount), fee);
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // create chain tx
            var ctx = db.ChainTxGet(txid);
            if (ctx == null)
            {
                ctx = new ChainTx(txid, date, fee, -1, 0);
                db.ChainTxs.Add(ctx);
            }
            if (ctx.NetworkStatus == null)
            {
                var networkStatus = new ChainTxNetworkStatus(ctx, ChainTxStatus.Unconfirmed, date, tx.ToBytes());
                db.ChainTxNetworkStatus.Add(networkStatus);
            }
            // create tx inputs
            uint n = 0;
            foreach (var spent in spents)
            {
                if (db.TxInputGet(txid, n) == null)
                {
                    var i = new TxInput(txid, spent.From, n, spent.Coin.Amount.Satoshi);
                    i.ChainTx = ctx;
                    i.WalletAddr = spent.Addr;
                    db.TxInputs.Add(i);
                }
                n++;
            }
            // create tx outputs
            n = 0;
            foreach (var output in outputs)
            {
                if (db.TxOutputGet(txid, n) == null)
                {
                    var o = new TxOutput(txid, output.To, n, output.Amount);
                    o.ChainTx = ctx;
                    db.TxOutputs.Add(o);
                    if (tagFor != null)
                        db.TxOutputsForTag.Add(new TxOutputForTag { TxOutput = o, Tag = tagFor });
                }
                n++;
            }
            // create wallet txs
            var wtxs = new List<WalletTx>();
            foreach (var spent in spents)
            {
                if (spent.Addr == null)
                    continue;
                var wtx = db.TxGet(spent.Addr, ctx, WalletDirection.Outgoing);
                if (wtx == null)
                {
                    wtx = new WalletTx { ChainTx = ctx, Address = spent.Addr, Direction = WalletDirection.Outgoing, State = WalletTxState.None };
                    db.WalletTxs.Add(wtx);
                }
                wtxs.Add(wtx);
            }
            return wtxs;
        }

        FeeRate GetFeeRate(Transaction tx, List<CoinSpend> toBeSpent)
        {
            // sign tx before calculating fee so it includes signatures
            var coins = from a in toBeSpent select a.Coin;
            var keys = from a in toBeSpent select a.Key;
            tx.Sign(keys.ToArray(), coins.ToArray());
            return tx.GetFeeRate(coins.ToArray());
        }

        Transaction GetTransaction(string txid)
        {
            var ctx = db.ChainTxGet(txid);
            if (ctx == null || ctx.NetworkStatus == null || ctx.NetworkStatus.TxBin == null)
                return null;
            var hex = BitConverter.ToString(ctx.NetworkStatus.TxBin).Replace("-", string.Empty);
            return Transaction.Parse(hex, GetNetwork());
        }

        WalletError UseReplacedTxCoins(IEnumerable<WalletAddr> addrs, List<CoinCandidate> candidates, string txid, Transaction tx=null)
        {
            // we call this function before choosing any other candidate coins so that
            // the replacement tx is sure to use at least one of them

            if (txid == null && tx == null)
                return WalletError.Success;
            // find tx to replace (if not provided by caller)
            if (tx == null)
            {
                tx = GetTransaction(txid);
                if (tx == null)
                    return WalletError.NothingToReplace;
            }
            // use tx inputs
            foreach (var input in tx.Inputs)
            {
                var txWithOutput = GetTransaction(input.PrevOut.Hash.ToString());
                if (txWithOutput == null)
                {
                    var txResult = GetClient().GetTransaction(input.PrevOut.Hash);
                    if (txResult != null)
                        txWithOutput = txResult.Transaction;
                }
                if (txWithOutput == null)
                    continue;
                var coin = new Coin(input.PrevOut, txWithOutput.Outputs[input.PrevOut.N]);
                var addrStr = coin.ScriptPubKey.GetDestinationAddress(GetNetwork()).ToString();
                var addr = addrs.Where(a => a.Address == addrStr).FirstOrDefault();
                if (addr != null)
                    candidates.Add(new CoinCandidate(addr, coin, null));
            }
            if (candidates.Count() == 0)
                return WalletError.UnableToReplace;
            return WalletError.Success;
        }

        void UseUtxoCoins(IEnumerable<WalletAddr> addrs, List<CoinCandidate> candidates, UTXOChanges utxos, int minConfs=0)
        {
            if (minConfs <= 0)
                foreach (var utxo in utxos.Unconfirmed.UTXOs)
                {
                    var addrStr = utxo.ScriptPubKey.GetDestinationAddress(GetNetwork()).ToString();
                    var addr = addrs.Where(a => a.Address == addrStr).FirstOrDefault();
                    if (addr != null)
                        candidates.Add(new CoinCandidate(addr, utxo.AsCoin(), utxo));
                }
            foreach (var utxo in utxos.Confirmed.UTXOs)
            {
                if (minConfs > 0 && utxo.Confirmations < minConfs)
                    continue;
                // check is not already spent but as yet unconfirmed
                if (utxos.Unconfirmed.SpentOutpoints.Any(so => so.Hash == utxo.Outpoint.Hash))
                    continue;
                var addrStr = utxo.ScriptPubKey.GetDestinationAddress(GetNetwork()).ToString();
                var addr = addrs.Where(a => a.Address == addrStr).FirstOrDefault();
                if (addr != null)
                    candidates.Add(new CoinCandidate(addr, utxo.AsCoin(), utxo));
            }
        }

        void CheckCoinsAreInWallet(IEnumerable<CoinCandidate> candidates, int currentHeight)
        {
            foreach (var candidate in candidates)
            {
                // Coins from double spending (UseReplacedTxCoins) we dont have the uxto for ATM
                // but we can be sure the uxto has been processed already
                if (candidate.Utxo != null)
                    processUtxo(candidate.Utxo, currentHeight);
            }
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, WalletTag tagFor=null, string replaceTxId = null)
        {
            wtxs = new List<WalletTx>();
            var tagChange_ = db.TagGet(tagChange);
            Util.WalletAssert(tagChange_ != null, $"Tag '{tagChange}' does not exist");
            // create tx template with destination as first output
            var tx = Transaction.Create(GetNetwork());
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to, GetNetwork());
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on (a tx we might want to replace and) UTXOs from the selected tag
            var addrs = GetAddresses(tag);
            var candidates = new List<CoinCandidate>();
            var res = UseReplacedTxCoins(addrs, candidates, replaceTxId);
            if (res != WalletError.Success)
                return res;
            var utxos = GetClient().GetUTXOs(pubkey);
            UseUtxoCoins(addrs, candidates, utxos);
            // add inputs until we can satisfy our output
            BigInteger totalInput = 0;
            var toBeSpent = new List<CoinSpend>();
            foreach (var candidate in candidates)
            {
                // add to list of coins and private keys to spend
                var txin = new TxIn(candidate.Coin.Outpoint);
                txin.Sequence = 0; // RBF: BIP125
                tx.Inputs.Add(txin);
                totalInput += candidate.Coin.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Addr.Path)).PrivateKey;
                toBeSpent.Add(new CoinSpend(candidate.Addr.Address, candidate.Addr, candidate.Coin, privateKey));
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
            var coins = from a in toBeSpent select a.Coin;
            var keys = from a in toBeSpent select a.Key;
            // check coins are represented as incoming wallet txs
            CheckCoinsAreInWallet(candidates, utxos.CurrentHeight);
            // sign inputs (after adding a change output)
            tx.Sign(keys.ToArray(), coins.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(coins.ToArray());
            if (fee.Satoshi > feeMax)
                return WalletError.MaxFeeBreached;
            // broadcast transaction
            var result = GetClient().Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                var coinOutput = new CoinOutput(to, amount);
                var wtxs_ = AddOutgoingTx(tx, toBeSpent, new List<CoinOutput> { coinOutput }, fee.Satoshi, tagFor);
                ((List<WalletTx>)wtxs).AddRange(wtxs_);
                return WalletError.Success;
            }
            else
            {
                logger.LogError("{0}, {1}, {2}", result.RPCCode, result.RPCCodeMessage, result.RPCMessage);
                return WalletError.FailedBroadcast;
            }
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, int minConfs=0, string replaceTxId=null)
        {
            wtxs = new List<WalletTx>();
            // generate new address to send to
            var to = NewOrUnusedAddress(tagTo);
            // calc amount in tagFrom
            BigInteger amount = 0;
            foreach (var tag in tagFrom)
                amount += this.GetBalance(tag);
            // check we have enough funds
            if (amount <= 0)
                return WalletError.InsufficientFunds;
            // create tx template with destination as first output
            var tx = Transaction.Create(GetNetwork());
            var money = new Money((ulong)amount);
            var toaddr = BitcoinAddress.Create(to.Address, GetNetwork());
            var output = tx.Outputs.Add(money, toaddr);
            // create list of candidate coins to spend based on (a tx we might want to replace and) UTXOs from the selected tag
            var candidates = new List<CoinCandidate>();
            if (replaceTxId != null)
            {
                // if we are replacing a tx we need to replace at least one of its inputs 
                var replaceTx = GetTransaction(replaceTxId);
                if (replaceTx == null)
                    return WalletError.NothingToReplace;
                var res = WalletError.UnableToReplace;
                foreach (var tag in tagFrom)
                {
                    var addrs = GetAddresses(tag);
                    if (UseReplacedTxCoins(addrs, candidates, replaceTxId, replaceTx) == WalletError.Success)
                        res = WalletError.Success;
                }
                if (res != WalletError.Success)
                    return res;
            }
            var utxos = GetClient().GetUTXOs(pubkey);
            //TODO: GetAddresses(tags)
            foreach (var tag in tagFrom)
            {
                var addrs = GetAddresses(tag);
                UseUtxoCoins(addrs, candidates, utxos, minConfs);
            }
            // add all inputs so we can satisfy our output
            BigInteger totalInput = 0;
            var toBeSpent = new List<CoinSpend>();
            foreach (var candidate in candidates)
            {
                // add to list of coins and private keys to spend
                var txin = new TxIn(candidate.Coin.Outpoint);
                txin.Sequence = 0; // RBF: BIP125
                tx.Inputs.Add(txin);
                totalInput += candidate.Coin.Amount.Satoshi;
                var privateKey = key.ExtKey.Derive(new KeyPath(candidate.Addr.Path)).PrivateKey;
                toBeSpent.Add(new CoinSpend(candidate.Addr.Address, candidate.Addr, candidate.Coin, privateKey));
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
            var coins = from a in toBeSpent select a.Coin;
            var keys = from a in toBeSpent select a.Key;
            // check coins are represented as incoming wallet txs
            CheckCoinsAreInWallet(candidates, utxos.CurrentHeight);
            tx.Sign(keys.ToArray(), coins.ToArray());
            // recalculate fee rate and check it is less then the max fee
            var fee = tx.GetFee(coins.ToArray());
            if (fee.Satoshi > feeMax)
                return WalletError.MaxFeeBreached;
            // broadcast transaction
            var result = GetClient().Broadcast(tx);
            if (result.Success)
            {
                // log outgoing transaction
                var coinOutput = new CoinOutput(to.Address, amount);
                var wtxs_ = AddOutgoingTx(tx, toBeSpent, new List<CoinOutput> { coinOutput }, fee.Satoshi, null);
                ((List<WalletTx>)wtxs).AddRange(wtxs_);
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
                if (tx.BlockHash != null)
                    req.Transactions.Add(new RescanRequest.TransactionToRescan { TransactionId = uint256.Parse(tx.TxId), BlockId = uint256.Parse(tx.BlockHash) });
            GetClient().Rescan(req);
            return true;
        }

        public bool RecreateTxs(IDbContextTransaction dbtx, Dictionary<string, List<UTXO>> utxos, Dictionary<string, OutgoingTx> outgoingTxs, int currentHeight)
        {
            System.Diagnostics.Debug.Assert(dbtx != null);
            foreach (var kvPair in utxos)
            {
                foreach (var utxo in kvPair.Value)
                    processUtxo(utxo, currentHeight);
            }
            db.SaveChanges();
            foreach (var kvPair in outgoingTxs)
            {
                var outgoingTx = kvPair.Value;
                AddOutgoingTx(outgoingTx.Tx, outgoingTx.Spends, outgoingTx.Outputs, outgoingTx.Fee, null);
            }
            db.SaveChanges();
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
