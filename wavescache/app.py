#!/usr/bin/python3

import os
import logging
import signal

import gevent
import gevent.pool
import base58
import pywaves

import config
import proxy
import utx
import blocks
import utils

cfg = config.read_cfg()
logger = logging.getLogger(__name__)

# set pywaves to offline mode
pywaves.setOffline()
if cfg.testnet:
    pywaves.setChain("testnet")

def setup_logging(level):
    # setup logging
    logger.setLevel(level)
    proxy.logger.setLevel(level)
    utx.logger.setLevel(level)
    blocks.logger.setLevel(level)
    ch = logging.StreamHandler()
    ch.setLevel(level)
    ch.setFormatter(logging.Formatter('[%(name)s %(levelname)s] %(message)s'))
    logger.addHandler(ch)
    proxy.logger.addHandler(ch)
    utx.logger.addHandler(ch)
    blocks.logger.addHandler(ch)
    # clear loggers set by any imported modules
    logging.getLogger().handlers.clear()


def on_transfer_utx(wutx, txid, sig, pubkey, asset_id, timestamp, amount, fee, recipient, attachment):
    recipient = base58.b58encode(recipient)
    sender = utils.address_from_public_key(pubkey)

    logger.info("tx from %s to %s" % (sender, recipient))

    proxy.clear_address_from_cache(sender)
    proxy.clear_address_from_cache(recipient)

def on_block_transfer_tx(txid, sender, asset_id, timestamp, amount, fee, recipient, attachment):
    logger.info("block tx from %s to %s" % (sender, recipient))

    proxy.clear_address_from_cache(sender)
    proxy.clear_address_from_cache(recipient)

def sigint_handler(signum, frame):
    global keep_running
    logger.warning("SIGINT caught, attempting to exit gracefully")
    keep_running = False

def g_exception(g):
    try:
        g.get()
    except Exception as e:
        import traceback
        stack_trace = traceback.format_exc()
        msg = f"{e}\n---\n{stack_trace}"
        logger.error(msg)

keep_running = True
if __name__ == "__main__":
    setup_logging(logging.DEBUG)
    signal.signal(signal.SIGINT, sigint_handler)

    logger.info("starting greenlets")
    group = gevent.pool.Group()
    if "ADDR" in os.environ:
        wproxy = proxy.Proxy(addr=os.environ["ADDR"])
    else:
        wproxy = proxy.Proxy()
    wproxy.start(group)
    port = 6863
    if not cfg.testnet:
        port = 6868
    wutx = utx.WavesUTX(None, on_transfer_utx, on_block_transfer_tx, addr=cfg.node_p2p_host, port=port, testnet=cfg.testnet)
    wutx.start(group)
    blks = blocks.Blocks(on_block_transfer_tx, node_http_base_url=cfg.node_http_base_url)
    blks.start(group)
    logger.info("main loop")
    for g in group:
        g.link_exception(g_exception)
    while keep_running:
        gevent.sleep(1)
        # check if any essential greenlets are dead
        if len(group) < 3:
            msg = "one of our greenlets is dead X("
            logger.error(msg)
            break
    logger.info("stopping greenlets")
    wproxy.stop()
    wutx.stop()
    blks.stop()
