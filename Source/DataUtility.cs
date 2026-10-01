using System;

namespace Celsius
{
    public static class DataUtility
    {
        public static string ArrayToString(float[] array)
        {
            if (array == null)
                return null;
            byte[] bytes = new byte[array.Length * sizeof(float)];
            Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        public static float[] StringToArray(string str)
        {
            byte[] bytes = Convert.FromBase64String(str);
            if (bytes.Length % sizeof(float) != 0)
                throw new FormatException("Celsius temperature data contains an incomplete float.");
            float[] array = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
            return array;
        }

        public static float[] Transpose(float[] array, int width)
        {
            if (array == null) return null;
            if (width <= 0 || array.Length % width != 0) throw new ArgumentException("Invalid thermal grid dimensions.");
            float[] result = new float[array.Length];
            int height = array.Length / width;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    result[x * height + y] = array[y * width + x];
            return result;
        }
    }
}
