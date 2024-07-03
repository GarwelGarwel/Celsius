using System;
using Unity.Jobs;

namespace Celsius
{
    internal class GridWorker : IJobParallelFor
    {
        public readonly Tuple<int, int>[] arr;
        public readonly TemperatureInfo info;
        public readonly int cell;

        public GridWorker(Tuple<int, int>[] arr, TemperatureInfo info, int cell)
        {
            this.arr = arr;
            this.info = info;
            this.cell = cell;
        }
        public void Execute(int index)
        {
            var tuple = arr[index];
            info.ProcessColumns(tuple.Item1, tuple.Item2, cell);
        }
    }
}
