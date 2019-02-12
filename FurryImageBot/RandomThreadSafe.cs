using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FurryImageBot
{
    class RandomThreadSafe
    {
        private static RNGCryptoServiceProvider RNGCryptoServiceProvider = new RNGCryptoServiceProvider();
        [ThreadStatic]
        private static Random Random;

        public static int Next(int minValue, int maxValue)
        {
            Random inst = Random;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                RNGCryptoServiceProvider.GetBytes(buffer);
                Random = inst = new Random(BitConverter.ToInt32(buffer, 0));
            }
            return inst.Next(minValue, maxValue);
        }
    }
}
