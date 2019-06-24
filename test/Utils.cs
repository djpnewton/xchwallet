using System;
using System.Collections.Generic;
using System.Text;
using RestSharp;

namespace test
{
    public static class Utils
    {
        public static IRestResponse SmartBtcComAuPushTx(bool mainnet, string txHex)
        {
            var url = "https://api.smartbit.com.au/v1/blockchain";
            if (!mainnet)
                url = "https://testnet-api.smartbit.com.au/v1/blockchain";
            var client = new RestClient(url);
            var req = new RestRequest("/pushtx", DataFormat.Json);
            var body = string.Format("{{\"hex\": \"{0}\"}}", txHex);
            req.AddJsonBody(body);
            return client.Post(req);
        }

        public static IRestResponse SmartBtcComAuRequest(bool mainnet, string endpoint)
        {
            var url = "https://api.smartbit.com.au/v1/blockchain";
            if (!mainnet)
                url = "https://testnet-api.smartbit.com.au/v1/blockchain";
            var client = new RestClient(url);
            var req = new RestRequest(endpoint, DataFormat.Json);
            return client.Get(req);
        }

        public static IRestResponse BtcApsComRequest(bool mainnet, string endpoint)
        {
            var url = "https://api.bitaps.com/btc/v1/blockchain";
            if (!mainnet)
                url = "https://api.bitaps.com/btc/testnet/v1/blockchain";
            var client = new RestClient(url);
            var req = new RestRequest(endpoint, DataFormat.Json);
            return client.Get(req);
        }
    }
}
