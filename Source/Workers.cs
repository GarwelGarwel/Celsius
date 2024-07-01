using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Verse;

namespace Celsius
{
    [BurstCompile]
    internal struct RegularWorkerReference : IJobParallelFor
    {
        public readonly Tuple<int, int>[] arr;
        public readonly TemperatureInfo info;
        public readonly int cell;

        public RegularWorkerReference(Tuple<int, int>[] arr, TemperatureInfo info, int cell)
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
