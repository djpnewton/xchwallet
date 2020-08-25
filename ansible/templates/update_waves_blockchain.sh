#!/bin/bash

set -e

# setup vars
WAVES_DIR=/home/waves/waves/testnet
BLOCKCHAIN_SERVER=http://blockchain-testnet.wavesnodes.com
{% if not testnet %}
WAVES_DIR=/home/waves/waves/mainnet
BLOCKCHAIN_SERVER=http://blockchain.wavesnodes.com
{% endif %}

# go to waves directory
echo WAVES_DIR: $WAVES_DIR
cd $WAVES_DIR

echo ::download blockchain
[ -f "blockchain_last.tar.SHA1SUM" ] && rm blockchain_last.tar.SHA1SUM
[ -f "blockchain_last.tar" ] && rm blockchain_last.tar
echo BLOCKCHAIN_SERVER: $BLOCKCHAIN_SERVER
wget $BLOCKCHAIN_SERVER/blockchain_last.tar.SHA1SUM
wget $BLOCKCHAIN_SERVER/blockchain_last.tar

echo ::verify sha1sum
sed 's/\/opt\/blockchain\/blockchain_last.tar/\.\/blockchain_last.tar/' blockchain_last.tar.SHA1SUM > blockchain_last.tar.SHA1SUM.mod
sha1sum -c blockchain_last.tar.SHA1SUM.mod

echo ::stop waves
systemctl stop waves

echo ::copy old data dir
[ -d "./data.old" ] && rm -r data.old
mv data data.old

echo ::create new data dir
tar xvf blockchain_last.tar
chown waves:waves data -R

echo ::start waves
systemctl start waves
