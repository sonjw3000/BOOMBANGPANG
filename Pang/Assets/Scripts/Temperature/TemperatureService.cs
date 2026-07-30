using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public interface ITemperatureModifier : IWearableFacility
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

	private readonly Dictionary<ITemperatureModifier, ModifierState> modifiers = new();
	private readonly Dictionary<int3, float> targets = new();
	private readonly Dictionary<GridCell, int3> cellPositions = new();
	private readonly HashSet<int3> dirtyCells = new();
	private readonly HashSet<int3> activeCells = new();
	private readonly List<ITemperatureModifier> modifierScratch = new();
	private readonly List<int3> cellScratch = new();
	private float[] temperatureSnapshot;
	private float[] nextTemperatureSnapshot;
	private int3 temperatureBufferSize;
	private int3 cellPositionMapSize;
	private bool eventsBound;
	private bool rebuildMapOnNextTick = true;

	public event System.Action OnGridOverlayRefreshRequested;
	public bool HideZeroAlphaPixels => false;

	public float AmbientTemperatureCelsius => ambientTemperatureCelsius;
	public float DegreesPerQuarterWeek => degreesPerQuarterWeek;
	public int ActiveCellCount => activeCells.Count;

	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private FacilityManager FacilityManager => GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingAddonService BuildingAddonService =>
		GameContext.HasInstance ? GameContext.Instance.BuildingAddonSvc : null;

	private void OnEnable()
	{
		BindEvents();
		RebuildModifiers();
		rebuildMapOnNextTick = true;
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
		cellPositions.Clear();
		dirtyCells.Clear();
		activeCells.Clear();
		temperatureSnapshot = null;
		nextTemperatureSnapshot = null;
		temperatureBufferSize = default;
		cellPositionMapSize = default;
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
		if (eventsBound || FacilityManager == null || GridService == null || BuildingAddonService == null)
			return;

		FacilityManager.SubscribeFacilityRegister<ITemperatureModifier>(HandleRegistered, HandleUnregistered);
		GridService.OnCellTemperatureChanged += HandleCellTemperatureChanged;
		BuildingAddonService.OnAddonInstalled += OnAddonInstalled;
		BuildingAddonService.OnAddonRemoved += OnAddonRemoved;
		BuildingAddonService.OnTargetTemperatureChanged += OnTargetTemperatureChanged;
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
		if (BuildingAddonService != null)
		{
			BuildingAddonService.OnAddonInstalled -= OnAddonInstalled;
			BuildingAddonService.OnAddonRemoved -= OnAddonRemoved;
			BuildingAddonService.OnTargetTemperatureChanged -= OnTargetTemperatureChanged;
		}
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

	private void OnAddonInstalled(Building building, BuildingAddon addon)
	{
		if (addon is TemperatureControlBuildingAddon)
			MarkBuildingCellsDirty(building);
	}

	private void OnAddonRemoved(Building building, BuildingAddon addon)
	{
		if (addon is TemperatureControlBuildingAddon)
			MarkBuildingCellsDirty(building);
	}

	private void OnTargetTemperatureChanged(Building building, float previous, float current)
	{
		MarkBuildingCellsDirty(building);
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
			Efficiency = FacilityEfficiency.GetOperatingEfficiency(modifier),
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
		MarkClimateControlCellsDirty();
		RecalculateDirtyTargets();
		ReportModifierOperation();
		ReportTemperatureControlOperation();
		AdvanceActiveTemperatures();
		OnGridOverlayRefreshRequested?.Invoke();
	}

	public bool IsTemperatureControlOperating(Building building, BuildingAddon addon)
	{
		if (building == null ||
			addon is not TemperatureControlBuildingAddon temperatureControl ||
			building.ContainsAddon(addon) == false)
		{
			return false;
		}

		IReadOnlyList<GridCell> cells = building.OccupiedCells;
		for (int i = 0; i < cells.Count; ++i)
		{
			GridCell cell = cells[i];
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId) &&
				CalculateTemperatureControlOutput(
					building,
					temperatureControl,
					cell.TemperatureCelsius,
					building.TargetTemperatureCelsius) > 0.0f)
			{
				return true;
			}
		}

		return false;
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

					float target = CalculateTarget(position, cell, next);
					bool climateControlIsActive = Mathf.Approximately(
						CalculateTemperatureControlDelta(cell, next),
						0.0f) == false;
					if (climateControlIsActive == false || HasAffectingModifier(position, cell))
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

	private void ReportModifierOperation()
	{
		foreach (ModifierState state in modifiers.Values)
		{
			if (state.Efficiency <= 0.0f || HasActiveDemand(state) == false)
				continue;

			GameContext.Instance.WearSvc.ReportOperation(
				state.Modifier,
				GameTime.SimulationTickWeeks * GameTime.QuarterWeekSimulationTickInterval);
		}
	}

	private void ReportTemperatureControlOperation()
	{
		if (BuildingManager == null || GameContext.HasInstance == false || GameContext.Instance.WearSvc == null)
			return;

		float elapsedWeeks = GameTime.SimulationTickWeeks * GameTime.QuarterWeekSimulationTickInterval;
		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int buildingIndex = 0; buildingIndex < buildings.Count; ++buildingIndex)
		{
			Building building = buildings[buildingIndex];
			if (building == null)
				continue;

			IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
			for (int addonIndex = 0; addonIndex < addons.Count; ++addonIndex)
			{
				BuildingAddon addon = addons[addonIndex];
				if (IsTemperatureControlOperating(building, addon))
					GameContext.Instance.WearSvc.ReportOperation(addon, elapsedWeeks);
			}
		}
	}

	private bool HasActiveDemand(ModifierState state)
	{
		for (int x = -state.Radius; x <= state.Radius; ++x)
		{
			int zRange = state.Radius - Mathf.Abs(x);
			for (int z = -zRange; z <= zRange; ++z)
			{
				int3 position = state.Position + new int3(x, 0, z);
				GridCell cell = GridService.GetCell(position);
				if (cell == null || Affects(state, position, cell) == false)
					continue;

				float target = targets.TryGetValue(position, out float value)
					? value
					: ambientTemperatureCelsius;
				if (Mathf.Approximately(cell.TemperatureCelsius, target) == false)
					return true;
			}
		}

		return false;
	}

	private void MarkAllCellsDirty()
	{
		int3 size = GridService.MapSize;
		for (int x = 0; x < size.x; ++x)
			for (int y = 0; y < size.y; ++y)
				for (int z = 0; z < size.z; ++z)
					dirtyCells.Add(new int3(x, y, z));
	}

	private void MarkClimateControlCellsDirty()
	{
		if (BuildingManager == null || GridService == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building != null && HasTemperatureControlAddon(building))
				MarkBuildingCellsDirty(building);
		}
	}

	private void MarkBuildingCellsDirty(Building building)
	{
		if (building == null || building.RuntimeBuildingId == 0 || GridService == null || GridService.IsReady == false)
			return;

		EnsureCellPositionLookup();
		uint buildingId = building.RuntimeBuildingId;
		IReadOnlyList<GridCell> cells = building.OccupiedCells;
		for (int i = 0; i < cells.Count; ++i)
		{
			GridCell cell = cells[i];
			if (cell != null &&
				cell.BuildingId == buildingId &&
				cellPositions.TryGetValue(cell, out int3 position) &&
				(cell.IsIndoor || targets.ContainsKey(position)))
			{
				dirtyCells.Add(position);
			}
		}
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

			float target = CalculateTarget(position, cell, cell.TemperatureCelsius);
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

	private float CalculateTarget(in int3 position, GridCell cell, float currentTemperatureCelsius)
	{
		float target = TryGetOperationalClimateTarget(
			cell,
			currentTemperatureCelsius,
			out float climateTarget)
			? climateTarget
			: ambientTemperatureCelsius;
		foreach (ModifierState state in modifiers.Values)
		{
			if (Affects(state, position, cell))
				target += state.Offset * state.Efficiency;
		}
		return Mathf.Max(-273.15f, target);
	}

	private bool TryGetOperationalClimateTarget(
		GridCell cell,
		float currentTemperatureCelsius,
		out float target)
	{
		target = ambientTemperatureCelsius;
		if (cell == null ||
			IsBuildingIndoorCell(cell, cell.BuildingId) == false ||
			BuildingManager == null ||
			BuildingManager.TryGetBuilding(cell.BuildingId, out Building building) == false ||
			building == null)
		{
			return false;
		}

		float requestedTarget = building.TargetTemperatureCelsius;
		if (float.IsNaN(requestedTarget) || float.IsInfinity(requestedTarget))
			return false;

		IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
		for (int i = 0; i < addons.Count; ++i)
		{
			if (addons[i] is not TemperatureControlBuildingAddon addon ||
				CalculateTemperatureControlOutput(
					building,
					addon,
					currentTemperatureCelsius,
					requestedTarget) <= 0.0f)
			{
				continue;
			}

			target = requestedTarget;
			return true;
		}

		return false;
	}

	private static bool Affects(ModifierState state, in int3 position, GridCell cell)
	{
		if (state.Efficiency <= 0f || cell.BuildingId != state.BuildingId || cell.IsIndoor == false || position.y != state.Position.y)
			return false;

		return Mathf.Abs(position.x - state.Position.x) + Mathf.Abs(position.z - state.Position.z) <= state.Radius;
	}

	private bool HasAffectingModifier(in int3 position, GridCell cell)
	{
		foreach (ModifierState state in modifiers.Values)
		{
			if (Affects(state, position, cell))
				return true;
		}

		return false;
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
			float current = cell.TemperatureCelsius;
			float climateDelta = CalculateTemperatureControlDelta(cell, current);
			bool applyBaseRate = Mathf.Approximately(climateDelta, 0.0f) ||
				HasAffectingModifier(position, cell);
			float next = applyBaseRate
				? Mathf.MoveTowards(current, target, degreesPerQuarterWeek)
				: current;
			next = Mathf.Max(-273.15f, next + climateDelta);
			GridService.TrySetTemperature(position, next);
			if (Mathf.Approximately(next, target))
			{
				activeCells.Remove(position);
				if (Mathf.Approximately(target, ambientTemperatureCelsius))
					targets.Remove(position);
			}
		}
	}

	private float CalculateTemperatureControlDelta(GridCell cell, float current)
	{
		if (cell == null ||
			IsBuildingIndoorCell(cell, cell.BuildingId) == false ||
			BuildingManager == null ||
			BuildingManager.TryGetBuilding(cell.BuildingId, out Building building) == false ||
			building == null)
		{
			return 0.0f;
		}

		float target = building.TargetTemperatureCelsius;
		if (Mathf.Approximately(current, target))
			return 0.0f;

		float availableDegrees = 0.0f;
		IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
		for (int i = 0; i < addons.Count; ++i)
		{
			if (addons[i] is TemperatureControlBuildingAddon addon)
				availableDegrees += CalculateTemperatureControlOutput(building, addon, current, target);
		}

		if (availableDegrees <= 0.0f)
			return 0.0f;

		return Mathf.MoveTowards(current, target, availableDegrees) - current;
	}

	private static float CalculateTemperatureControlOutput(
		Building building,
		TemperatureControlBuildingAddon addon,
		float current,
		float target)
	{
		// BuildingAddonService validates the combined target range. Each installed controller
		// contributes here by direction so 0–20 cooling + 20–40 heating controls the full 0–40 range.
		if (building == null ||
			addon == null ||
			float.IsNaN(current) ||
			float.IsInfinity(current) ||
			float.IsNaN(target) ||
			float.IsInfinity(target) ||
			HasTemperatureControlDemand(addon, current, target) == false)
		{
			return 0.0f;
		}

		float efficiency = FacilityEfficiency.GetOperatingEfficiency(building, addon, addon);
		return addon.TemperatureControlDegreesPerQuarterWeek * efficiency;
	}

	private static bool HasTemperatureControlDemand(
		TemperatureControlBuildingAddon addon,
		float current,
		float target)
	{
		if (Mathf.Approximately(current, target))
			return false;

		return current > target ? addon.CanCool : addon.CanHeat;
	}

	private static bool HasTemperatureControlAddon(Building building)
	{
		if (building == null)
			return false;

		IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
		for (int i = 0; i < addons.Count; ++i)
		{
			if (addons[i] is TemperatureControlBuildingAddon)
				return true;
		}

		return false;
	}

	private static bool IsBuildingIndoorCell(GridCell cell, uint buildingId)
	{
		return cell != null && buildingId != 0 && cell.BuildingId == buildingId && cell.IsIndoor;
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

	private void EnsureCellPositionLookup()
	{
		if (GridService == null || GridService.IsReady == false)
			return;

		int3 size = GridService.MapSize;
		if (cellPositions.Count > 0 && cellPositionMapSize.Equals(size))
			return;

		cellPositions.Clear();
		cellPositionMapSize = size;
		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					GridCell cell = GridService.GetCell(x, y, z);
					if (cell != null)
						cellPositions[cell] = new int3(x, y, z);
				}
			}
		}
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
		cellPositions.Clear();
		cellPositionMapSize = size;

		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					int index = ToIndex(x, y, z, in size);
					GridCell cell = GridService.GetCell(x, y, z);
					temperatureSnapshot[index] = cell?.TemperatureCelsius ?? ambientTemperatureCelsius;
					if (cell != null)
						cellPositions[cell] = new int3(x, y, z);
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
