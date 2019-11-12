#!/usr/bin/python3

import logging

import gevent
import requests

logger = logging.getLogger(__name__)

def parse_block_txs_easy(block_num, on_block_transfer_tx, node_http_base_url):
    ## We will just RPC to the node and get the nicely formatted block txs as a shortcut.

    # get the current block height
    url = node_http_base_url + "/blocks/height"
    r = requests.get(url)
    r.raise_for_status()
    height = r.json()["height"]
    # if we are just starting then fast forward to the current block height
    if block_num <= 0:
        block_num = height
    # iterate through all blocks up to the current tip ('block_num < height' because WAVES does a thing where the lastest block is continuously updated until a new block creator is chosen)
    while block_num < height:
        url = node_http_base_url + "/blocks/at/" + str(block_num)
        r = requests.get(url)
        r.raise_for_status()
        block = r.json()
        logger.info("parsing block: %d, %s" % (block_num, block["signature"]))
        block_txs = block["transactions"]
        # iterator through all block transactions
        for block_tx in block_txs:
            if block_tx["type"] == 4: # transfer tx
                txid = block_tx["id"]
                sender = block_tx["sender"]
                asset_id = block_tx["assetId"]
                timestamp = block_tx["timestamp"]
                amount = block_tx["amount"]
                fee = block_tx["fee"]
                recipient = block_tx["recipient"]
                attachment = block_tx["attachment"]
                # block tx callback
                on_block_transfer_tx(txid, sender, asset_id, timestamp, amount, fee, recipient, attachment)
        # increment block num
        block_num += 1
    return block_num

class Blocks():

    def __init__(self, on_block_transfer_tx, node_http_base_url="http://127.0.0.1"):
        self.block_num = 0
        self.on_block_transfer_tx = on_block_transfer_tx
        self.node_http_base_url = node_http_base_url

    def start(self, group=None):
        def runloop():
            logger.info("Blocks runloop started")
            while 1:
                self.block_num = parse_block_txs_easy(self.block_num, self.on_block_transfer_tx, self.node_http_base_url)
                gevent.sleep(20)

        def start_greenlet():
            logger.info("starting Blocks runloop...")
            self.runloop_greenlet.start()

        # create greenlet
        self.runloop_greenlet = gevent.Greenlet(runloop)
        if group != None:
            group.add(self.runloop_greenlet)
        # start greenlet
        gevent.spawn(start_greenlet)

    def stop(self):
        self.runloop_greenlet.kill()

if __name__ == "__main__":
    # setup logging
    logger.setLevel(logging.DEBUG)
    ch = logging.StreamHandler()
    ch.setLevel(logging.DEBUG)
    ch.setFormatter(logging.Formatter('[%(name)s %(levelname)s] %(message)s'))
    logger.addHandler(ch)
    # clear loggers set by any imported modules
    logging.getLogger().handlers.clear()

    def on_block_transfer_tx(txid, sig, sender, asset_id, timestamp, amount, fee, recipient, attachment):
        logger.info("block tx %s" % txid)

    blks = Blocks(on_block_transfer_tx, "https://testnet1.wavesnodes.com")
    blks.start()

    while 1:
        gevent.sleep(1)

    blks.stop()
