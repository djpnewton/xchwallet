import os
import configparser

class Cfg():
    def __init__(self):
        dir_path = os.path.dirname(os.path.realpath(__file__))
        configParser = configparser.RawConfigParser()
        configParser.read("%s/config.ini" % dir_path)

        self.testnet = configParser.getboolean("main", "testnet")
        if self.testnet:
            self.startblock = configParser.getint("main", "startblock_testnet")
        else:
            self.startblock = configParser.getint("main", "startblock")
        self.geth_uri = configParser.get("main", "geth_uri")
