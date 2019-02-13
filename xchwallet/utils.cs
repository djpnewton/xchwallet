using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace xchwallet
{
    public static class Utils
    {
        public static byte[] ParseHexString(string hex)
        {
            if ((hex.Length % 2) != 0)
                throw new ArgumentException("Invalid length: " + hex.Length);

            if (hex.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                hex = hex.Substring(2);

            int arrayLength = hex.Length / 2;
            byte[] byteArray = new byte[arrayLength];
            for (int i = 0; i < arrayLength; i++)
                byteArray[i] = byte.Parse(hex.Substring(i*2, 2), NumberStyles.HexNumber);

            return byteArray;
        }

        public static string CreateDepositCode(int length=10, bool allowLetters=false)
        {
            var code = new StringBuilder();
            var rng = new RNGCryptoServiceProvider();
            var rnd = new byte[1];
            int n = 0;
            while (n < length) {
                rng.GetBytes(rnd);
                var c = (char)rnd[0];
                if ((Char.IsDigit(c) || (allowLetters && Char.IsLetter(c))) && rnd[0] < 127) {
                    ++n;
                    code.Append(char.ToUpper(c));
                }
            }
            return code.ToString();
        }
    }
}