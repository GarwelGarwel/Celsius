using System;
using System.Collections.Generic;
using Verse;

namespace Celsius
{
    public partial class TemperatureInfo
    {
        CellCoefficients[] coefficients;
        float[] capacity, denominator, environmentWeight, terrainCapacity, terrainConductivity, terrainFlow;
        readonly HashSet<int> dirtyCells = new HashSet<int>();
        readonly HashSet<int> dirtySnow = new HashSet<int>();
        bool allDirty = true, rebuilding, sourcesDirty;
        float maximumRate;
        internal long CoefficientCellsRefreshed;

        // Called on the game thread by change notifications, never from the solver.
        public void InvalidateCell(int index)
        {
            if (thermalProperties == null || index < 0 || index >= thermalProperties.Length) return;
            thermalProperties[index] = null;
            if (!allDirty) dirtyCells.Add(index);
        }
        public void InvalidateCell(IntVec3 cell)
        {
            if (cell.InBounds(map)) InvalidateCell(map.cellIndices.CellToIndex(cell));
        }
        internal void InvalidateSnow(IntVec3 cell)
        {
            if (!initialized || !cell.InBounds(map)) return;
            int i = map.cellIndices.CellToIndex(cell);
            bool present = map.snowGrid.GetDepth(cell) > 0;
            if ((coefficients[i].Snow != 0) != present) dirtySnow.Add(i);
        }
        internal void InvalidateRooms()
        {
            roomsDirty = true;
            sourcesDirty = true;
        }
        readonly HashSet<int> heatTerrainCells = new HashSet<int>();

        void AllocateSnapshot(int n)
        {
            coefficients = new CellCoefficients[n]; capacity = new float[n]; denominator = new float[n];
            environmentWeight = new float[n]; terrainCapacity = new float[n];
            terrainConductivity = new float[n]; terrainFlow = new float[n];
        }

        void CaptureCell(int i)
        {
            CoefficientCellsRefreshed++;
            ThermalProps p = GetThermalPropertiesAt(i);
            capacity[i] = Math.Max(0.0001f, p.heatCapacity);
            RoofDef roof = map.roofGrid.RoofAt(i);
            float e = 0, ignored = 0;
            TemperatureUtility.CalculateHeatTransferEnvironment(0, p, roof, ref ignored, ref e);
            environmentWeight[i] = e;
            TerrainDef terrain = map.terrainGrid.TerrainAt(i);
            ThermalProps soil = Settings.FreezingAndMeltingEnabled ? terrain?.GetTerrainThermalProperties()?.GetThermalProps() : null;
            terrainCapacity[i] = soil != null ? Math.Max(0, soil.heatCapacity) : 0;
            terrainConductivity[i] = soil?.conductivity ?? 0;
            terrainFlow[i] = soil != null && soil.heatCapacity > 0 ? soil.HeatFlow : 0;
            TerrainThermalProperties phase = terrain?.GetTerrainThermalProperties();
            phaseDirection[i] = (byte)(phase?.phaseTransition ?? PhaseTransitionType.None);
            phaseThreshold[i] = phase?.transitionTemperature ?? float.NaN;
            if (terrainTemperatures != null && terrainCapacity[i] > 0 && float.IsNaN(terrainTemperatures[i]))
                terrainTemperatures[i] = temperatures[i];
            // Newly built/removed materials are external reservoirs; phase changes preserve sensible
            // energy explicitly in ApplyWorldEffects. Do not invent heat when a heat source is present.
            coefficients[i].Source = 0;
            if (terrain != null && terrain.heatPerTick != 0)
            {
                heatTerrainCells.Add(i);
                Room room = map.cellIndices.IndexToCell(i).GetRoom(map);
                if (terrain.heatPerTick < 0 || room == null || room.UsesOutdoorTemperature)
                    coefficients[i].Source = terrain.heatPerTick * Settings.HeatPushEffect / capacity[i];
            }
            else heatTerrainCells.Remove(i);
            coefficients[i].Snow = (byte)(map.snowGrid.GetDepth(map.cellIndices.IndexToCell(i)) > 0 ? 1 : 0);
            coefficients[i].Unroofed = (byte)(roof == null ? 1 : 0);
        }

        float Weight(int from, int to)
        {
            float energy = 0, weight = 0;
            TemperatureUtility.CalculateHeatTransferCells(0, thermalProperties[to], thermalProperties[from].airflow, ref energy, ref weight);
            return weight;
        }
        void SetDenominator(int i)
        {
            int w = map.Size.x, x = i % w;
            float d = thermalProperties[i].HeatFlow + environmentWeight[i] + terrainFlow[i];
            if (x > 0) d += Weight(i, i - 1);
            if (x + 1 < w) d += Weight(i, i + 1);
            if (i >= w) d += Weight(i, i - w);
            if (i + w < temperatures.Length) d += Weight(i, i + w);
            denominator[i] = Math.Max(d, 1e-20f);
        }
        float Conductance(int i, int j)
        {
            float a = capacity[i] * thermalProperties[i].conductivity * Weight(i, j) / denominator[i];
            float b = capacity[j] * thermalProperties[j].conductivity * Weight(j, i) / denominator[j];
            return ThermalKernel.Harmonic(a, b) / Settings.ThermalReferenceTicks;
        }
        void SetCoefficients(int i)
        {
            int w = map.Size.x, x = i % w;
            CellCoefficients c = coefficients[i];
            c.Left = x > 0 ? Conductance(i, i - 1) / capacity[i] : 0;
            c.Right = x + 1 < w ? Conductance(i, i + 1) / capacity[i] : 0;
            c.Down = i >= w ? Conductance(i, i - w) / capacity[i] : 0;
            c.Up = i + w < temperatures.Length ? Conductance(i, i + w) / capacity[i] : 0;
            c.Environment = thermalProperties[i].conductivity * environmentWeight[i] / denominator[i] / Settings.ThermalReferenceTicks;
            c.Terrain = c.TerrainReverse = 0;
            if (terrainCapacity[i] > 0)
            {
                float a = capacity[i] * thermalProperties[i].conductivity * terrainFlow[i] / denominator[i];
                float b = terrainCapacity[i] * terrainConductivity[i] * thermalProperties[i].HeatFlow /
                    Math.Max(1e-20f, terrainFlow[i] + thermalProperties[i].HeatFlow);
                float conductance = ThermalKernel.Harmonic(a, b) / Settings.ThermalReferenceTicks;
                c.Terrain = conductance / capacity[i]; c.TerrainReverse = conductance / terrainCapacity[i];
            }
            maximumRate = Math.Max(maximumRate, Math.Max(c.TerrainReverse, c.Left + c.Right + c.Down + c.Up + c.Environment + c.Terrain));
            coefficients[i] = c;
        }
        void AddNeighbours(HashSet<int> set, int i)
        {
            int w = map.Size.x, x = i % w;
            set.Add(i);
            if (x > 0) set.Add(i - 1);
            if (x + 1 < w) set.Add(i + 1);
            if (i >= w) set.Add(i - w);
            if (i + w < temperatures.Length) set.Add(i + w);
        }
        readonly HashSet<int> dirtyDenominators = new HashSet<int>(), dirtyCoefficients = new HashSet<int>();
        void RefreshSnapshot()
        {
            if (rebuilding) return;
            if (!allDirty && !sourcesDirty && dirtyCells.Count == 0 && dirtySnow.Count == 0) return;
            rebuilding = true;
            try
            {
                if (sourcesDirty)
                {
                    sourcesDirty = false;
                    // Defer this to the idle snapshot boundary; room rebuilding can itself send
                    // notifications while CaptureCell is running.
                    if (!allDirty) foreach (int i in heatTerrainCells) dirtyCells.Add(i);
                }
                if (allDirty)
                {
                    maximumRate = 0;
                    for (int i = 0; i < temperatures.Length; i++) CaptureCell(i);
                    for (int i = 0; i < temperatures.Length; i++) SetDenominator(i);
                    for (int i = 0; i < temperatures.Length; i++) SetCoefficients(i);
                    allDirty = false;
                }
                else if (dirtyCells.Count > 0)
                {
                    // A property affects neighbouring denominators, then their incident edges.
                    foreach (int i in dirtyCells) { CaptureCell(i); AddNeighbours(dirtyDenominators, i); }
                    foreach (int i in dirtyDenominators) { SetDenominator(i); AddNeighbours(dirtyCoefficients, i); }
                    foreach (int i in dirtyCoefficients) SetCoefficients(i);
                    dirtyDenominators.Clear(); dirtyCoefficients.Clear();
                }
                dirtyCells.Clear();
                foreach (int i in dirtySnow)
                    coefficients[i].Snow = (byte)(map.snowGrid.GetDepth(map.cellIndices.IndexToCell(i)) > 0 ? 1 : 0);
                dirtySnow.Clear();
            }
            finally { rebuilding = false; }
        }
    }
}
