using System;
using System.Collections.Generic;

namespace Celsius
{
    public static class DataUtility
    {
        public static string ArrayToString(float[] array)
        {
            if (array == null)
                return null;
            List<byte> bytes = new List<byte>(array.Length * sizeof(float));
            for (int i = 0; i < array.Length; i++)
                bytes.AddRange(BitConverter.GetBytes(array[i]));
            return Convert.ToBase64String(bytes.ToArray());
        }

        public static float[] StringToArray(string str)
        {
            byte[] bytes = Convert.FromBase64String(str);
            float[] array = new float[bytes.Length / sizeof(float)];
            for (int i = 0; i < array.Length; i++)
                array[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
            return array;
        }

        public static float[] Transpose(float[] array, int width)
        {
            float[] result = new float[array.Length];
            int height = array.Length / width;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    result[x * height + y] = array[y * width + x];
            return result;
        }
    }
}
