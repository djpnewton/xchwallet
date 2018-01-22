import time
import gevent
from gevent import Greenlet, GreenletExit
from database import db_session
from models import Account, Block
from config import Cfg
from eth_blocks import get_current_block_number, get_block_hash_and_txs, get_block_hash, scan_pending_txs
from eth_blocks import pending_tx_filter, check_tx_filter

cfg = Cfg()

class BlockCheckGreenlet(Greenlet):

    def __init__(self, logger, account_lock):
        Greenlet.__init__(self)
        self.logger = logger
        self.account_lock = account_lock
        self.delay = 5
        self.keep_running = True

    def stop_processing(self):
        self.keep_running = False

    def _run(self):
        fltr = pending_tx_filter()
        tx_not_seen_counter = 0
        while True:
            gevent.sleep(self.delay)
            # check for new pending transactions
            if not check_tx_filter(self.logger, fltr):
                # replace pending tx filter if it gets stale
                tx_not_seen_counter += 1
                if tx_not_seen_counter > 10:
                    fltr = pending_tx_filter()
            # check for new blocks
            self.block_check()

    def block_check(self):
        # set current scanned block
        last_block = Block.last_block(db_session)
        if last_block:
            current_scanned_block = last_block.num
        else:
            current_scanned_block = cfg.startblock
            # check if no accounts created so we can skip to the most recent block
            with self.account_lock:
                acct_count = Account.count(db_session)
                if acct_count == 0:
                    current_scanned_block = get_current_block_number() - 1

        # check for reorgs and invalidate any blocks (and associated txs)
        block_num = current_scanned_block
        block = Block.from_number(db_session, current_scanned_block)
        if block:
            any_reorgs = False
            block_hash = get_block_hash(block_num)
            if not block_hash:
                self.logger.error("unable to get hash for block %d" % block_num)
                return
            while block_hash != block.hash:
                self.logger.info("block %d hash does not match current blockchain, must have been reorged" % block_num)
                block.set_reorged(db_session)
                any_reorgs = True
                # decrement block_num, set new current_scanned_block
                block_num -= 1
                current_scanned_block = block_num
                # now do the previous block
                block = Block.from_number(db_session, block_num)
                if not block:
                    break
                block_hash = get_block_hash(block_num)
            if any_reorgs:
                db_session.commit()

        # get address list from db
        with self.account_lock:
            addresses = Account.all_active_addresses(db_session)

        # scan for new blocks
        current_block = get_current_block_number()
        while current_scanned_block < current_block and self.keep_running:
            block_num = current_scanned_block + 1
            start = time.time()
            block_hash, txs, tx_count = get_block_hash_and_txs(block_num, addresses)
            # check for reorged blocks now reorged *back* into the main chain
            block = Block.from_hash(db_session, block_hash)
            if block:
                self.logger.info("block %s (was #%d) now un-reorged" % (block_hash.hex(), block.num))
                block.num = block_num
                block.reorged = False
            else:
                block = Block(block_num, block_hash)
                db_session.add(block)
                db_session.flush()
            for key in txs.keys():
                self.logger.info("adding txs for " + key)
                for tx in txs[key]:
                    self.logger.info(" - %s, %s" % (tx["hash"].hex(), tx["value"]))
                Account.add_txs(db_session, key, block.id, txs[key])
            db_session.commit()
            current_scanned_block = block_num
            self.logger.info("#block# %d scan took %f seconds (%d addresses, %d txs)" % (block_num, time.time() - start, len(addresses), tx_count))

        # scan for pending transactions
        start = time.time()
        txs, tx_count = scan_pending_txs(addresses)
        for key in txs.keys():
            self.logger.info("adding txs for " + key)
            for tx in txs[key]:
                self.logger.info(" - %s, %s" % (tx["hash"].hex(), tx["value"]))
            Account.add_txs(db_session, key, None, txs[key])
        db_session.commit()
        self.logger.info("!pending! tx scan took %f seconds (%d addresses, %d txs)" % (time.time() - start, len(addresses), tx_count))