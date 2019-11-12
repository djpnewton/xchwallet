#!/usr/bin/python3

import sys
import logging
import json

import gevent
from gevent.pywsgi import WSGIServer
from flask import Flask, request, Response
import requests
from requests.adapters import HTTPAdapter
from requests.packages.urllib3.util.retry import Retry

import config

cfg = config.read_cfg()
app = Flask(__name__)
logger = logging.getLogger(__name__)

cache = {}
address_index = {}

def add_address_to_cache(addr, path):
    if addr:
        logger.info("indexing cache: " + addr + ", " + path)
        if not addr in address_index:
            address_index[addr] = {}
        address_index[addr][path] = True

def clear_address_from_cache(addr):
    if addr in address_index:
        logger.info("clearing cache: " + addr)
        for key in address_index[addr].keys():
            if key in cache:
                del cache[key]
                logger.info("  - " + key)
        del address_index[addr]

def create_response(path, addr=None):
    if path in cache:
        logger.info("using cache: " + path)
        body, status_code, content_type = cache[path]
        res = Response(response=body, status=200, mimetype=content_type)
    else:
        logger.info("proxying path: " + path)
        url = "%s/%s" % (cfg.node_http_base_url, path)
        #args = dict(request.args.items())
        response = requests.get(url)#, params=args)
        content_type = response.headers["Content-Type"]
        res = Response(response=response.text, status=response.status_code, mimetype=content_type)
        # cache value for next time if response is ok
        if response.ok:
            cache[path] = (response.text, response.status_code, content_type) 
            add_address_to_cache(addr, path)
    return res

@app.route('/assets/details/<txid>')
@app.route('/transactions/info/<txid>')
def proxy_asset_tx_info(txid):
    path = request.full_path[1:]
    return create_response(path)

@app.route('/transactions/address/<addr>/limit/<limit>')
def proxy_addr_txs(addr, limit):
    path = request.full_path[1:]
    return create_response(path, addr)

@app.route('/', defaults={'path': ''})
@app.route('/<path:path>')
def proxy(path):
    """Proxy connections to the API"""
    logger.info("proxying path: " + path)
    url = "%s/%s" % (cfg.node_http_base_url, path)
    args = dict(request.args.items())
    response = requests.get(url, params=args)
    res = Response(response=response.text, status=response.status_code, mimetype=response.headers["Content-Type"])
    return res

def get(url):
    with requests.Session() as s:
        retries = Retry(
            total=10,
            backoff_factor=0.2,
            status_forcelist=[500, 502, 503, 504])
        s.mount('http://', HTTPAdapter(max_retries=retries))
        s.mount('https://', HTTPAdapter(max_retries=retries))
        response = s.get(url)
        return response

class Proxy():

    def __init__(self, addr="127.0.0.1", port=5100):
        self.addr = addr
        self.port = port
        self.runloop_greenlet = None

    def start(self, group=None):
        def runloop():
            logger.info("Proxy runloop started")

            http_server = WSGIServer((self.addr, self.port), app)
            http_server.serve_forever()

        def start_greenlets():
            logger.info("starting Proxy runloop...")
            self.runloop_greenlet.start()

        # create greenlets
        self.runloop_greenlet = gevent.Greenlet(runloop)
        if group != None:
            group.add(self.runloop_greenlet)
        # start greenlets
        gevent.spawn(start_greenlets)

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

    proxy = Proxy()
    proxy.start()

    while 1:
        gevent.sleep(1)

    proxy.stop()
