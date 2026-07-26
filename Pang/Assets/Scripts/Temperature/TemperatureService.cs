using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public interface ITemperatureModifier : IFacility
{
	int EffectRadius { get; }
	float TemperatureOffsetCelsius { get; }
}

public sealed class TemperatureService : MonoBehaviour, IGridOverlayProvider
{
	private sealed class ModifierState
	{
		public ITemperatureModifier Modifier;
		public int3 Position;
		public uint BuildingId;
		public int Radius;
		public float Offset;
		public float Efficiency;
	}

	[SerializeField] private float ambientTemperatureCelsius = GridCell.DefaultTemperatureCelsius;
	[FormerlySerializedAs("degreesPerTick")]
	[SerializeField, Min(0f)] private float degreesPerQuarterWeek = 1f;
	[SerializeField, Min(0f)] private float fireHeatDegreesPerTickAtMaxIntensity = 20f;
	[SerializeField, Range(0f, 0.25f)] private float heatDiffusionRatePerTick = 0.1f;
	[SerializeField, Min(0f)] private float naturalCoolingDegreesPerTick = 1f;
	[SerializeField, Min(0f)] private float defaultItemThermalResponsePerWeek = 16f;

	private readonly Dictionary<ITemperatureModifier, ModifierState> modifiers = new();
	private readonly Dictionary<int3, float> targets = new();
	private readonly HashSet<int3> dirtyCells = new();
	private readonly HashSet<int3> activeCells = new();
	private readonly List<ITemperatureModifier> modifierScratch = new();
	private readonly List<int3> cellScratch = new();
	private float[] temperatureSnapshot;
	private float[] nextTemperatureSnapshot;
	private int3 temperatureBufferSize;
	private bool eventsBound;
	private bool rebuildMapOnNextTick = true;

	public event System.Action OnGridOverlayRefreshRequested;
	public bool HideZeroAlphaPixels => false;

	public float AmbientTemperatureCelsius => ambientTemperatureCelsius;
	public float DegreesPerQuarterWeek => degreesPerQuarterWeek;
	public float DefaultItemThermalResponsePerWeek => defaultItemThermalResponsePerWeek;
	public int ActiveCellCount => activeCells.Count;

	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private FacilityManager FacilityManager => GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;

	private void OnEnable()
	{
		BindEvents();
		RebuildModifiers();
	}

	private void Start()
	{
		BindEvents();
		RebuildModifiers();
		InitializeTemperatureBuffers();
	}

	private void OnDisable() => UnbindEvents();

	public void ResetRuntimeState()
	{
		modifiers.Clear();
		targets.Clear();
		dirtyCells.Clear();
		activeCells.Clear();
		temperatureSnapshot = null;
		nextTemperatureSnapshot = null;
		temperatureBufferSize = default;
		rebuildMapOnNextTick = true;
	}

	public void RebuildRuntimeState()
	{
		RebuildModifiers();
		InitializeTemperatureBuffers();
		rebuildMapOnNextTick = true;
	}

	private void BindEvents()
	{
		if (eventsBound || FacilityManager == null || GridService == null)
			return;

		FacilityManager.SubscribeFacilityRegister<ITemperatureModifier>(HandleRegistered, HandleUnregistered);
		GridService.OnCellTemperatureChanged += HandleCellTemperatureChanged;
		eventsBound = true;
	}

	private void UnbindEvents()
	{
		if (eventsBound == false)
			return;

		if (FacilityManager != null)
			FacilityManager.UnsubscribeFacilityRegister<ITemperatureModifier>(HandleRegistered, HandleUnregistered);
		if (GridService != null)
			GridService.OnCellTemperatureChanged -= HandleCellTemperatureChanged;
		eventsBound = false;
	}

	private void RebuildModifiers()
	{
		modifiers.Clear();
		if (FacilityManager == null)
			return;

		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			IReadOnlyList<ITemperatureModifier> found = FacilityManager.GetFacilities<ITemperatureModifier>(buildingIds[i]);
			for (int j = 0; j < found.Count; ++j)
				Register(buildingIds[i], found[j]);
		}
	}

	private void HandleRegistered(uint buildingId, IFacility facility)
	{
		if (facility is ITemperatureModifier modifier)
			Register(buildingId, modifier);
	}

	private void HandleUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is not ITemperatureModifier modifier || modifiers.TryGetValue(modifier, out ModifierState state) == false)
			return;

		MarkAffectedDirty(state);
		modifiers.Remove(modifier);
	}

	private void Register(uint buildingId, ITemperatureModifier modifier)
	{
		if (modifier == null || buildingId == 0)
			return;

		if (modifiers.TryGetValue(modifier, out ModifierState oldState))
			MarkAffectedDirty(oldState);

		ModifierState state = Capture(modifier, buildingId);
		modifiers[modifier] = state;
		MarkAffectedDirty(state);
	}

	private static ModifierState Capture(ITemperatureModifier modifier, uint buildingId)
	{
		return new ModifierState
		{
			Modifier = modifier,
			Position = modifier.GridPosition,
			BuildingId = buildingId,
			Radius = Mathf.Max(0, modifier.EffectRadius),
			Offset = modifier.TemperatureOffsetCelsius,
			Efficiency = Mathf.Clamp01(GameContext.HasInstance ? GameContext.Instance.PowerSvc.GetPowerEfficiency(modifier) : 0f),
		};
	}

	public void ProcessQuarterWeekTick()
	{
		if (GridService == null || GridService.IsReady == false)
			return;

		if (rebuildMapOnNextTick)
		{
			MarkAllCellsDirty();
			rebuildMapOnNextTick = false;
		}

		RefreshModifierStates();
		RecalculateDirtyTargets();
		AdvanceActiveTemperatures();
		OnGridOverlayRefreshRequested?.Invoke();
	}

	public void ProcessSimulationTick()
	{
		if (GridService == null || GridService.IsReady == false)
			return;

		if (EnsureTemperatureBuffers() == false)
			return;

		int3 size = temperatureBufferSize;

		bool changed = false;
		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					int index = ToIndex(x, y, z, in size);
					int3 position = new(x, y, z);
					GridCell cell = GridService.GetCell(position);
					if (cell == null)
						continue;

					float current = temperatureSnapshot[index];
					float next = current + fireHeatDegreesPerTickAtMaxIntensity *
						Mathf.Clamp01(cell.FireIntensity / FireService.MaximumFireIntensity);
					float neighborAverage = CalculateNeighborAverage(x, y, z, in size, current);
					next += (neighborAverage - current) * heatDiffusionRatePerTick;

					float target = targets.TryGetValue(position, out float value) ? value : ambientTemperatureCelsius;
					next = Mathf.MoveTowards(next, target, naturalCoolingDegreesPerTick);
					nextTemperatureSnapshot[index] = Mathf.Max(-273.15f, next);
				}
			}
		}

		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					int index = ToIndex(x, y, z, in size);
					int3 position = new(x, y, z);
					if (Mathf.Approximately(temperatureSnapshot[index], nextTemperatureSnapshot[index]) == false)
						changed |= GridService.TrySetTemperature(position, nextTemperatureSnapshot[index]);
				}
			}
		}

		if (changed)
			OnGridOverlayRefreshRequested?.Invoke();
	}

	public bool ApplyHeatImpulse(in int3 position, float degreesCelsius)
	{
		return GridService != null && degreesCelsius > 0.0f &&
			float.IsNaN(degreesCelsius) == false && float.IsInfinity(degreesCelsius) == false &&
			GridService.TryAdjustTemperature(position, degreesCelsius);
	}

	public bool TryFillGridOverlay(Color32[] buffer, int floor)
	{
		if (GridService == null || GridService.IsReady == false)
			return false;

		int3 size = GridService.MapSize;
		if (buffer == null || buffer.Length < size.x * size.z || floor < 0 || floor >= size.y)
			return false;

		for (int z = 0; z < size.z; ++z)
		{
			for (int x = 0; x < size.x; ++x)
			{
				GridCell cell = GridService.GetCell(x, floor, z);
				buffer[z * size.x + x] = GetOverlayColor(cell != null
					? cell.TemperatureCelsius
					: ambientTemperatureCelsius);
			}
		}

		return true;
	}

	private static Color32 GetOverlayColor(float temperatureCelsius)
	{
		float clamped = Mathf.Clamp(temperatureCelsius, -100f, 100f);
		if (clamped <= 0f)
		{
			byte blue = (byte)Mathf.RoundToInt(Mathf.InverseLerp(-100f, 0f, clamped) * byte.MaxValue);
			return new Color32(0, 0, blue, 0);
		}

		float normalized = Mathf.InverseLerp(0f, 100f, clamped);
		byte red = (byte)Mathf.RoundToInt(normalized * byte.MaxValue);
		byte blueToRed = (byte)Mathf.RoundToInt((1f - normalized) * byte.MaxValue);
		return new Color32(red, 0, blueToRed, 0);
	}

	private void RefreshModifierStates()
	{
		modifierScratch.Clear();
		foreach (ITemperatureModifier modifier in modifiers.Keys)
			modifierScratch.Add(modifier);

		for (int i = 0; i < modifierScratch.Count; ++i)
		{
			ITemperatureModifier modifier = modifierScratch[i];
			if (modifier is Object unityObject && unityObject == null)
			{
				RemoveMissing(modifier);
				continue;
			}

			if (FacilityManager == null || FacilityManager.TryGetBuildingId(modifier, out uint buildingId) == false || buildingId == 0)
			{
				RemoveMissing(modifier);
				continue;
			}

			ModifierState previous = modifiers[modifier];
			ModifierState current = Capture(modifier, buildingId);
			if (HasChanged(previous, current) == false)
				continue;

			MarkAffectedDirty(previous);
			modifiers[modifier] = current;
			MarkAffectedDirty(current);
		}
	}

	private void RemoveMissing(ITemperatureModifier modifier)
	{
		if (modifiers.TryGetValue(modifier, out ModifierState state))
			MarkAffectedDirty(state);
		modifiers.Remove(modifier);
	}

	private static bool HasChanged(ModifierState a, ModifierState b)
	{
		return a.Position.Equals(b.Position) == false || a.BuildingId != b.BuildingId || a.Radius != b.Radius ||
			Mathf.Approximately(a.Offset, b.Offset) == false || Mathf.Approximately(a.Efficiency, b.Efficiency) == false;
	}

	private void MarkAllCellsDirty()
	{
		int3 size = GridService.MapSize;
		for (int x = 0; x < size.x; ++x)
			for (int y = 0; y < size.y; ++y)
				for (int z = 0; z < size.z; ++z)
					dirtyCells.Add(new int3(x, y, z));
	}

	private void MarkAffectedDirty(ModifierState state)
	{
		if (state == null || state.BuildingId == 0 || GridService == null)
			return;

		for (int x = -state.Radius; x <= state.Radius; ++x)
		{
			int zRange = state.Radius - Mathf.Abs(x);
			for (int z = -zRange; z <= zRange; ++z)
			{
				int3 position = state.Position + new int3(x, 0, z);
				GridCell cell = GridService.GetCell(position);
				if (cell != null && cell.BuildingId == state.BuildingId && cell.IsIndoor)
					dirtyCells.Add(position);
			}
		}
	}

	private void RecalculateDirtyTargets()
	{
		cellScratch.Clear();
		foreach (int3 position in dirtyCells)
			cellScratch.Add(position);
		dirtyCells.Clear();

		for (int i = 0; i < cellScratch.Count; ++i)
		{
			int3 position = cellScratch[i];
			GridCell cell = GridService.GetCell(position);
			if (cell == null)
			{
				targets.Remove(position);
				activeCells.Remove(position);
				continue;
			}

			float target = CalculateTarget(position, cell);
			targets[position] = target;
			if (Mathf.Approximately(cell.TemperatureCelsius, target))
			{
				activeCells.Remove(position);
				if (Mathf.Approximately(target, ambientTemperatureCelsius))
					targets.Remove(position);
			}
			else
			{
				activeCells.Add(position);
			}
		}
	}

	private float CalculateTarget(in int3 position, GridCell cell)
	{
		float target = ambientTemperatureCelsius;
		foreach (ModifierState state in modifiers.Values)
		{
			if (Affects(state, position, cell))
				target += state.Offset * state.Efficiency;
		}
		return Mathf.Max(-273.15f, target);
	}

	private static bool Affects(ModifierState state, in int3 position, GridCell cell)
	{
		if (state.Efficiency <= 0f || cell.BuildingId != state.BuildingId || cell.IsIndoor == false || position.y != state.Position.y)
			return false;

		return Mathf.Abs(position.x - state.Position.x) + Mathf.Abs(position.z - state.Position.z) <= state.Radius;
	}

	private void AdvanceActiveTemperatures()
	{
		cellScratch.Clear();
		foreach (int3 position in activeCells)
			cellScratch.Add(position);

		for (int i = 0; i < cellScratch.Count; ++i)
		{
			int3 position = cellScratch[i];
			GridCell cell = GridService.GetCell(position);
			if (cell == null)
			{
				activeCells.Remove(position);
				targets.Remove(position);
				continue;
			}

			float target = targets.TryGetValue(position, out float value) ? value : ambientTemperatureCelsius;
			float next = Mathf.MoveTowards(cell.TemperatureCelsius, target, degreesPerQuarterWeek);
			GridService.TrySetTemperature(position, next);
			if (Mathf.Approximately(next, target))
			{
				activeCells.Remove(position);
				if (Mathf.Approximately(target, ambientTemperatureCelsius))
					targets.Remove(position);
			}
		}
	}

	private void HandleCellTemperatureChanged(int3 position, float previous, float current)
	{
		if (temperatureSnapshot != null &&
			position.x >= 0 && position.x < temperatureBufferSize.x &&
			position.y >= 0 && position.y < temperatureBufferSize.y &&
			position.z >= 0 && position.z < temperatureBufferSize.z)
		{
			temperatureSnapshot[ToIndex(position.x, position.y, position.z, in temperatureBufferSize)] = current;
		}

		float target = targets.TryGetValue(position, out float value) ? value : ambientTemperatureCelsius;
		if (Mathf.Approximately(current, target))
			activeCells.Remove(position);
		else
			activeCells.Add(position);
	}

	private bool EnsureTemperatureBuffers()
	{
		if (GridService == null || GridService.IsReady == false)
			return false;

		int3 size = GridService.MapSize;
		int cellCount = size.x * size.y * size.z;
		if (temperatureSnapshot != null && nextTemperatureSnapshot != null &&
			temperatureSnapshot.Length == cellCount && temperatureBufferSize.Equals(size))
		{
			return true;
		}

		InitializeTemperatureBuffers();
		return temperatureSnapshot != null;
	}

	private void InitializeTemperatureBuffers()
	{
		if (GridService == null || GridService.IsReady == false)
			return;

		int3 size = GridService.MapSize;
		int cellCount = size.x * size.y * size.z;
		temperatureSnapshot = new float[cellCount];
		nextTemperatureSnapshot = new float[cellCount];
		temperatureBufferSize = size;

		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					int index = ToIndex(x, y, z, in size);
					temperatureSnapshot[index] = GridService.GetCell(x, y, z)?.TemperatureCelsius ?? ambientTemperatureCelsius;
				}
			}
		}
	}

	private float CalculateNeighborAverage(int x, int y, int z, in int3 size, float fallback)
	{
		float total = 0.0f;
		int count = 0;
		AddNeighborTemperature(x + 1, y, z, in size, ref total, ref count);
		AddNeighborTemperature(x - 1, y, z, in size, ref total, ref count);
		AddNeighborTemperature(x, y, z + 1, in size, ref total, ref count);
		AddNeighborTemperature(x, y, z - 1, in size, ref total, ref count);
		return count > 0 ? total / count : fallback;
	}

	private void AddNeighborTemperature(int x, int y, int z, in int3 size, ref float total, ref int count)
	{
		if (x < 0 || x >= size.x || y < 0 || y >= size.y || z < 0 || z >= size.z)
			return;

		total += temperatureSnapshot[ToIndex(x, y, z, in size)];
		++count;
	}

	private static int ToIndex(int x, int y, int z, in int3 size) => x + size.x * (y + size.y * z);
}
