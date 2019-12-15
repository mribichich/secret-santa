namespace SecretSanta {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;

    public static class ShuffleList {
        public static IList<T> Shuffle<T>(IList<T> list) {
            var provider = new RNGCryptoServiceProvider();

            var listCopy = new List<T>(list);

            var n = listCopy.Count;

            while (n > 1) {
                var box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (byte.MaxValue / n)));
                var k = box[0] % n;
                n--;
                var value = listCopy[k];
                listCopy[k] = listCopy[n];
                listCopy[n] = value;
            }

            return listCopy;
        }

        public static IList<T> ShuffleTimes<T>(int times, IList<T> list) {
            if (times == 0) {
                return list;
            }

            var newList = Shuffle(list);

            return ShuffleTimes<T>(times - 1, newList.Reverse().ToList());
        }

        public static int Random(int total) {
            var provider = new RNGCryptoServiceProvider();

            var box = new byte[11];
            provider.GetBytes(box);

            var middle = box[5];
            var middleProd = (double)middle / byte.MaxValue;
            var middleReal = middleProd * total;

            var index = (int)Math.Round(middleReal);

            return index < total ? index : index - 1;
        }
    }
}
