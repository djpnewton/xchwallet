import requests
import time
import web3
from config import Cfg

cfg = Cfg()
web3 = web3.Web3(web3.providers.rpc.HTTPProvider(cfg.geth_uri, request_kwargs={'timeout': 60}))
if cfg.testnet:
    assert(int(web3.version.network) == 3) #ropsten
else:
    assert(int(web3.version.network) == 1) #mainnet

# pool of pending transactions
pending_txs = {}

def get_current_block_number():
    return web3.eth.blockNumber

def get_block_hash_and_txs(block_num, addresses):
    txs = {}
    block = web3.eth.getBlock(block_num, True)
    block_transactions = block.transactions
    if not block_transactions:
        block_transactions = []
    for tx in block_transactions:
        # remove from pending_txs if found in a block
        if tx["hash"] in pending_txs:
            del pending_txs[tx["hash"]]
        if tx["to"]:
            to = tx["to"].lower()
            if to in addresses:
                if to in txs:
                    txs[to].append(tx)
                else:
                    txs[to] = [tx]
    return block.hash, txs, len(block_transactions)

def get_block_hash(block_num):
    block = web3.eth.getBlock(block_num, False)
    if block:
        return block.hash
    return None

def scan_pending_txs(addresses):
    txs = {}
    remove = []
    _5hours = 60 * 60 * 5
    now = time.time()
    for key, item in pending_txs.items():
        timestamp, tx = item
        to = tx["to"]
        if to in addresses:
            if to in txs:
                txs[to].append(tx)
            else:
                txs[to] = [tx]
        # mark all pending txs in the pool for longer then _5hours
        if now - timestamp > _5hours:
            remove.append(key)
    # remove all expired txs from pending_txs
    for key in remove:
        del pending_txs[key]
    return txs, len(pending_txs)

def pending_tx_filter():
    return web3.eth.filter("pending")

def check_tx_filter(logger, filter):
    def get_pending_tx_record(txid):
        tx = web3.eth.getTransaction(txid)
        if not tx:
            logger.error("could not get tx info (%s)" % txid.hex())
        elif tx["to"]:
            tx = {"to": tx["to"].lower(), "from": tx["from"].lower(), "hash": tx["hash"], "value": tx["value"]}
            return (time.time(), tx)
        else:
            logger.info("could not get tx 'to' info, possibly contract stuff (%s)" % txid.hex())

    seen_txids = filter.get_new_entries()
    for txid in seen_txids:
        logger.info("!new tx! {0}".format(txid.hex()))
        # add transaction to pending transaction pool
        if not txid in pending_txs:
            record = None
            record = get_pending_tx_record(txid)
            if record:
                pending_txs[txid] = record
    return seen_txids

def get_pending_txs():
    return pending_txs
