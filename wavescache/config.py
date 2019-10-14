import os
import configparser

def get_filename():
    return os.path.join(os.path.dirname(os.path.realpath(__file__)), "config.cfg")

def read_cfg():
    cp = configparser.ConfigParser()
    cp.read(get_filename())

    cfg = type("cfg", (object,), {})()

    cfg.testnet = cp.getboolean("network", "testnet")
    cfg.node_http_base_url = cp["node"]["node_http_base_url"]
    cfg.node_p2p_host = cp["node"]["node_p2p_host"]

    return cfg

def set_testnet(value):
    # write address
    import re
    with open(get_filename()) as f:
        data = f.read()
    def subtestnet(m):
        return m.group(1) + value
    pattern = "^(testnet=)(.*)"
    data = re.sub(pattern, subtestnet, data, flags=re.MULTILINE)
    with open(get_filename(), "w") as f:
        f.write(data)
