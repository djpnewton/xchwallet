using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Newtonsoft.Json;
using WavesCS;
using Microsoft.Extensions.Logging;

namespace xchwallet
{
    public class WavWallet : BaseWallet
    {
        public const string TYPE = "WAVES";
        protected override string _type()
        {
            return TYPE;
        }

        Node node = null;
        protected string assetId = null;
        protected Asset asset = null;
        protected Asset feeAsset = null;

        char ChainId()
        {
            if (mainnet)
                return Node.MainNetChainId;
            return Node.TestNetChainId;
        }

        PrivateKeyAccount CreateAccount(int nonce)
        {
            var seed = Utils.ParseHexString(seedHex);
            return PrivateKeyAccount.CreateFromSeed(seed, ChainId(), nonce);
        }

        public WavWallet(ILogger logger, WalletContext db, bool mainnet, Uri nodeAddress) : base(logger, db, mainnet)
        {
            this.logger = logger;

            this.node = new Node(nodeAddress.ToString(), ChainId());
            this.assetId = Assets.WAVES.Id;
            this.asset = Assets.WAVES;
            this.feeAsset = this.asset;
        }

        public override bool IsMainnet()
        {
            return mainnet;
        }

        public override WalletAddr NewAddress(string tag)
        {
            var nonce = db.LastPathIndex;
            db.LastPathIndex++;
            // create new address that is unused
            var _tag = db.TagGet(tag);
            Util.WalletAssert(_tag != null, $"Tag '{tag}' does not exist");
            var acct = CreateAccount(nonce);
            var address = new WalletAddr(_tag, nonce.ToString(), nonce, acct.Address);
            // add to address list
            db.WalletAddrs.Add(address);
            return address;
        }

        public override void UpdateFromBlockchain()
        {
            var blockHeight = (long)node.GetObject("blocks/height")["height"];
            var addedTxs = new List<ChainTx>();
            foreach (var tag in GetTags())
            {
                foreach (var addr in tag.Addrs)
                    UpdateTxs(addr, addedTxs, blockHeight);
            }
        }

        void UpdateTxs(WalletAddr address, List<ChainTx> addedTxs, long blockHeight)
        {
            var sufficientTxsQueried = false;
            var processedTxs = new Dictionary<string, TransferTransaction>();
            var limit = 100;
            while (!sufficientTxsQueried)
            {  
                var nodeTxs = node.GetTransactions(address.Address, limit);
                logger.LogDebug("UpdateTxs ({0}) count: {1}, limit: {2}, sufficientTxsQueried: {3}", address.Address, nodeTxs.Count(), limit, sufficientTxsQueried);
 
                // if less txs are returned then we requested we have all txs for this account
                if (nodeTxs.Count() < limit)
                    sufficientTxsQueried = true;

                foreach (var nodeTx in nodeTxs)
                {
                    if (nodeTx is TransferTransaction)
                    {
                        var trans = (TransferTransaction)nodeTx;
                        if (trans.Asset.Id == assetId)
                        {
                            var id = trans.GenerateId();
                            var date = ((DateTimeOffset)trans.Timestamp).ToUnixTimeSeconds();

                            // skip any txs we have already processed
                            if (processedTxs.ContainsKey(id))
                                continue;

                            var amount = trans.Asset.AmountToLong(trans.Amount);
                            var fee = trans.FeeAsset.AmountToLong(trans.Fee);
                            // calculate the confs - this is slow, we could probably make it better by recording the block height in ChainTx
                            // once then we can cheaply recalc the confirmation count
                            var txinfo = node.GetObject($"transactions/info/{id}");
                            var txHeight = (long)txinfo["height"];
                            var confs = blockHeight - txHeight;

                            var ctx = db.ChainTxGet(id);
                            if (ctx == null)
                            {
                                // if we already have a pending addition to the db we
                                // not create a new one or update it
                                ctx = addedTxs.SingleOrDefault(t => t.TxId == id);
                                if (ctx == null)
                                {
                                    ctx = new ChainTx(id, date, trans.Sender, trans.Recipient, amount, fee, txHeight, confs);
                                    db.ChainTxs.Add(ctx);
                                    addedTxs.Add(ctx);
                                }
                            }
                            else
                            {
                                ctx.Height = txHeight;
                                ctx.Confirmations = confs;
                                db.ChainTxs.Update(ctx);
                                // if we are replacing txs already in our wallet we have queried sufficent txs for this account
                                sufficientTxsQueried = true;
                            }
                            var wtx = db.TxGet(address, ctx);
                            if (wtx == null)
                            {
                                if (trans.Recipient == address.Address)
                                {
                                    // special case we are recipient and sender
                                    if (trans.Sender == address.Address)
                                    {
                                        ctx.Amount = 0;
                                        wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Outgoing, Acknowledged=false };
                                    }
                                    else
                                        wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Incomming, Acknowledged=false };
                                }
                                else
                                    wtx = new WalletTx{ ChainTx=ctx, Address=address, Direction=WalletDirection.Outgoing, Acknowledged=false };
                                db.WalletTxs.Add(wtx);
                                db.WalletTxAddMeta(wtx);
                            }

                            // record the transactions we have processed already
                            processedTxs[id] = trans;
                        }
                    }
                }
                limit *= 2;
            }
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
            Util.WalletAssert(addr != null, $"Address '{address}' does not exist");
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

        WalletError CreateSpendTx(IEnumerable<WalletAddr> candidates, string to, BigInteger amount, BigInteger fee, BigInteger feeMax,
            out Tuple<string, TransferTransaction> signedSpendTx)
        {
            signedSpendTx = null;
            if (fee > feeMax)
                return WalletError.MaxFeeBreached;
            foreach (var acct in candidates)
            {
                // get balance for the account
                var balance = GetAddrBalance(acct.Address);
                logger.LogDebug($"balance {balance}, amount: {amount}, amount + fee: {amount + fee}");
                // if the balance is greater then the fee we can use this account
                if (balance >= fee + amount)
                {
                    // create signed transaction
                    var account = CreateAccount(Int32.Parse(acct.Path));
                    var amountDecimal = asset.BigIntToAmount(amount);
                    var feeDecimal = asset.BigIntToAmount(fee);
                    var tx = new TransferTransaction(account.PublicKey, to, asset, amountDecimal, feeDecimal, feeAsset);
                    tx.Sign(account);
                    signedSpendTx = new Tuple<string, TransferTransaction>(acct.Address, tx);
                    return WalletError.Success;
                }
            }
            return WalletError.InsufficientFunds;
        }

        WalletError CreateSpendTxs(IEnumerable<WalletAddr> candidates, string to, BigInteger amount, BigInteger fee, BigInteger feeMax,
            out List<Tuple<string, TransferTransaction>> signedSpendTxs, bool ignoreFees=false)
        {
            signedSpendTxs = new List<Tuple<string, TransferTransaction>>();
            var amountRemaining = amount;
            var feeTotal = new BigInteger(0);
            foreach (var acct in candidates)
            {
                if (amountRemaining == 0)
                    break;
                // get balance for the account
                var balance = GetAddrBalance(acct.Address);
                // if the balance is greater then the fee we can use this account
                if (balance > fee)
                {
                    // calcuate the actual amount we will use from this account
                    var amountThisAddress = balance - fee;
                    if (amountThisAddress > amountRemaining)
                        amountThisAddress = amountRemaining;
                    // create signed transaction
                    var account = CreateAccount(Int32.Parse(acct.Path));
                    var amountThisAddressDecimal = asset.BigIntToAmount(amountThisAddress);
                    var feeDecimal = asset.BigIntToAmount(fee);
                    var tx = new TransferTransaction(account.PublicKey, to, asset, amountThisAddressDecimal, feeDecimal, feeAsset);
                    tx.Sign(account);
                    // update spend tx list and amount remaining
                    amountRemaining -= amountThisAddress;
                    if (ignoreFees)
                        amountRemaining -= fee;
                    signedSpendTxs.Add(new Tuple<string, TransferTransaction>(acct.Address, tx));
                }
                feeTotal += fee;
            }
            logger.LogDebug("feeMax {0}, feeTotal {1}", feeMax, feeTotal);
            logger.LogDebug("amountRemaining {0}", amountRemaining);
            if (feeTotal > feeMax)
                return WalletError.MaxFeeBreached;
            if (amountRemaining != 0)
                return WalletError.InsufficientFunds;
            return WalletError.Success;
        }

        WalletTx AddOutgoingTx(string from, TransferTransaction signedTx, WalletTxMeta meta)
        {
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var address = db.AddrGet(from);
            var amount = asset.AmountToLong(signedTx.Amount);
            var fee = asset.AmountToLong(signedTx.Fee);
            if (from == signedTx.Recipient) // special case if we send to ourselves
                amount = 0;
            logger.LogDebug("outgoing tx: amount: {0}, fee: {1}", amount, fee);
            var ctx = new ChainTx(signedTx.GenerateId(), date, from, signedTx.Recipient, amount, fee, -1, 0);
            db.ChainTxs.Add(ctx);
            var wtx = new WalletTx{ChainTx=ctx, Address=address, Direction=WalletDirection.Outgoing};
            db.WalletTxs.Add(wtx);
            if (meta != null)
                wtx.Meta = meta;
            else
                db.WalletTxAddMeta(wtx);
            return wtx;
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out WalletTx wtx, WalletTxMeta meta=null)
        {
            wtx = null;
            // create spend transaction from accounts
            var accts = GetAddresses(tag);
            Tuple<string, TransferTransaction> signedSpendTx;
            var res = CreateSpendTx(accts, to, amount, feeUnit, feeMax, out signedSpendTx);
            if (res == WalletError.Success)
            {
                // send the raw signed transaction and get the txid
                try
                {
                    var output = node.Broadcast(signedSpendTx.Item2);
                    logger.LogDebug(output);
                    var txid = signedSpendTx.Item2.GenerateId();
                }
                catch (System.Net.WebException ex)
                {
                    logger.LogError("{0}", ex);
                    return WalletError.FailedBroadcast;
                }
                // add to wallet data
                wtx = AddOutgoingTx(signedSpendTx.Item1, signedSpendTx.Item2, meta);
            }
            return res;
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs)
        {
            wtxs = new List<WalletTx>();
            var to = NewOrExistingAddress(tagTo);
            BigInteger balance = 0;
            var accts = new List<WalletAddr>();
            foreach (var tag in tagFrom)
            {
                balance += GetBalance(tag);
                var tagAccts = GetAddresses(tag);
                accts.AddRange(tagAccts);
            }
            List<Tuple<string, TransferTransaction>> signedSpendTxs;
            var res = CreateSpendTxs(accts, to.Address, balance, feeUnit, feeMax, out signedSpendTxs, ignoreFees: true);
            if (res == WalletError.Success)
            {
                // send each raw signed transaction and get the txid
                foreach (var tx in signedSpendTxs)
                {
                    try
                    {
                        var output = node.Broadcast(tx.Item2);
                        logger.LogDebug(output);
                    }
                    catch (System.Net.WebException ex)
                    {
                        logger.LogError("{0}", ex);
                        return WalletError.PartialBroadcast;
                    }
                    // add to wallet data
                    var wtx = AddOutgoingTx(tx.Item1, tx.Item2, null);
                    ((List<WalletTx>)wtxs).Add(wtx);
                }
            }
            return res;
        }

        public override bool ValidateAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;
            var data = Base58.Decode(address);
            if (data.Length != 26)
                return false;
            if (data[0] != 1)
                return false;
            if (data[1] != ChainId())
                return false;
            return true;
        }

        public override string AmountToString(BigInteger value)
        {
            return asset.BigIntToAmount(value).ToString();
        }

        public override BigInteger StringToAmount(string value)
        {
            var _scale = new decimal(1, 0, 0, false, asset.Decimals);
            var d = decimal.Parse(value) / _scale;
            return (BigInteger)d;
        }
    }

    public class ZapWallet : WavWallet
    {
        public new const string TYPE = "ZAP";
        protected override string _type()
        {
            return TYPE;
        }

        public ZapWallet(ILogger logger, WalletContext db, bool mainNet, Uri nodeAddress) : base(logger, db, mainNet, nodeAddress)
        {
            this.assetId = "CgUrFtinLXEbJwJVjwwcppk4Vpz1nMmR3H5cQaDcUcfe";
            if (IsMainnet())
                this.assetId = "9R3iLi4qGLVWKc16Tg98gmRvgg1usGEYd7SgC1W5D6HB";
            this.asset = new Asset(assetId, "Zap", 2);
            this.feeAsset = this.asset;
        }
    }

    public static class AssetExtensons
    {
        public static decimal BigIntToAmount<T>(this T asset, BigInteger value) where T: Asset
        {
            var _scale = new decimal(1, 0, 0, false, asset.Decimals);
            return (decimal)value * _scale;
        }
    }
}
