using System.Collections.Generic;
using Unity.Mathematics;
using System;

public enum BuildingState
{
	Active,
	PendingDemolition,
	Destroyed,
}

public enum BuildingWorkScope
{
	HomeOnly,
	HomeAndOutdoor,
	CrossBuilding,
	Global,
}

public static class BuildingWorkScopeUtility
{
	public static string ToDisplayString(BuildingWorkScope scope)
	{
		return scope switch
		{
			BuildingWorkScope.HomeOnly => "Home Only",
			BuildingWorkScope.HomeAndOutdoor => "Home + Outdoor",
			BuildingWorkScope.CrossBuilding => "Cross Building",
			BuildingWorkScope.Global => "Global",
			_ => scope.ToString(),
		};
	}
}

public class Building
{
	private string displayName = string.Empty;
	private uint runtimeBuildingId;
	private BuildingState state = BuildingState.Active;
	private BuildingWorkScope workScope = BuildingWorkScope.HomeOnly;
	private CargoProcessStage outboundTargetStage = CargoProcessStage.None;

	private bool overrideCapsuleThreshold = false;
	private float capsuleThresholdPercent = 80.0f;
	private bool suitRemovalAllowed;
	private BuildingItemIndex itemIndex;
	private PowerPort powerPort;
	private int currentPowerConsumption;
	private int addonSlotCapacity;
	private float targetTemperatureCelsius = GridCell.DefaultTemperatureCelsius;

	private bool isRegistered;
	private bool isTrackingTemperature;
	private float occupiedCellTemperatureSum;

	private readonly List<GridCell> occupiedCells;

	private readonly List<IFacility> occupiedFacilities = new();
	private readonly List<BuildingAddon> installedAddons = new();
	private readonly List<CargoPort> occupiedCargoPorts = new();
	private readonly List<CapsuleBuffer> occupiedCapsuleBuffers = new();
	private readonly HashSet<uint> inputBuildingIds = new();
	private readonly HashSet<uint> outputBuildingIds = new();

	// todo
	// airlock 추가시에 적용
	// private List<Airlock> airlocks = new List<Airlock>();
	public string DisplayName => displayName;
	public uint RuntimeBuildingId => runtimeBuildingId;
	public BuildingState State => state;
	public BuildingWorkScope WorkScope => workScope;
	public CargoProcessStage OutboundTargetStage => outboundTargetStage;
	public IReadOnlyList<GridCell> OccupiedCells => occupiedCells;
	public IReadOnlyList<IFacility> OccupiedFacilities => occupiedFacilities;
	public IReadOnlyList<CargoPort> OccupiedCargoPorts => occupiedCargoPorts;
	public IReadOnlyList<CapsuleBuffer> OccupiedCapsuleBuffers => occupiedCapsuleBuffers;
	public IReadOnlyCollection<uint> InputBuildingIds => inputBuildingIds;
	public IReadOnlyCollection<uint> OutputBuildingIds => outputBuildingIds;
	public BuildingItemIndex ItemIndex => itemIndex;
	public PowerPort PowerPort => powerPort;
	public int CurrentPowerConsumption => currentPowerConsumption;
	public int AddonSlotCapacity => addonSlotCapacity;
	public int AvailableAddonSlots => UnityEngine.Mathf.Max(0, addonSlotCapacity - installedAddons.Count);
	public IReadOnlyList<BuildingAddon> InstalledAddons => installedAddons;
	public float TargetTemperatureCelsius => targetTemperatureCelsius;
	public float PowerEfficiency => powerPort != null ? powerPort.PowerEfficiency : 0f;
	public float AverageTemperatureCelsius => occupiedCells.Count > 0
		? occupiedCellTemperatureSum / occupiedCells.Count
		: GridCell.DefaultTemperatureCelsius;

	public bool OverrideCapsuleThreshold => overrideCapsuleThreshold;
	public float CapsuleThresholdPercent => capsuleThresholdPercent;
	public bool SuitRemovalAllowed => suitRemovalAllowed;
	public bool IsSuitRemovalPolicyActive => suitRemovalAllowed && CanControlSuitRemoval();

	private OutboundWorkflowService OutboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;

	public bool CanControlCapsuleThreshold()
	{
		return GameContext.HasInstance &&
			GameContext.Instance.ResearchService?.IsResearched(ResearchIds.WorkflowPolicyOptimization) == true;
	}

	public bool CanControlSuitRemoval()
	{
		return GameContext.HasInstance &&
			GameContext.Instance.ResearchService?.IsResearched(ResearchIds.IndoorWorkProtocols) == true;
	}

	public bool TrySetOverrideCapsuleThreshold(bool value)
	{
		if (CanControlCapsuleThreshold() == false)
			return false;

		if (overrideCapsuleThreshold == value)
			return true;

		SetOverrideCapsuleThreshold(value);
		MarkBuildingRoutingDirty();
		return true;
	}

	public bool TrySetCapsuleThresholdPercent(float value)
	{
		if (CanControlCapsuleThreshold() == false || overrideCapsuleThreshold == false)
			return false;

		float clamped = UnityEngine.Mathf.Clamp(value, 0.0f, 100.0f);
		if (UnityEngine.Mathf.Approximately(capsuleThresholdPercent, clamped))
			return true;

		SetCapsuleThresholdPercent(clamped);
		MarkBuildingRoutingDirty();
		return true;
	}

	internal void SetOverrideCapsuleThreshold(bool value) => overrideCapsuleThreshold = value;
	internal void SetCapsuleThresholdPercent(float value) => capsuleThresholdPercent = UnityEngine.Mathf.Clamp(value, 0.0f, 100.0f);
	internal void SetSuitRemovalAllowed(bool value) => suitRemovalAllowed = value;
	internal void SetAddonSlotCapacity(int value) => addonSlotCapacity = UnityEngine.Mathf.Max(0, value);
	internal void SetTargetTemperatureCelsius(float value) => targetTemperatureCelsius = value;
	internal void SetOutboundTargetStage(CargoProcessStage stage)
	{
		outboundTargetStage = CargoProcessStageUtility.IsDefined(stage)
			? stage
			: CargoProcessStage.None;
	}
	internal void AssignRuntimeBuildingId(uint id) => runtimeBuildingId = id;
	internal void SetRegistered(bool registered)
	{
		if (isRegistered == registered)
			return;

		isRegistered = registered;
		if (registered)
		{
			StartTemperatureTracking();
			OnRegistered();
		}
		else
		{
			OnUnregistered();
			StopTemperatureTracking();
		}
	}
	public void Rename(string newDisplayName) => displayName = newDisplayName;
	public void SetState(BuildingState newState) => state = newState;
	public void SetWorkScope(BuildingWorkScope newWorkScope) => workScope = newWorkScope;
	public bool TrySetOutboundTargetStage(CargoProcessStage stage)
	{
		if (CargoProcessStageUtility.IsDefined(stage) == false)
			return false;

		if (outboundTargetStage == stage)
			return true;

		outboundTargetStage = stage;
		MarkBuildingRoutingDirty();
		return true;
	}

	private void MarkBuildingRoutingDirty()
	{
		if (runtimeBuildingId != 0 && GameContext.HasInstance)
		{
			GameContext.Instance.OBWorkflowSvc?.QueueLaunchSortEvaluation(runtimeBuildingId);
			GameContext.Instance.CapsuleRelocateCoordinator.MarkBuildingDirty(runtimeBuildingId);
		}
	}

	protected static void MarkDockRoutingDirty(CapsuleDock dock)
	{
		if (dock != null && GameContext.HasInstance)
			GameContext.Instance.CapsuleRelocateCoordinator.MarkDirty(dock);
	}

	protected virtual void OnRegistered() { }
	protected virtual void OnUnregistered() { }

	private void StartTemperatureTracking()
	{
		if (isTrackingTemperature) return;
		occupiedCellTemperatureSum = 0.0f;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (cell == null) continue;
			occupiedCellTemperatureSum += cell.TemperatureCelsius;
			cell.OnTemperatureChanged += HandleOccupiedCellTemperatureChanged;
		}
		isTrackingTemperature = true;
	}

	private void StopTemperatureTracking()
	{
		if (isTrackingTemperature == false) return;
		for (int i = 0; i < occupiedCells.Count; ++i)
		{
			GridCell cell = occupiedCells[i];
			if (cell != null) cell.OnTemperatureChanged -= HandleOccupiedCellTemperatureChanged;
		}
		isTrackingTemperature = false;
	}

	private void HandleOccupiedCellTemperatureChanged(GridCell cell, float previous, float current)
	{
		occupiedCellTemperatureSum += current - previous;
	}

	internal bool HasInputBuilding(uint buildingId) => buildingId != 0 && inputBuildingIds.Contains(buildingId);
	internal bool HasOutputBuilding(uint buildingId) => buildingId != 0 && outputBuildingIds.Contains(buildingId);

	public Building(
		string displayName,
		List<GridCell> occupiedCells,
		CargoProcessStage outboundTargetStage = CargoProcessStage.None)
	{
		this.displayName = displayName;
		this.outboundTargetStage = CargoProcessStageUtility.IsDefined(outboundTargetStage)
			? outboundTargetStage
			: CargoProcessStage.None;
		this.occupiedCells = occupiedCells ?? new List<GridCell>();

		itemIndex = new(this);
		itemIndex.OnItemStatusAdded += HandleItemStatusAdded;
	}

	internal bool AddInputBuilding(uint buildingId)
	{
		return buildingId != 0 && buildingId != runtimeBuildingId && inputBuildingIds.Add(buildingId);
	}

	internal bool AddOutputBuilding(uint buildingId)
	{
		return buildingId != 0 && buildingId != runtimeBuildingId && outputBuildingIds.Add(buildingId);
	}

	internal bool RemoveInputBuilding(uint buildingId)
	{
		return buildingId != 0 && inputBuildingIds.Remove(buildingId);
	}

	internal bool RemoveOutputBuilding(uint buildingId)
	{
		return buildingId != 0 && outputBuildingIds.Remove(buildingId);
	}

	internal void ClearBuildingLinks()
	{
		inputBuildingIds.Clear();
		outputBuildingIds.Clear();
	}

	internal bool RegisterFacility(IFacility facility)
	{
		if (facility == null || occupiedFacilities.Contains(facility))
			return false;

		occupiedFacilities.Add(facility);
		if (powerPort == null && facility is PowerPort registeredPowerPort)
			powerPort = registeredPowerPort;
		RecalculatePowerConsumption();
		if (facility is IItemContainer itemContainer)
			ItemIndex.Register(itemContainer, facility);

		if (facility is CargoPort cargoPort && occupiedCargoPorts.Contains(cargoPort) == false)
			occupiedCargoPorts.Add(cargoPort);
		else if (facility is CapsuleBuffer capsuleBuffer && occupiedCapsuleBuffers.Contains(capsuleBuffer) == false)
			occupiedCapsuleBuffers.Add(capsuleBuffer);

		MarkDockRoutingDirty(facility as CapsuleDock);

		return true;
	}

	internal bool UnregisterFacility(IFacility facility)
	{
		if (facility == null)
			return false;

		bool removed = occupiedFacilities.Remove(facility);
		if (facility == powerPort)
			powerPort = FindFirstPowerPort();
		RecalculatePowerConsumption();
		if (facility is IItemContainer itemContainer)
			ItemIndex.Unregister(itemContainer);

		if (facility is CargoPort cargoPort)
			occupiedCargoPorts.Remove(cargoPort);
		else if (facility is CapsuleBuffer capsuleBuffer)
			occupiedCapsuleBuffers.Remove(capsuleBuffer);
		if (removed)
			MarkBuildingRoutingDirty();

		return removed;
	}

	internal bool TryAddAddon(BuildingAddon addon)
	{
		if (addon == null || installedAddons.Contains(addon) || installedAddons.Count >= addonSlotCapacity)
			return false;

		installedAddons.Add(addon);
		RecalculatePowerConsumption();
		return true;
	}

	internal bool TryRemoveAddon(BuildingAddon addon)
	{
		if (addon == null || installedAddons.Remove(addon) == false)
			return false;

		RecalculatePowerConsumption();
		return true;
	}

	internal bool ContainsAddon(BuildingAddon addon)
	{
		return addon != null && installedAddons.Contains(addon);
	}

	public int RecalculatePowerConsumption()
	{
		int totalConsumption = 0;
		for (int i = 0; i < occupiedFacilities.Count; ++i)
		{
			totalConsumption += UnityEngine.Mathf.Max(0, occupiedFacilities[i].PowerConsumption);
		}

		for (int i = 0; i < installedAddons.Count; ++i)
		{
			BuildingAddon addon = installedAddons[i];
			if (addon != null)
				totalConsumption += UnityEngine.Mathf.Max(0, addon.PowerConsumption);
		}

		currentPowerConsumption = totalConsumption;
		return currentPowerConsumption;
	}

	private PowerPort FindFirstPowerPort()
	{
		for (int i = 0; i < occupiedFacilities.Count; ++i)
		{
			if (occupiedFacilities[i] is PowerPort candidate)
				return candidate;
		}

		return null;
	}

	protected virtual bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		CargoCapsule capsule = capsuleBuffer?.DockedCapsule;
		if (capsule == null || capsule.RouteKind != CargoRouteKind.Standard ||
			capsuleBuffer.IsCapsuleEmpty() || outboundTargetStage == CargoProcessStage.None)
		{
			return false;
		}

		bool evaluateLaunchReadiness = outboundTargetStage == CargoProcessStage.LaunchReady;
		bool launchReady = evaluateLaunchReadiness &&
			CargoProcessStageEvaluator.IsLaunchReady(capsule, OutboundWorkflowService);
		if (CargoProcessStageEvaluator.TryEvaluate(
				capsule,
				OutboundWorkflowService,
				launchReady,
				out CargoProcessStage stage) == false ||
			stage != outboundTargetStage)
		{
			return false;
		}

		float workflowThreshold = OutboundWorkflowService != null
			? OutboundWorkflowService.CargoPortThresholdPercent
			: capsuleThresholdPercent;
		float threshold = overrideCapsuleThreshold ? capsuleThresholdPercent : workflowThreshold;
		return capsuleBuffer.FilledPercent >= threshold;
	}

	internal bool CanDispatchOutboundBuffer(CapsuleBuffer capsuleBuffer)
	{
		return IsBufferOutboundReady(capsuleBuffer);
	}

	private void HandleItemStatusAdded(uint itemId, ItemStatus status, IItemContainer container)
	{
		if (container is CapsuleBuffer capsuleBuffer)
			MarkDockRoutingDirty(capsuleBuffer);
	}

}
