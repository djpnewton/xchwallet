#!/usr/bin/env python3

import sys
import os
import threading
import time
from flask import Flask, request, jsonify
import gevent
from gevent.pywsgi import WSGIServer
from database import db_session, init_db
from models import Account, Block
from config import Cfg
from eth_blocks import get_pending_txs
from block_check import BlockCheckGreenlet

cfg = Cfg()
init_db()
account_lock = threading.Lock()
app = Flask("gethtxscan")
if not app.debug:
    import logging
    from logging.handlers import RotatingFileHandler
    file_handler = RotatingFileHandler(os.path.join(os.path.dirname(os.path.realpath(__file__)), "gethtxscan.log"), maxBytes=1024 * 1024 * 100, backupCount=20)
    file_handler.setLevel(logging.INFO)
    formatter = logging.Formatter("%(asctime)s - %(name)s - %(levelname)s - %(message)s")
    file_handler.setFormatter(formatter)
    app.logger.addHandler(file_handler)
    app.logger.setLevel(logging.INFO)

@app.errorhandler(Exception)
def exceptions(e):
    app.logger.error(
            """
Request:   {method} {path}
IP:        {ip}
            """.format(
                method = request.method,
                path = request.path,
                ip = request.remote_addr,
            ), exc_info=sys.exc_info()
        )
    return "Internal Server Error", 500

@app.teardown_appcontext
def shutdown_session(exception=None):
    db_session.remove()

@app.route("/")
def root():
    last_block = Block.last_block(db_session)
    num_accounts = Account.count(db_session)
    return "last block: %d, %s<br/>number of accounts tracked: %d" % (last_block.num, last_block.hash.hex(), num_accounts)

@app.route("/watch_account/<account>")
def watch_account(account):
    with account_lock:
        acct = Account.from_address(db_session, account.lower())
        acct.active = True
        db_session.add(acct)
        db_session.commit()
    return jsonify(acct.to_json())

@app.route("/stop_account/<account>")
def stop_account(account):
    with account_lock:
        acct = Account.from_address(db_session, account.lower())
        acct.active = False
        db_session.add(acct)
        db_session.commit()
    return jsonify(acct.to_json())

@app.route("/list_transactions/<account>")
def list_transactions(account):
    acct = Account.from_address(db_session, account.lower())
    txs = []
    for tx in acct.transactions:
        block_num = Block.tx_block_num(db_session, tx.block_id)
        txs.append(tx.to_json(block_num))
    return jsonify(txs)

@app.route("/incomming_value/<account>")
def incomming_value(account):
    acct = Account.from_address(db_session, account.lower())
    value = 0
    for tx in acct.transactions:
        value += tx.value
    return jsonify(str(value))

@app.route("/has_transactions", methods=("POST",))
def has_transactions():
    start = time.time()
    addresses = request.form["addresses"]
    if addresses:
        addresses = addresses.split(",")
    else:
        addresses = []
    addrs_with_txs = Account.has_txs(db_session, addresses)
    app.logger.info("*has_transactions* check took %f seconds (%d checked, %d with tx)" % (time.time() - start, len(addresses), len(addrs_with_txs)))
    return jsonify(addrs_with_txs)

@app.route("/last_blocknum")
def last_blocknum():
    last_block = Block.last_block(db_session)
    if last_block:
        return jsonify({"last_blocknum": last_block.num})
    return jsonify({"last_blocknum": 0})

@app.route("/num_pending_txs")
def num_pending_txs():
    return jsonify({"num_pending_txs": len(get_pending_txs())})

@app.route("/active_accounts")
def active_accounts():
    n = Account.count_active(db_session)
    return jsonify({"active_accounts": n})

@app.route("/all_active_addresses")
def all_active_addresses():
    addresses = Account.all_active_addresses(db_session)
    return jsonify({"active_addresses": addresses})

if __name__ == '__main__':
    http_server = WSGIServer(('', 5001), app)
    srv_greenlet = gevent.spawn(http_server.start)
    block_check = BlockCheckGreenlet(app.logger, account_lock)
    block_check.start()
    try:
        gevent.joinall([srv_greenlet, block_check])
    except KeyboardInterrupt:
        print("Exiting")
