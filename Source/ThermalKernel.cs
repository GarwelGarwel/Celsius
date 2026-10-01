using System;

namespace Celsius
{
    // Plain numeric data only. No Verse, Unity, game objects, random state, or allocations in Step.
    internal struct CellCoefficients
    {
        public float Left, Right, Down, Up, Environment, Terrain, TerrainReverse, Source;
        public byte Snow, Unroofed;
    }

    internal static class ThermalKernel
    {
        internal static float Harmonic(float a, float b) => a > 0 && b > 0 ? 2 * a * b / (a + b) : 0;

        // A convex explicit step is stable and cannot overshoot the input temperatures (without sources).
        // Each shared edge uses one conductance; reciprocal rates differ only by heat capacity.
        internal static unsafe void Step(float[] input, float[] output, float[] ground, float[] nextGround,
            CellCoefficients[] coefficients, int width, float dt, float outdoor, float rain,
            double[] snowMelt, int[] snowBlocks, int firstRow = 0, int lastRow = -1)
        {
            int count = input.Length;
            fixed (float* src = input, dst = output, soil = ground, soilOut = nextGround)
            fixed (CellCoefficients* c = coefficients)
            {
                int endRow = lastRow < 0 ? count / width : lastRow;
                for (int row = firstRow * width; row < endRow * width; row += width)
                    for (int x = 0; x < width; x++)
                    {
                        int i = row + x;
                        float t = src[i];
                        CellCoefficients* k = c + i;
                        float flux = k->Environment * (outdoor - t) + k->Source;
                        if (x > 0) flux += k->Left * (src[i - 1] - t);
                        if (x + 1 < width) flux += k->Right * (src[i + 1] - t);
                        if (row > 0) flux += k->Down * (src[i - width] - t);
                        if (i + width < count) flux += k->Up * (src[i + width] - t);
                        if (soil != null)
                        {
                            float g = soil[i];
                            if (k->Terrain > 0)
                            {
                                flux += k->Terrain * (g - t);
                                soilOut[i] = g + dt * k->TerrainReverse * (t - g);
                            }
                            else soilOut[i] = g;
                        }
                        float next = t + dt * flux;
                        dst[i] = next;
                        if (snowMelt != null && k->Snow != 0 && next > 0)
                        {
                            double amount = 0.000000348 * next * Math.Min(next, 10) * dt * (k->Unroofed != 0 ? rain : 1);
                            // One writer. The release flag publishes the preceding counter writes.
                            System.Threading.Volatile.Write(ref snowMelt[i], snowMelt[i] + amount);
                            System.Threading.Volatile.Write(ref snowBlocks[i >> 8], 1);
                        }
                    }
            }
        }
    }

    // Main-thread pending edits and an immutable in-flight batch. Absolute writes stay absolute;
    // incremental heat is applied once, and never races a worker's temperature buffer swap.
    internal sealed class HeatBatch
    {
        internal readonly byte[] Mode;
        internal readonly float[] Value;
        internal readonly int[] Cells;
        internal int Count;
        internal HeatBatch(int count) { Mode = new byte[count]; Value = new float[count]; Cells = new int[count]; }
        internal float At(int i, float original)
        {
            byte mode = Mode[i];
            if (mode == 0) return original;
            return mode == 2 ? Value[i] : original + Value[i];
        }
        internal void Add(int i, float delta)
        {
            if (Mode[i] == 0) { Cells[Count++] = i; Mode[i] = 1; }
            Value[i] += delta;
        }
        internal void Set(int i, float value)
        {
            if (Mode[i] == 0) Cells[Count++] = i;
            Mode[i] = 2; Value[i] = value;
        }
        internal void Apply(float[] temperatures)
        {
            for (int k = 0; k < Count; k++) { int i = Cells[k]; temperatures[i] = At(i, temperatures[i]); }
        }
        internal void Clear()
        {
            for (int k = 0; k < Count; k++) { int i = Cells[k]; Mode[i] = 0; Value[i] = 0; }
            Count = 0;
        }
    }
}
