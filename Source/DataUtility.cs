using System;
using System.Collections.Generic;

namespace Celsius
{
    public static class DataUtility
    {
        public static string ArrayToString(float[,] array)
        {
            if (array == null)
                return null;
            List<byte> bytes = new List<byte>(array.Length * sizeof(float));
            for (int x = 0; x < array.GetLength(0); x++)
                for (int z = 0; z < array.GetLength(1); z++)
                    bytes.AddRange(BitConverter.GetBytes(array[x, z]));
            return Convert.ToBase64String(bytes.ToArray());
        }

        public static float[,] StringToArray(string str, int sizeX)
        {
            byte[] bytes = Convert.FromBase64String(str);
            float[,] array = new float[sizeX, bytes.Length / sizeX / sizeof(float)];
            int i = 0;
            for (int x = 0; x < sizeX; x++)
                for (int z = 0; z < array.GetLength(1); z++)
                {
                    array[x, z] = BitConverter.ToSingle(bytes, i);
                    i += sizeof(float);
                }
            return array;
        }
    }
}
