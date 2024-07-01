using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Celsius
{
    [BurstCompile]
    internal struct RegularWorkerReference : IJobParallelFor
    {
        public readonly Tuple<int, int>[] arr;
        public TemperatureInfo info;

        public RegularWorkerReference(Tuple<int, int>[] arr, TemperatureInfo info)
        {
            this.arr = arr;
            this.info = info;
        }
        public void Execute(int index)
        {
            var tuple = arr[index];
            info.ProcessColumns(tuple.Item1, tuple.Item2);
        }
    }
}
