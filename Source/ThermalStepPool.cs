using System;
using System.Threading;

namespace Celsius
{
    // A bounded persistent team. Each thread owns a contiguous strip, while reading an immutable
    // previous-temperature bitmap. No thread-pool scheduling, per-cell locks, or game callbacks.
    internal sealed class ThermalStepPool : IDisposable
    {
        readonly Thread[] threads;
        readonly AutoResetEvent[] wakes;
        readonly CountdownEvent finished;
        readonly int width, height, workers;
        volatile bool stopping;
        Exception error;
        float[] input, output, ground, nextGround;
        CellCoefficients[] coefficients;
        float dt, outdoor, rain;
        double[] snow;
        int[] blocks;
        internal ThermalStepPool(int width, int height, int workers)
        {
            this.width = width; this.height = height; this.workers = Math.Max(1, Math.Min(workers, height));
            threads = new Thread[this.workers - 1]; wakes = new AutoResetEvent[threads.Length];
            finished = new CountdownEvent(threads.Length);
            for (int i = 0; i < threads.Length; i++)
            {
                int slice = i; wakes[i] = new AutoResetEvent(false);
                threads[i] = new Thread(() => Loop(slice)) { IsBackground = true, Name = "Celsius numeric tile " + i };
                threads[i].Start();
            }
        }
        void Slice(int id) => ThermalKernel.Step(input, output, ground, nextGround, coefficients,
            width, dt, outdoor, rain, snow, blocks, height * id / workers, height * (id + 1) / workers);
        void Loop(int id)
        {
            while (true)
            {
                wakes[id].WaitOne(); if (stopping) return;
                try { Slice(id); } catch (Exception e) { Interlocked.CompareExchange(ref error, e, null); }
                finally { finished.Signal(); }
            }
        }
        internal void Step(float[] input, float[] output, float[] ground, float[] nextGround,
            CellCoefficients[] coefficients, float dt, float outdoor, float rain, double[] snow, int[] blocks)
        {
            this.input = input; this.output = output; this.ground = ground; this.nextGround = nextGround;
            this.coefficients = coefficients; this.dt = dt; this.outdoor = outdoor; this.rain = rain;
            this.snow = snow; this.blocks = blocks;
            finished.Reset(threads.Length);
            foreach (AutoResetEvent wake in wakes) wake.Set();
            try { Slice(workers - 1); }
            finally { finished.Wait(); }
            if (error != null) throw new InvalidOperationException("Thermal tile failed", error);
        }
        public void Dispose()
        {
            stopping = true;
            foreach (AutoResetEvent wake in wakes) wake.Set();
            foreach (Thread thread in threads) thread.Join();
            foreach (AutoResetEvent wake in wakes) wake.Dispose();
            finished.Dispose();
        }
    }
}
