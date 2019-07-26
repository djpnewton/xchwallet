#!/usr/bin/env python3

import argparse
import json

# requires requests
import requests

parser = argparse.ArgumentParser()
parser.add_argument("-t", "--testnet", action="store_true", dest="testnet", help="use testnet")

args = parser.parse_args()

# get local blockheight
local_blockheight = None
try:
    url = "http://localhost:6869/blocks/height"
    r = requests.get(url)
    if r:
        doc = r.json()
        local_blockheight = doc["height"]
except:
    pass

# get remote blockheight
remote_blockheight = None
try:
    url = "https://nodes.wavesnodes.com/blocks/height"
    if args.testnet:
        url = "https://testnet1.wavesnodes.com/blocks/height"
    r = requests.get(url)
    if r:
        doc = r.json()
        remote_blockheight = doc["height"]
except:
    pass

d = {"local_blockheight": local_blockheight, \
      "remote_blockheight": remote_blockheight}
print(json.dumps(d))
