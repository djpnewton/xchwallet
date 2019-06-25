using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Newtonsoft.Json;
using WavesCS;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

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

        public override LedgerModel LedgerModel { get { return LedgerModel.Account; } }

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

        public override void UpdateFromBlockchain(IDbContextTransaction dbtx)
        {
            System.Diagnostics.Debug.Assert(dbtx != null);
            var blockHeight = (long)node.GetObject("blocks/height")["height"];
            foreach (var tag in GetTags())
                foreach (var addr in tag.Addrs)
                { 
                    UpdateTxs(addr, blockHeight);
                    db.SaveChanges();
                }
            return;
        }

        ChainTx AddUpdateChainTx(string id, long date, long confs, long height, BigInteger fee, byte[] attachment, ref bool sufficientTxsQueried)
        {
            // add/update chain tx
            var ctx = db.ChainTxGet(id);
            if (ctx == null)
            {
                ctx = new ChainTx(id, date, fee, height, confs);
                db.ChainTxs.Add(ctx);
            }
            else
            {
                ctx.Height = height;
                ctx.Confirmations = confs;
                db.ChainTxs.Update(ctx);
                // if we are replacing txs already in our wallet we have queried sufficent txs for this account
                sufficientTxsQueried = true;
            }
            if (ctx.Attachment == null && attachment != null && attachment.Length > 0)
            {
                var att = new ChainAttachment(ctx, attachment);
                db.ChainAttachments.Add(att);
            }
            return ctx;
        }

        void AddOutputInputAndWalletTx(WalletAddr address, ChainTx ctx, uint nOut, string sender, string recipient, BigInteger amount)
        {
            // add output
            var o = db.TxOutputGet(ctx.TxId, nOut);
            if (o == null)
            {
                o = new TxOutput(ctx.TxId, recipient, nOut, amount);
                o.ChainTx = ctx;
                if (recipient == address.Address)
                    o.WalletAddr = address;
                db.TxOutputs.Add(o);
            }
            else if (o.WalletAddr == null && o.Addr == address.Address)
            {
                logger.LogInformation($"Updating output with no address: {ctx.TxId}, {o.N}, {address.Address}");
                o.WalletAddr = address;
                db.TxOutputs.Update(o);
            }
            // add input (so we can see who its from)
            var i = db.TxInputGet(ctx.TxId, nOut);
            if (i == null)
            {
                i = new TxInput(ctx.TxId, sender, nOut, amount);
                i.ChainTx = ctx;
                db.TxInputs.Add(i);
            }
            // add / update wallet tx
            var dir = recipient == address.Address ? WalletDirection.Incomming : WalletDirection.Outgoing;
            var wtx = db.TxGet(address, ctx, dir);
            if (wtx == null)
            {
                wtx = new WalletTx { ChainTx = ctx, Address = address, Direction = dir, State = WalletTxState.None };
                db.WalletTxs.Add(wtx);
            }
        }

        void AddFeeInput(ChainTx ctx, uint nIn, string sender, BigInteger fee)
        {
            // add fee input
            var i = db.TxInputGet(ctx.TxId, nIn);
            if (i == null)
            {
                i = new TxInput(ctx.TxId, sender, nIn, fee);
                i.ChainTx = ctx;
                db.TxInputs.Add(i);
            }
        }

        void UpdateTxs(WalletAddr address, long blockHeight)
        {
            var sufficientTxsQueried = false;
            var processedTxs = new Dictionary<string, Transaction>();
            var limit = 100;
            string after = null;
            while (!sufficientTxsQueried)
            {  
                var nodeTxs = node.GetTransactions(address.Address, limit, after);
                logger.LogDebug($"UpdateTxs ({address.Address}) count: {nodeTxs.Count()}, limit: {limit}, after: {after}, processedCount: {processedTxs.Count()}");
 
                // if less txs are returned then we requested we have all txs for this account
                if (nodeTxs.Count() < limit)
                    sufficientTxsQueried = true;

                foreach (var nodeTx in nodeTxs)
                {
                    after = nodeTx.GenerateId();
                    // get common waves tx params
                    var id = nodeTx.GenerateId();
                    var date = ((DateTimeOffset)nodeTx.Timestamp).ToUnixTimeSeconds();
                    var confs = nodeTx.Height > 0 ? (blockHeight - nodeTx.Height) + 1 : 0;
                    // skip any txs we have already processed
                    if (processedTxs.ContainsKey(id))
                        continue;
                    if (nodeTx is TransferTransaction)
                    {
                        var trans = (TransferTransaction)nodeTx;
                        if (trans.Asset.Id == assetId)
                        {
                            var amount = trans.Asset.AmountToLong(trans.Amount);
                            var fee = trans.FeeAsset.AmountToLong(trans.Fee);
                            var attachment = trans.Attachment;

                            // add/update chain tx
                            var ctx = AddUpdateChainTx(id, date, confs, nodeTx.Height, fee, attachment, ref sufficientTxsQueried);
                            // add output, input and wallet tx
                            AddOutputInputAndWalletTx(address, ctx, 0, trans.Sender, trans.Recipient, amount);
                            // add fee input
                            AddFeeInput(ctx, 1, trans.Sender, fee);

                            // record the transactions we have processed already
                            processedTxs[id] = trans;
                        }
                    }
                    else if (nodeTx is MassTransferTransaction)
                    {
                        var massTx = (MassTransferTransaction)nodeTx;
                        if (massTx.Asset.Id == assetId)
                        {
                            var attachment = massTx.Attachment;
                            var fee = Assets.WAVES.AmountToLong(massTx.Fee);

                            // add/update chain tx
                            var ctx = AddUpdateChainTx(id, date, confs, nodeTx.Height, fee, attachment, ref sufficientTxsQueried);
                            // process transfers
                            uint n = 0;
                            foreach (var trans in massTx.Transfers)
                            {
                                var amount = massTx.Asset.AmountToLong(trans.Amount);
                                // add output, input and wallet tx
                                AddOutputInputAndWalletTx(address, ctx, n, massTx.Sender, trans.Recipient, amount);
                                n++;
                            }
                            // add fee input
                            AddFeeInput(ctx, n, massTx.Sender, fee);

                            // record the transactions we have processed already
                            processedTxs[id] = massTx;
                        }
                    }
                }
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
                    var tx = new TransferTransaction(ChainId(), account.PublicKey, to, asset, amountDecimal, feeDecimal, feeAsset);
                    tx.Sign(account);
                    signedSpendTx = new Tuple<string, TransferTransaction>(acct.Address, tx);
                    return WalletError.Success;
                }
            }
            return WalletError.InsufficientFunds;
        }

        WalletError CreateSpendTxs(IEnumerable<Tuple<WalletAddr, BigInteger>> candidates, string to, BigInteger amount, BigInteger fee, BigInteger feeMax,
            out List<Tuple<string, TransferTransaction>> signedSpendTxs, bool ignoreFees=false)
        {
            signedSpendTxs = new List<Tuple<string, TransferTransaction>>();
            var amountRemaining = amount;
            var feeTotal = new BigInteger(0);
            foreach (var candidate in candidates)
            {
                if (amountRemaining == 0)
                    break;
                var acct = candidate.Item1;
                var balance = candidate.Item2;
                // if the balance is greater then the fee we can use this account
                if (balance > fee)
                {
                    // calcuate the actual amount we will use from this account
                    var amountThisAddress = balance - fee;
                    if (amountThisAddress > amountRemaining)
                        amountThisAddress = amountRemaining;
                    // create signed transaction
                    var wavesAccount = CreateAccount(Int32.Parse(acct.Path));
                    var amountThisAddressDecimal = asset.BigIntToAmount(amountThisAddress);
                    var feeDecimal = asset.BigIntToAmount(fee);
                    var tx = new TransferTransaction(ChainId(), wavesAccount.PublicKey, to, asset, amountThisAddressDecimal, feeDecimal, feeAsset);
                    tx.Sign(wavesAccount);
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

        WalletTx AddOutgoingTx(string from, TransferTransaction signedTx, WalletTag tagOnBehalfOf)
        {
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var address = db.AddrGet(from);
            var amount = asset.AmountToLong(signedTx.Amount);
            var fee = asset.AmountToLong(signedTx.Fee);
            logger.LogDebug("outgoing tx: amount: {0}, fee: {1}", amount, fee);
            // create chain tx
            var ctx = new ChainTx(signedTx.GenerateId(), date, fee, -1, 0);
            db.ChainTxs.Add(ctx);
            // create tx input
            var i = new TxInput(signedTx.GenerateId(), from /*signedTx.Sender is null*/, 0, amount + fee);
            i.ChainTx = ctx;
            i.WalletAddr = address;
            db.TxInputs.Add(i);
            // create tx output
            var o = new TxOutput(signedTx.GenerateId(), signedTx.Recipient, 0, amount);
            o.ChainTx = ctx;
            db.TxOutputs.Add(o);
            // create wallet tx
            var wtx = new WalletTx{ChainTx=ctx, Address=address, Direction=WalletDirection.Outgoing};
            wtx.TagOnBehalfOf = tagOnBehalfOf;
            db.WalletTxs.Add(wtx);
            return wtx;
        }

        public override WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, WalletTag tagOnBehalfOf=null)
        {
            wtxs = new List<WalletTx>();
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
                    logger.LogError(ex, "error broadcasting tx");
                    var resp = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                    dynamic obj = JsonConvert.DeserializeObject(resp);
                    if (obj != null)
                        logger.LogError($"message: {obj.message}");
                    return WalletError.PartialBroadcast;
                }
                // add to wallet data
                var wtx = AddOutgoingTx(signedSpendTx.Item1, signedSpendTx.Item2, tagOnBehalfOf);
                ((List<WalletTx>)wtxs).Add(wtx);
            }
            return res;
        }

        public override WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, int minConfs=0)
        {
            wtxs = new List<WalletTx>();
            var to = NewOrExistingAddress(tagTo);
            BigInteger balance = 0;
            var candidates = new List<Tuple<WalletAddr, BigInteger>>();
            foreach (var tag in tagFrom)
            {
                var tagAccts = GetAddresses(tag);
                foreach (var acct in tagAccts)
                {
                    var acctBal = GetAddrBalance(acct, minConfs);
                    if (acctBal > 0)
                        candidates.Add(new Tuple<WalletAddr, BigInteger>(acct, acctBal));
                    balance += acctBal;
                }
            }
            List<Tuple<string, TransferTransaction>> signedSpendTxs;
            var res = CreateSpendTxs(candidates, to.Address, balance, feeUnit, feeMax, out signedSpendTxs, ignoreFees: true);
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
                        logger.LogError(ex, "error broadcasting tx");
                        var resp = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                        dynamic obj = JsonConvert.DeserializeObject(resp);
                        if (obj != null)
                            logger.LogError($"message: {obj.message}");
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
            try
            {
                var data = Base58.Decode(address);
                if (data.Length != 26)
                    return false;
                if (data[0] != 1)
                    return false;
                if (data[1] != ChainId())
                    return false;
            }
            catch
            {
                return false;
            }
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
