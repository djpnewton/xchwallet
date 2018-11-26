using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

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
    }
}