#!/usr/bin/env python3

import argparse
import json

# requires requests
import requests

parser = argparse.ArgumentParser()
parser.add_argument("-t", "--testnet", action="store_true", dest="testnet", help="use testnet")

args = parser.parse_args()

# RedRat ZAP balance
redrat_zap_balance = None
try:
  url = "https://nodes.wavesnodes.com/assets/balance/3PBJopU1CzKK53r7u1XdsG2bqoGLGQTwxc6/9R3iLi4qGLVWKc16Tg98gmRvgg1usGEYd7SgC1W5D6HB"
  if args.testnet:
      url = "https://testnode1.wavesnodes.com/assets/balance/3MsKcPckNDdCfvZJq2PGtDG7Dx8d9U8qMwF/CgUrFtinLXEbJwJVjwwcppk4Vpz1nMmR3H5cQaDcUcfe"
  r = requests.get(url)
  if r:
      doc = r.json()
      redrat_zap_balance = doc["balance"]/100
except:
  pass

d = {"redrat_zap_balance": redrat_zap_balance}
print(json.dumps(d))
