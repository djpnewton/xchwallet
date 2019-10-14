import base58
import pywaves
import pyblake2

def txid_from_txdata(serialized_txdata):
    txid = pyblake2.blake2b(serialized_txdata, digest_size=32).digest()
    return base58.b58encode(txid)

def address_from_public_key(public_key, b58encoded=False):
    if b58encoded:
        pubkey = base58.b58decode(public_key)
    else:
        pubkey = public_key
    unhashed_address = chr(1) + str(pywaves.CHAIN_ID) + pywaves.crypto.hashChain(pubkey)[0:20]
    addr_hash = pywaves.crypto.hashChain(pywaves.crypto.str2bytes(unhashed_address))[0:4]
    return base58.b58encode(pywaves.crypto.str2bytes(unhashed_address + addr_hash))
