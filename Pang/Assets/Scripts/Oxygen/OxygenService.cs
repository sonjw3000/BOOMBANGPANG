using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface IOxygenSupplier : IHealth, IWearable
{
	float OxygenSupplyPerTick { get; }
}

public sealed class OxygenService : MonoBehaviour, IGridOverlayProvider
{
	[SerializeField, Min(0.0f)] private float fireOxygenConsumptionAtMaxIntensity = 5.0f;

	private bool eventsBound;
	private bool clearOutdoorOnNextTick = true;
	private bool isProcessingTick;

	public event Action OnGridOverlayRefreshRequested;
	public bool HideZeroAlphaPixels => true;

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
	}

	public void RebuildRuntimeState()
	{
		clearOutdoorOnNextTick = true;
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

		IReadOnlyList<GridCell> occupiedCells = building.OccupiedCells;
		int indoorCellCount = 0;
		float oxygenSum = 0f;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId) == false)
				continue;

			++indoorCellCount;
			oxygenSum += cell.Oxygen;
		}

		if (indoorCellCount == 0)
			return false;

		float average = oxygenSum / indoorCellCount;
		float supply = CalculateSupply(building);
		float requestedSupply = (GridCell.MaximumOxygen - average) * indoorCellCount;
		ReportSupplierOperation(building, requestedSupply, supply, context.ElapsedWeeks);
		float next = Mathf.Clamp(average + supply / indoorCellCount, GridCell.DefaultOxygen, GridCell.MaximumOxygen);

		bool changed = false;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (IsBuildingIndoorCell(cell, building.RuntimeBuildingId))
				changed |= GridService.TrySetOxygen(cell, next);
		}

		return changed;
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

					float consumption = fireOxygenConsumptionAtMaxIntensity *
						Mathf.Clamp01(cell.FireIntensity / FireService.MaximumFireIntensity);
					changed |= GridService.TrySetOxygen(cell, cell.Oxygen - consumption);
				}
			}
		}

		return changed;
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
