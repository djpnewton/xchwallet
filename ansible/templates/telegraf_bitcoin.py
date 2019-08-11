#!/usr/bin/env python3

import argparse
import json
import base64

# requires requests
import requests

parser = argparse.ArgumentParser()
parser.add_argument("-t", "--testnet", action="store_true", dest="testnet", help="use testnet")

args = parser.parse_args()

# get local blockheight
local_blockheight = None
try:
    url = "http://localhost:8332"
    if args.testnet:
        url = "http://localhost:18332"
    payload = {
        "method": "getblockcount",
        "params": [],
        "jsonrpc": "2.0",
        "id": 0,
    }
    base64string = base64.b64encode(("%s:%s" % ("user", "pass")).encode()).decode()
    headers = {"Content-Type": "application/json", "Authorization": "Basic %s" % base64string}
    r = requests.post(url, data=json.dumps(payload), headers=headers)
    if r:
        local_blockheight = r.json()["result"]
except:
    pass

# get nbxplorer blockheight
nbxplorer_blockheight = None
nbxplorer_synced = None
try:
    url = "http://localhost:24444/v1/cryptos/BTC/status"
    r = requests.get(url)
    if r:
        doc = r.json()
        nbxplorer_blockheight = doc["chainHeight"]
        nbxplorer_synced = 1 if doc["isFullySynched"] else 0
except:
    pass

# get remote blockheight
remote_blockheight = None
try:
    url = "https://api.smartbit.com.au/v1/blockchain/blocks?limit=1"
    if args.testnet:
        url = "https://testnet-api.smartbit.com.au/v1/blockchain/blocks?limit=1"
    r = requests.get(url)
    if r:
        doc = r.json()
        if doc["success"]:
            remote_blockheight = doc["blocks"][0]["height"]
except:
    pass

# caluulate absolute difference between local and remote blockheight
diff_blockheight = abs(local_blockheight - remote_blockheight)

d = {"local_blockheight": local_blockheight, \
     "nbxplorer_blockheight": nbxplorer_blockheight, \
     "nbxplorer_synced": nbxplorer_synced, \
     "remote_blockheight": remote_blockheight, \
     "diff_blockheight": diff_blockheight}
print(json.dumps(d))
