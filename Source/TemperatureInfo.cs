using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace Celsius
{
    public partial class TemperatureInfo : MapComponent
    {
        // Number of full map temperature updates between rare updates and between resets of min/max temperature
        public const int RareUpdateInterval = 4;
        public const int MinMaxTemperatureResetInterval = 20;

        // Minimum possible temperature of autoignition
        const float MinIgnitionTemperature = 100;

        bool initialized;
        internal bool IsInitialized => initialized;
        float[] temperatures;
        float[] terrainTemperatures;
        ThermalProps[] thermalProperties;

        internal bool isPathfinderUpdating = false;

        static float minComfortableTemperature = TemperatureTuning.DefaultTemperature - 5, maxComfortableTemperature = TemperatureTuning.DefaultTemperature + 5;
        static readonly Color minColor = Color.blue;
        static readonly Color minComfortableColor = new Color(0, 1, 0.25f);
        static readonly Color maxComfortableColor = new Color(0.25f, 1, 0);
        static readonly Color maxColor = Color.red;

        bool minMaxTemperaturesUpdated;
        float minTemperature, maxTemperature;
        CellBoolDrawer overlayDrawer;


        public TemperatureInfo(Map map)
            : base(map)
        { }

        public override void FinalizeInit()
        {
            if (initialized) return;
            thermalProperties = new ThermalProps[map.Size.x * map.Size.z];
            mapWidth = map.Size.x;
            if (temperatures != null && temperatures.Length != thermalProperties.Length)
                throw new InvalidOperationException("Celsius temperature grid does not match this map's dimensions.");
            if (terrainTemperatures != null && terrainTemperatures.Length != thermalProperties.Length)
                throw new InvalidOperationException("Celsius terrain grid does not match this map's dimensions.");

            // Initializing for the first run
            if (temperatures == null)
            {
                LogUtility.Log($"Initializing temperatures for {map} for the first time.", LogLevel.Important);
                temperatures = new float[map.Size.x * map.Size.z];
                float outdoorTemperature = map.mapTemperature.OutdoorTemp, annualAverageTemperature = Find.WorldGrid[map.Tile].temperature;
                for (int i = 0; i < temperatures.Length; i++)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(i);
                    Room room = cell.GetRoomOrAdjacent(map);
                    if (room != null)
                        temperatures[i] = room.TempTracker.Temperature;
                    else
                    {
                        RoofDef roof = map.roofGrid.RoofAt(i);
                        temperatures[i] = roof != null && roof.isThickRoof ? annualAverageTemperature : outdoorTemperature;
                    }
                }
            }
            InitializeTerrainTemperatures();

            minComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
            maxComfortableTemperature = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
            ResetMinMaxTemperature();

            overlayDrawer = new CellBoolDrawer(
                index => !map.fogGrid.IsFogged(index),
                () => Color.white,
                index => TemperatureColorForCell(index),
                map.Size.x,
                map.Size.z);

            phaseDirection = new byte[temperatures.Length];
            phaseThreshold = new float[temperatures.Length];
            roomOwners = new RoomSample[temperatures.Length];
            StartWorker();
            initialized = true;
            TemperatureUtility.temperatureInfos[map] = this;
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public void InitializeTerrainTemperatures()
        {
            if (!Settings.FreezingAndMeltingEnabled || temperatures == null) return;
            if (initialized) Synchronize();
            if (terrainTemperatures == null)
            {
                terrainTemperatures = new float[temperatures.Length];
                for (int i = 0; i < terrainTemperatures.Length; i++) terrainTemperatures[i] = float.NaN;
            }
            for (int i = 0; i < terrainTemperatures.Length; i++)
                if (float.IsNaN(terrainTemperatures[i]) && map.terrainGrid.TerrainAt(i).HasTemperature())
                    terrainTemperatures[i] = temperatures[i];
            allDirty = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Synchronize();
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            Scribe_Values.Look(ref version, "version");

            string str = DataUtility.ArrayToString(temperatures);
            Scribe_Values.Look(ref str, "temperatures");
            if (str != null && Scribe.mode == LoadSaveMode.LoadingVars)
                temperatures = DataUtility.StringToArray(str);

            {
                str = DataUtility.ArrayToString(terrainTemperatures);
                Scribe_Values.Look(ref str, "terrainTemperatures");
                if (str != null && Scribe.mode == LoadSaveMode.LoadingVars)
                    terrainTemperatures = DataUtility.StringToArray(str);
            }

            // Transpose temperature arrays from pre-2.0 Celsius to adapt to the new format
            if (version == null && Scribe.mode == LoadSaveMode.LoadingVars)
            {
                LogUtility.Log($"Loaded map {map} is from pre-2.0 Celsius. Converting it to the new data format.");
                temperatures = DataUtility.Transpose(temperatures, map.Size.x);
                if (terrainTemperatures != null)
                    terrainTemperatures = DataUtility.Transpose(terrainTemperatures, map.Size.x);
            }
        }

        public override void MapRemoved()
        {
            StopWorker();
            base.MapRemoved();
            TemperatureUtility.temperatureInfos.Remove(map);
            LogUtility.Log($"Map {map} removed. TemperatureInfo cache now contains {TemperatureUtility.temperatureInfos.Count} records.");
        }

        public void ResetAllThings()
        {
            if (initialized) Synchronize();
            List<Thing> things = map?.listerThings?.AllThings;
            if (things == null)
            { 
                LogUtility.Log($"ResetAllThings: Couldn't get all things for map {map}.", LogLevel.Error);
                return;
            }
            for (int i = 0; i < things.Count; i++)
                things[i].TryGetComp<CompThermal>()?.Reset();
            if (thermalProperties != null) Array.Clear(thermalProperties, 0, thermalProperties.Length);
            allDirty = true;
            roomsDirty = true;
        }

        public void ResetSnowMeltRate() { } // Retained for binary compatibility; captured per job.

        public void ResetMinMaxTemperature()
        {
            minTemperature = minComfortableTemperature - 10;
            maxTemperature = maxComfortableTemperature + 10;
        }

        Color TemperatureColorForCell(int index)
        {
            if (Settings.UseVanillaTemperatureColors)
                return map.mapTemperature.GetCellExtraColor(index);
            float temperature = GetTemperatureForCell(index);
            if (temperature < minComfortableTemperature)
                return Color.Lerp(minColor, minComfortableColor, (temperature - minTemperature) / (minComfortableTemperature - minTemperature));
            if (temperature < maxComfortableTemperature)
                return Color.Lerp(minComfortableColor, maxComfortableColor, (temperature - minComfortableTemperature) / (maxComfortableTemperature - minComfortableTemperature));
            return Color.Lerp(maxComfortableColor, maxColor, (temperature - maxComfortableTemperature) / (maxTemperature - maxComfortableTemperature));
        }

        public override void MapComponentUpdate()
        {
            if (!initialized) return;
            long start = Stopwatch.GetTimestamp();
            Publish();
            MainTimestampTicks += Stopwatch.GetTimestamp() - start;
            FlushSnow();
            if (Find.PlaySettings.showTemperatureOverlay && Find.CurrentMap == map)
            {
                if (!minMaxTemperaturesUpdated && !Settings.UseVanillaTemperatureColors)
                {
                    for (int i = 0; i < temperatures.Length; i++)
                        if (temperatures[i] < minTemperature)
                            minTemperature = temperatures[i];
                        else if (temperatures[i] > maxTemperature)
                            maxTemperature = temperatures[i];
                    minMaxTemperaturesUpdated = true;
                }
                overlayDrawer.MarkForDraw();
            }
            overlayDrawer.CellBoolDrawerUpdate();
        }

        public override void MapComponentOnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == DefOf.Celsius_SwitchTemperatureMap.MainKey && Find.MainTabsRoot.OpenTab == null)
            {
                Find.PlaySettings.showTemperatureOverlay = !Find.PlaySettings.showTemperatureOverlay;
                Event.current.Use();
            }
            if (!Find.PlaySettings.showTemperatureOverlay || !Settings.ShowTemperatureTooltip)
                return;
            int index = map.cellIndices.CellToIndex(UI.MouseCell());
            if (InBounds(index) && (!map.fogGrid.IsFogged(index) || Prefs.DevMode))
            {
                GameFont font = Text.Font;
                Text.Font = GameFont.Tiny;
                string tooltip = "Celsius_MapTempOverlay_Cell".Translate(GetTemperatureForCell(index).ToStringTemperature(Settings.TemperatureDisplayFormatString));
                if (Settings.FreezingAndMeltingEnabled && HasTerrainTemperatures)
                {
                    float terrainTemperature = terrainTemperatures[index];
                    if (!float.IsNaN(terrainTemperature))
                        tooltip += "\n" + "Celsius_MapTempOverlay_Terrain".Translate(terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString));
                }

                if (Settings.DebugMode)
                    Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 150, 100), $"{tooltip}\n{GetThermalPropertiesAt(index)}");
                else Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), tooltip);

                Text.Font = font;
            }
        }

        public bool InBounds(int index) => index >= 0 && index < (temperatures?.Length ?? 0);

        public float GetTemperatureForCell(int index)
        {
            if (!InBounds(index)) return TemperatureTuning.DefaultTemperature;
            float t = temperatures[index];
            if (flight != null && flight.Count != 0) t = flight.At(index, t);
            return pending != null && pending.Count != 0 ? pending.At(index, t) : t;
        }

        public float GetTemperatureForCell(IntVec3 cell) => GetTemperatureForCell(map.cellIndices.CellToIndex(cell));

        public float GetRoomAverageTemperature(Room room)
        {
            if (room == null || room.ID == -1 || !initialized) return map.mapTemperature.OutdoorTemp;
            if (roomsDirty)
            {
                rooms.Clear(); Array.Clear(roomOwners, 0, roomOwners.Length);
                roomListDirty = true; roomsDirty = false;
            }
            if (rooms.TryGetValue(room.ID, out RoomSample sample) && sample.Generation == publishedGeneration) return sample.Temperature;
            if (sample == null)
            {
                int[] cells = room.Cells.Select(c => map.cellIndices.CellToIndex(c)).ToArray();
                if (cells.Length == 0) return map.mapTemperature.OutdoorTemp;
                sample = new RoomSample { Cells = cells };
                rooms.Add(room.ID, sample);
                foreach (int i in cells) roomOwners[i] = sample;
                roomListDirty = true;
            }
            double sum = 0;
            foreach (int i in sample.Cells) sum += GetTemperatureForCell(i);
            sample.Temperature = (float)(sum / sample.Cells.Length); sample.Generation = publishedGeneration;
            return sample.Temperature;
        }

        public bool HasTerrainTemperatures => Settings.FreezingAndMeltingEnabled && terrainTemperatures != null;

        public float GetTerrainTemperature(int index) => terrainTemperatures[index];

        public void SetTemperatureForCell(int index, float temperature)
        {
            if (!InBounds(index) || float.IsNaN(temperature) || float.IsInfinity(temperature)) return;
            temperature = Math.Max(temperature, -273.15f);
            if (pending == null) { temperatures[index] = temperature; return; }
            float delta = temperature - GetTemperatureForCell(index);
            pending.Set(index, temperature);
            RoomSample room = roomOwners[index];
            if (room != null) room.Temperature += delta / room.Cells.Length;
        }

        public void SetTemperatureForCell(IntVec3 cell, float temperature) => SetTemperatureForCell(map.cellIndices.CellToIndex(cell), temperature);

        public ThermalProps GetThermalPropertiesAt(int index)
        {
            ThermalProps props = thermalProperties[index];
            if (props != null)
                return props;
            List<Thing> thingsList = map.thingGrid.ThingsListAtFast(index);
            for (int i = thingsList.Count - 1; i >= 0; i--)
            {
                Thing thing = thingsList[i];
                if (CompThermal.ShouldApplyTo(thing.def))
                {
                    props = thing.TryGetComp<CompThermal>()?.ThermalProperties;
                    if (props != null)
                        return thermalProperties[index] = props;
                }
            }
            return thermalProperties[index] = ThermalProps.Air;
        }

        public void PushHeat(int index, float energy)
        {
            if (!InBounds(index) || float.IsNaN(energy) || float.IsInfinity(energy)) return;
            float delta = energy * Settings.HeatPushEffect / Math.Max(0.0001f, GetThermalPropertiesAt(index).heatCapacity);
            delta = Math.Max(delta, -273.15f - GetTemperatureForCell(index));
            if (pending == null) { temperatures[index] += delta; return; }
            pending.Add(index, delta);
            RoomSample room = roomOwners[index];
            if (room != null) room.Temperature += delta / room.Cells.Length;
        }

        public void PushHeat(IntVec3 cell, float energy) => PushHeat(map.cellIndices.CellToIndex(cell), energy);

        public float GetIgnitionTemperatureForCell(int index)
        {
            float min = 9999;
            List<Thing> things = map.thingGrid.ThingsListAtFast(index);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].FireBulwark)
                    return 9999;
                if (things[i].GetStatValue(StatDefOf.Flammability) > 0)
                {
                    float ignitionTemperature = things[i].GetStatValue(DefOf.Celsius_IgnitionTemperature);
                    if (ignitionTemperature >= MinIgnitionTemperature && ignitionTemperature < min)
                        min = ignitionTemperature;
                }
            }
            return min;
        }
    }
}
