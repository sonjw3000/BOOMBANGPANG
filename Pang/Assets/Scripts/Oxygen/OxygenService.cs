using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface IOxygenSupplier : IFacility
{
	float OxygenSupplyPerTick { get; }
}

public sealed class OxygenService : MonoBehaviour, IGridOverlayProvider
{
	[SerializeField, Min(0.0f)] private float fireOxygenConsumptionAtMaxIntensity = 5.0f;

	private readonly Dictionary<uint, List<IOxygenSupplier>> suppliersByBuilding = new();
	private bool eventsBound;
	private bool clearOutdoorOnNextTick = true;
	private bool isProcessingTick;

	public event Action OnGridOverlayRefreshRequested;
	public bool HideZeroAlphaPixels => true;

	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private FacilityManager FacilityManager => GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;

	private void OnEnable()
	{
		BindEvents();
		RebuildSuppliers();
	}

	private void Start()
	{
		BindEvents();
		RebuildSuppliers();
	}

	private void OnDisable() => UnbindEvents();

	public void ResetRuntimeState()
	{
		suppliersByBuilding.Clear();
		clearOutdoorOnNextTick = true;
		isProcessingTick = false;
	}

	public void RebuildRuntimeState()
	{
		RebuildSuppliers();
		clearOutdoorOnNextTick = true;
	}

	public void ProcessSimulationTick()
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
				changed |= ProcessBuilding(buildings[i]);

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
		if (eventsBound || FacilityManager == null || GridService == null)
			return;

		FacilityManager.SubscribeFacilityRegister<IOxygenSupplier>(HandleRegistered, HandleUnregistered);
		GridService.OnCellOxygenChanged += HandleCellOxygenChanged;
		GridService.OnSpaceRegionsChanged += HandleSpaceRegionsChanged;
		eventsBound = true;
	}

	private void UnbindEvents()
	{
		if (eventsBound == false)
			return;

		if (FacilityManager != null)
			FacilityManager.UnsubscribeFacilityRegister<IOxygenSupplier>(HandleRegistered, HandleUnregistered);
		if (GridService != null)
		{
			GridService.OnCellOxygenChanged -= HandleCellOxygenChanged;
			GridService.OnSpaceRegionsChanged -= HandleSpaceRegionsChanged;
		}
		eventsBound = false;
	}

	private void RebuildSuppliers()
	{
		suppliersByBuilding.Clear();
		if (FacilityManager == null)
			return;

		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			uint buildingId = buildingIds[i];
			IReadOnlyList<IOxygenSupplier> suppliers = FacilityManager.GetFacilities<IOxygenSupplier>(buildingId);
			for (int j = 0; j < suppliers.Count; ++j)
				Register(buildingId, suppliers[j]);
		}
	}

	private void HandleRegistered(uint buildingId, IFacility facility)
	{
		if (facility is IOxygenSupplier supplier)
			Register(buildingId, supplier);
	}

	private void HandleUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is not IOxygenSupplier supplier ||
			suppliersByBuilding.TryGetValue(buildingId, out List<IOxygenSupplier> suppliers) == false)
		{
			return;
		}

		suppliers.Remove(supplier);
		if (suppliers.Count == 0)
			suppliersByBuilding.Remove(buildingId);
	}

	private void Register(uint buildingId, IOxygenSupplier supplier)
	{
		if (buildingId == 0 || supplier == null)
			return;

		if (suppliersByBuilding.TryGetValue(buildingId, out List<IOxygenSupplier> suppliers) == false)
		{
			suppliers = new List<IOxygenSupplier>();
			suppliersByBuilding[buildingId] = suppliers;
		}

		if (suppliers.Contains(supplier) == false)
			suppliers.Add(supplier);
	}

	private bool ProcessBuilding(Building building)
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
		float supply = CalculateSupply(building.RuntimeBuildingId);
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

	private float CalculateSupply(uint buildingId)
	{
		if (suppliersByBuilding.TryGetValue(buildingId, out List<IOxygenSupplier> suppliers) == false)
			return 0f;

		float total = 0f;
		for (int i = suppliers.Count - 1; i >= 0; --i)
		{
			IOxygenSupplier supplier = suppliers[i];
			if (supplier is UnityEngine.Object unityObject && unityObject == null)
			{
				suppliers.RemoveAt(i);
				continue;
			}

			GridCell cell = GridService.GetCell(supplier.GridPosition);
			if (IsBuildingIndoorCell(cell, buildingId) == false)
				continue;

			float efficiency = Mathf.Clamp01(GameContext.Instance.PowerSvc.GetPowerEfficiency(supplier));
			total += Mathf.Max(0f, supplier.OxygenSupplyPerTick) * efficiency;
		}

		return total;
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
