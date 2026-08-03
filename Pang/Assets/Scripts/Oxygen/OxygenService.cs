using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface IOxygenSupplier : IHealth, IWearable
{
	float OxygenSupplyPerTick { get; }
}

public enum BuildingOxygenAlertLevel
{
	Normal,
	Warning,
	Critical,
}

public sealed class OxygenService : MonoBehaviour, IGridOverlayProvider
{
	[SerializeField, Min(0.0f)] private float fireOxygenConsumptionAtMaxIntensity = 5.0f;
	[SerializeField, Min(0.0f)] private float humanOxygenConsumptionPerTick = 1.0f;
	[SerializeField, Range(0.0f, 100.0f)] private float lowOxygenWarningThreshold = 30.0f;
	[SerializeField, Range(0.0f, 100.0f)] private float criticalOxygenThreshold = 20.0f;
	[SerializeField, Min(0.0f)] private float criticalOxygenDamagePerTick = 2.0f;

	private bool eventsBound;
	private bool clearOutdoorOnNextTick = true;
	private bool isProcessingTick;
	private readonly List<HumanWorker> suitlessHumans = new();
	private readonly Dictionary<uint, BuildingOxygenAlertLevel> alertLevelsByBuildingId = new();

	public event Action OnGridOverlayRefreshRequested;
	public bool HideZeroAlphaPixels => true;
	public float HumanOxygenConsumptionPerTick => Mathf.Max(0.0f, humanOxygenConsumptionPerTick);
	public float LowOxygenWarningThreshold => Mathf.Clamp(
		lowOxygenWarningThreshold,
		CriticalOxygenThreshold,
		GridCell.MaximumOxygen);
	public float CriticalOxygenThreshold => Mathf.Clamp(
		criticalOxygenThreshold,
		GridCell.DefaultOxygen,
		GridCell.MaximumOxygen);
	public float CriticalOxygenDamagePerTick => Mathf.Max(0.0f, criticalOxygenDamagePerTick);

	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;

	private void OnEnable()
	{
		BindEvents();
	}

	private void Start()
	{
		BindEvents();
	}

	private void OnDisable() => UnbindEvents();

	public void ResetRuntimeState()
	{
		clearOutdoorOnNextTick = true;
		isProcessingTick = false;
		alertLevelsByBuildingId.Clear();
	}

	public void RebuildRuntimeState()
	{
		clearOutdoorOnNextTick = true;
		alertLevelsByBuildingId.Clear();
	}

	public float GetAverageOxygen(Building building)
	{
		return TryGetBuildingOxygen(building, out float average, out _) ? average : GridCell.DefaultOxygen;
	}

	public int GetSuitlessHumanCount(Building building)
	{
		return CollectSuitlessHumans(building, null);
	}

	public float GetHumanOxygenConsumptionPerTick(Building building)
	{
		return GetSuitlessHumanCount(building) * HumanOxygenConsumptionPerTick;
	}

	public float GetOxygenSupplyPerTick(Building building) => CalculateSupply(building);
	public float GetFireOxygenConsumptionPerTick(Building building)
	{
		if (building == null)
			return 0.0f;

		float total = 0.0f;
		IReadOnlyList<GridCell> occupiedCells = building.OccupiedCells;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId))
				total += CalculateFireOxygenConsumption(cell);
		}

		return total;
	}

	public float GetNetOxygenPerTick(Building building)
	{
		return GetOxygenSupplyPerTick(building) -
			GetHumanOxygenConsumptionPerTick(building) -
			GetFireOxygenConsumptionPerTick(building);
	}

	public float GetSuitlessWorkSpeedMultiplier(HumanWorker human)
	{
		if (human == null || human.IsSuitRemoved == false || GridService == null)
			return 1.0f;

		GridCell cell = GridService.GetCell(human.GridPosition);
		return cell != null && cell.IsIndoor
			? EvaluateSuitlessWorkSpeedMultiplier(cell.Oxygen)
			: 1.0f;
	}

	public static float EvaluateSuitlessWorkSpeedMultiplier(float oxygen)
	{
		float value = Mathf.Clamp(oxygen, GridCell.DefaultOxygen, GridCell.MaximumOxygen);
		if (value <= 20.0f)
			return Mathf.Lerp(0.25f, 0.5f, value / 20.0f);
		if (value <= 80.0f)
			return Mathf.Lerp(0.5f, 1.0f, (value - 20.0f) / 60.0f);

		return Mathf.Lerp(1.0f, 2.0f, (value - 80.0f) / 20.0f);
	}

	public void ProcessSimulationTick(in SimulationTickContext context)
	{
		if (GridService == null || GridService.IsReady == false || BuildingManager == null)
			return;

		bool changed = false;
		isProcessingTick = true;
		try
		{
			if (clearOutdoorOnNextTick)
			{
				changed |= ClearOutdoorOxygen();
				clearOutdoorOnNextTick = false;
			}

			IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
			for (int i = 0; i < buildings.Count; ++i)
				changed |= ProcessBuilding(buildings[i], in context);

			changed |= ConsumeOxygenForFires();

			for (int i = 0; i < buildings.Count; ++i)
				ProcessBuildingHazards(buildings[i]);
		}
		finally
		{
			isProcessingTick = false;
		}

		if (changed)
			OnGridOverlayRefreshRequested?.Invoke();
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
				buffer[z * size.x + x] = GetOverlayColor(cell);
			}
		}

		return true;
	}

	private void BindEvents()
	{
		if (eventsBound || GridService == null)
			return;

		GridService.OnCellOxygenChanged += HandleCellOxygenChanged;
		GridService.OnSpaceRegionsChanged += HandleSpaceRegionsChanged;
		eventsBound = true;
	}

	private void UnbindEvents()
	{
		if (eventsBound == false)
			return;

		if (GridService != null)
		{
			GridService.OnCellOxygenChanged -= HandleCellOxygenChanged;
			GridService.OnSpaceRegionsChanged -= HandleSpaceRegionsChanged;
		}
		eventsBound = false;
	}

	private bool ProcessBuilding(Building building, in SimulationTickContext context)
	{
		if (building == null || building.RuntimeBuildingId == 0)
			return false;

		if (TryGetBuildingOxygen(building, out float average, out int indoorCellCount) == false)
			return false;

		int suitlessHumanCount = CollectSuitlessHumans(building, null);
		float humanConsumption = suitlessHumanCount * HumanOxygenConsumptionPerTick;
		float supply = CalculateSupply(building);
		float requestedSupply =
			(GridCell.MaximumOxygen - average) * indoorCellCount + humanConsumption;
		ReportSupplierOperation(building, requestedSupply, supply, context.ElapsedWeeks);
		float next = Mathf.Clamp(
			average + (supply - humanConsumption) / indoorCellCount,
			GridCell.DefaultOxygen,
			GridCell.MaximumOxygen);

		bool changed = false;
		IReadOnlyList<GridCell> occupiedCells = building.OccupiedCells;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId))
				changed |= GridService.TrySetOxygen(cell, next);
		}

		return changed;
	}

	private void ProcessBuildingHazards(Building building)
	{
		if (TryGetBuildingOxygen(building, out float average, out _) == false)
			return;

		suitlessHumans.Clear();
		int suitlessHumanCount = CollectSuitlessHumans(building, suitlessHumans);
		ApplyCriticalOxygenDamage(average, suitlessHumans);
		UpdateBuildingOxygenAlert(building, average, suitlessHumanCount);
	}

	private bool TryGetBuildingOxygen(Building building, out float average, out int indoorCellCount)
	{
		average = GridCell.DefaultOxygen;
		indoorCellCount = 0;
		if (building == null || building.RuntimeBuildingId == 0)
			return false;

		float oxygenSum = 0.0f;
		IReadOnlyList<GridCell> occupiedCells = building.OccupiedCells;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId) == false)
				continue;

			indoorCellCount += 1;
			oxygenSum += cell.Oxygen;
		}

		if (indoorCellCount <= 0)
			return false;

		average = oxygenSum / indoorCellCount;
		return true;
	}

	private int CollectSuitlessHumans(Building building, List<HumanWorker> results)
	{
		if (building == null || building.IsSuitRemovalPolicyActive == false || GameContext.HasInstance == false)
			return 0;

		WorkerManager workerManager = GameContext.Instance.WorkerMgr;
		if (workerManager == null || GridService == null)
			return 0;

		int count = 0;
		IReadOnlyList<AIWorker> workers = workerManager.Workers;
		for (int i = 0; i < workers.Count; ++i)
		{
			if (workers[i] is not HumanWorker human ||
				human.Health <= 0.0f ||
				human.IsSuitRemoved == false)
			{
				continue;
			}

			GridCell cell = GridService.GetCell(human.GridPosition);
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId) == false)
				continue;

			count += 1;
			results?.Add(human);
		}

		return count;
	}

	private void ApplyCriticalOxygenDamage(float oxygen, IReadOnlyList<HumanWorker> humans)
	{
		if (oxygen > CriticalOxygenThreshold || CriticalOxygenDamagePerTick <= 0.0f || humans == null)
			return;

		for (int i = 0; i < humans.Count; ++i)
		{
			HumanWorker human = humans[i];
			if (human != null && human.Health > 0.0f && human.IsSuitRemoved)
				human.ApplyDamage(CriticalOxygenDamagePerTick);
		}
	}

	private void UpdateBuildingOxygenAlert(Building building, float oxygen, int suitlessHumanCount)
	{
		if (building == null || building.RuntimeBuildingId == 0)
			return;

		uint buildingId = building.RuntimeBuildingId;
		if (building.IsSuitRemovalPolicyActive == false || suitlessHumanCount <= 0)
		{
			alertLevelsByBuildingId.Remove(buildingId);
			return;
		}

		alertLevelsByBuildingId.TryGetValue(buildingId, out BuildingOxygenAlertLevel previous);
		BuildingOxygenAlertLevel next = ResolveAlertLevel(previous, oxygen);
		alertLevelsByBuildingId[buildingId] = next;
		if (next <= previous)
			return;

		if (next == BuildingOxygenAlertLevel.Critical)
			ShowBuildingOxygenAlert(
				building,
				FloatingTextPreset.Error,
				$"Low O2 - {CriticalOxygenThreshold:0}%");
		else if (next == BuildingOxygenAlertLevel.Warning)
			ShowBuildingOxygenAlert(
				building,
				FloatingTextPreset.Warning,
				$"Warning: Low O2 - {LowOxygenWarningThreshold:0}%");
	}

	private BuildingOxygenAlertLevel ResolveAlertLevel(BuildingOxygenAlertLevel previous, float oxygen)
	{
		const float recoveryMargin = 5.0f;
		if (previous == BuildingOxygenAlertLevel.Critical)
		{
			if (oxygen > LowOxygenWarningThreshold + recoveryMargin)
				return BuildingOxygenAlertLevel.Normal;
			if (oxygen > CriticalOxygenThreshold + recoveryMargin)
				return BuildingOxygenAlertLevel.Warning;
			return BuildingOxygenAlertLevel.Critical;
		}

		if (previous == BuildingOxygenAlertLevel.Warning)
		{
			if (oxygen <= CriticalOxygenThreshold)
				return BuildingOxygenAlertLevel.Critical;
			return oxygen > LowOxygenWarningThreshold + recoveryMargin
				? BuildingOxygenAlertLevel.Normal
				: BuildingOxygenAlertLevel.Warning;
		}

		if (oxygen <= CriticalOxygenThreshold)
			return BuildingOxygenAlertLevel.Critical;
		return oxygen <= LowOxygenWarningThreshold
			? BuildingOxygenAlertLevel.Warning
			: BuildingOxygenAlertLevel.Normal;
	}

	private static void ShowBuildingOxygenAlert(
		Building building,
		FloatingTextPreset preset,
		string message)
	{
		if (building == null || GameContext.HasInstance == false)
			return;

		BuildingFootprintService footprintService = GameContext.Instance.BuildingFootprintService;
		if (footprintService == null ||
			footprintService.TryGetFootprint(building.RuntimeBuildingId, out BuildingFootprintRecord footprint) == false ||
			footprint == null)
		{
			return;
		}

		Vector3 worldPosition = new(footprint.Center.x, footprint.Floor + 1.5f, footprint.Center.y);
		GameContext.Instance.FloatingTextManager?.ShowWorld(preset, message, worldPosition, 2.0f);
	}

	private static float CalculateSupply(Building building)
	{
		if (building == null)
			return 0f;

		float total = 0f;
		IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
		for (int i = 0; i < addons.Count; ++i)
		{
			if (addons[i] is not IOxygenSupplier supplier)
				continue;

			float efficiency = FacilityEfficiency.GetOperatingEfficiency(building, supplier, supplier);
			total += Mathf.Max(0f, supplier.OxygenSupplyPerTick) * efficiency;
		}

		return total;
	}

	private static void ReportSupplierOperation(
		Building building,
		float requestedSupply,
		float availableSupply,
		float elapsedWeeks)
	{
		if (building == null || requestedSupply <= 0.0f || availableSupply <= 0.0f)
			return;

		float loadRatio = Mathf.Clamp01(requestedSupply / availableSupply);
		IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
		for (int i = 0; i < addons.Count; ++i)
		{
			if (addons[i] is not IOxygenSupplier supplier)
				continue;

			if (FacilityEfficiency.GetOperatingEfficiency(building, supplier, supplier) <= 0.0f)
				continue;

			GameContext.Instance.WearSvc.ReportOperation(supplier, elapsedWeeks, loadRatio);
		}
	}

	private bool ConsumeOxygenForFires()
	{
		if (fireOxygenConsumptionAtMaxIntensity <= 0.0f)
			return false;

		bool changed = false;
		int3 size = GridService.MapSize;
		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					GridCell cell = GridService.GetCell(x, y, z);
					if (cell == null || cell.FireIntensity <= 0.0f || cell.Oxygen <= GridCell.DefaultOxygen)
						continue;

					float consumption = CalculateFireOxygenConsumption(cell);
					changed |= GridService.TrySetOxygen(cell, cell.Oxygen - consumption);
				}
			}
		}

		return changed;
	}

	private float CalculateFireOxygenConsumption(GridCell cell)
	{
		if (cell == null || cell.FireIntensity <= 0.0f || cell.Oxygen <= GridCell.DefaultOxygen)
			return 0.0f;

		float requested = fireOxygenConsumptionAtMaxIntensity *
			Mathf.Clamp01(cell.FireIntensity / FireService.MaximumFireIntensity);
		return Mathf.Min(requested, cell.Oxygen - GridCell.DefaultOxygen);
	}

	private bool ClearOutdoorOxygen()
	{
		bool changed = false;
		int3 size = GridService.MapSize;
		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					GridCell cell = GridService.GetCell(x, y, z);
					if (cell != null && cell.IsIndoor == false)
						changed |= GridService.TrySetOxygen(cell, GridCell.DefaultOxygen);
				}
			}
		}

		return changed;
	}

	private void HandleCellOxygenChanged(GridCell cell, float previous, float current)
	{
		if (isProcessingTick == false)
			OnGridOverlayRefreshRequested?.Invoke();
	}

	private void HandleSpaceRegionsChanged()
	{
		clearOutdoorOnNextTick = true;
		OnGridOverlayRefreshRequested?.Invoke();
	}

	private static bool IsBuildingIndoorCell(GridCell cell, uint buildingId)
	{
		return cell != null && cell.IsIndoor && cell.BuildingId == buildingId;
	}

	private static Color32 GetOverlayColor(GridCell cell)
	{
		if (cell == null || cell.IsIndoor == false)
			return new Color32(0, 0, 0, 0);

		float normalized = Mathf.Clamp01(cell.Oxygen / GridCell.MaximumOxygen);
		Color color = normalized <= 0.5f
			? Color.Lerp(Color.red, Color.yellow, normalized * 2f)
			: Color.Lerp(Color.yellow, Color.cyan, (normalized - 0.5f) * 2f);
		color.a = 1f;
		return color;
	}
}
