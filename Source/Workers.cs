using System;
using Unity.Jobs;
using Verse;

namespace Celsius
{
    internal class GridWorker : IJobParallelFor
    {
        public readonly TemperatureInfo info;
        public readonly int[][] arr;

        public GridWorker(int[][] arr, TemperatureInfo info)
        {
            this.info = info;
            this.arr = arr;
        }
        public void Execute(int index)
        {
            info.ProcessZone(arr[index], info.mouseCell);
        }
    }
}
