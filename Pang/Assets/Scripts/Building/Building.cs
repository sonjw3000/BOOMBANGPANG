using System.Collections.Generic;
using Unity.Mathematics;
using System;

public enum BuildingType
{
	Generic,
	Staging,
	Storage,
	Packing,
	Launch,
}

public static class BuildingTypeUtility
{
	public static string ToDisplayString(BuildingType type)
	{
		return type switch
		{
			BuildingType.Generic => "Generic",
			BuildingType.Staging => "Staging",
			BuildingType.Storage => "Storage",
			BuildingType.Packing => "Packing",
			BuildingType.Launch => "Launch",
			_ => type.ToString(),
		};
	}
}

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
	private BuildingType buildingType = BuildingType.Generic;
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
	private readonly HashSet<InboundCargoPort> pendingInboundPorts = new();
	private readonly HashSet<InboundCargoPort> queuedInboundPorts = new();
	private readonly HashSet<CapsuleBuffer> queuedOutboundBuffers = new();
	private readonly Dictionary<InboundCargoPort, CapsuleBuffer> queuedInboundTargets = new();
	private readonly Dictionary<CapsuleBuffer, OutboundCargoPort> queuedOutboundTargets = new();
	private readonly Dictionary<InboundCargoPort, CapsuleRelocationTask> queuedInboundTaskOwners = new();
	private readonly Dictionary<CapsuleBuffer, CapsuleRelocationTask> queuedOutboundTaskOwners = new();
	private readonly Dictionary<CapsuleLogisticsState, HashSet<CargoCapsule>> capsulesByState = new();
	private readonly Dictionary<CargoCapsule, CapsuleLogisticsState> registeredCapsuleStates = new();

	// item transfer for state
	protected readonly HashSet<ItemStatus> trackingItemStatus = new();
	protected readonly HashSet<IItemContainer> dirtyItemStateContainers = new();

	// todo
	// airlock 추가시에 적용
	// private List<Airlock> airlocks = new List<Airlock>();
	public string DisplayName => displayName;
	public BuildingType Type => buildingType;
	public uint RuntimeBuildingId => runtimeBuildingId;
	public BuildingState State => state;
	public BuildingWorkScope WorkScope => workScope;
	public CargoProcessStage OutboundTargetStage => outboundTargetStage;
	public IReadOnlyList<GridCell> OccupiedCells => occupiedCells;
	public IReadOnlyList<IFacility> OccupiedFacilities => occupiedFacilities;
	public IReadOnlyList<CargoPort> OccupiedCargoPorts => occupiedCargoPorts;
	public IReadOnlyCollection<InboundCargoPort> PendingInboundPorts => pendingInboundPorts;
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

	protected TaskManager TaskManager => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private OutboundWorkflowService OutboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
	private CapsuleBufferService CapsuleBufferService => GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;

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
			: GetDefaultOutboundTargetStage(buildingType);
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
			GameContext.Instance.CapsuleRelocateCoordinator.MarkBuildingDirty(runtimeBuildingId);
	}

	protected static void MarkDockRoutingDirty(CapsuleDock dock)
	{
		if (dock != null && GameContext.HasInstance)
			GameContext.Instance.CapsuleRelocateCoordinator.MarkDirty(dock);
	}

	protected bool IsCapsuleBufferTaskRuleSettled(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null)
			return false;

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		if (ruleManager == null ||
			capsuleBuffer.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId)
		{
			return true;
		}

		return CapsuleBufferService?.IsRuleMatchedBuffer(
			capsuleBuffer,
			capsuleBuffer.DockedCapsule,
			OutboundTargetStage == CargoProcessStage.LaunchReady) == true;
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
	public bool IsItemStatusInterested(ItemStatus status) => trackingItemStatus.Contains(status);

	internal bool TryTakeDirtyItemContainer(out IItemContainer container)
	{
		foreach (IItemContainer candidate in dirtyItemStateContainers)
		{
			container = candidate;
			dirtyItemStateContainers.Remove(candidate);
			return true;
		}

		container = null;
		return false;
	}

	internal void MarkItemContainerDirty(IItemContainer container)
	{
		if (container != null)
			dirtyItemStateContainers.Add(container);
	}

	internal void ClearItemContainerDirty(IItemContainer container)
	{
		if (container != null)
			dirtyItemStateContainers.Remove(container);
	}

	internal bool HasAvailableItemStatus(IItemContainer container, ItemStatus status)
	{
		return TryFindAvailableItem(container, status, out _, out _);
	}

	internal int GetAvailableItemQuantity(IItemContainer container, uint itemId, ItemStatus status)
	{
		if (container == null ||
			ItemIndex.GetContainers(itemId, status).TryGetValue(container, out int quantity) == false)
		{
			return 0;
		}

		int reserved = GetReservedItemQuantity(container, itemId, status);
		return UnityEngine.Mathf.Max(0, quantity - reserved);
	}

	internal bool TryFindAvailableItem(IItemContainer container, ItemStatus status, out uint itemId, out int quantity)
	{
		foreach (var entry in ItemIndex.QuantityByKeyAndContainer)
		{
			if (entry.Key.Status != status ||
				entry.Value.TryGetValue(container, out int storedQuantity) == false)
			{
				continue;
			}

			int reserved = GetReservedItemQuantity(container, entry.Key.ItemId, status);
			int available = UnityEngine.Mathf.Max(0, storedQuantity - reserved);
			if (available <= 0)
				continue;

			itemId = entry.Key.ItemId;
			quantity = available;
			return true;
		}

		itemId = 0;
		quantity = 0;
		return false;
	}

	private int GetReservedItemQuantity(IItemContainer container, uint itemId, ItemStatus status)
	{
		int reserved = ItemIndex.GetReservedContainers(itemId, status).TryGetValue(container, out int reservedQuantity)
			? reservedQuantity
			: 0;

		if (status != ItemStatus.None &&
			ItemIndex.GetReservedContainers(itemId, ItemStatus.None).TryGetValue(container, out int itemOnlyReservedQuantity))
		{
			reserved += itemOnlyReservedQuantity;
		}

		return reserved;
	}

	public Building(string displayName, List<GridCell> occupiedCells, BuildingType buildingType = BuildingType.Generic)
	{
		this.displayName = displayName;
		this.buildingType = buildingType;
		outboundTargetStage = GetDefaultOutboundTargetStage(buildingType);
		this.occupiedCells = occupiedCells ?? new List<GridCell>();

		itemIndex = new(this);
		itemIndex.OnItemStatusAdded += HandleItemStatusAdded;
	}

	public static CargoProcessStage GetDefaultOutboundTargetStage(BuildingType buildingType)
	{
		return buildingType switch
		{
			BuildingType.Staging => CargoProcessStage.Labeled,
			BuildingType.Storage => CargoProcessStage.Picked,
			BuildingType.Packing => CargoProcessStage.Packed,
			BuildingType.Launch => CargoProcessStage.LaunchReady,
			_ => CargoProcessStage.None,
		};
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
		{
			occupiedCargoPorts.Add(cargoPort);
			SubscribeCargoPort(cargoPort);
			EvaluateCargoPortState(cargoPort);
		}
		else if (facility is CapsuleBuffer capsuleBuffer && occupiedCapsuleBuffers.Contains(capsuleBuffer) == false)
		{
			occupiedCapsuleBuffers.Add(capsuleBuffer);
			SubscribeCapsuleBuffer(capsuleBuffer);
			EvaluateCapsuleDockState(capsuleBuffer);
		}

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
		{
			UnregisterCapsule(cargoPort.DockedCapsule);
			UnsubscribeCargoPort(cargoPort);
			occupiedCargoPorts.Remove(cargoPort);
		}
		else if (facility is CapsuleBuffer capsuleBuffer)
		{
			UnregisterCapsule(capsuleBuffer.DockedCapsule);
			UnsubscribeCapsuleBuffer(capsuleBuffer);
			occupiedCapsuleBuffers.Remove(capsuleBuffer);
			queuedOutboundBuffers.Remove(capsuleBuffer);
			RemoveQueuedInboundTarget(capsuleBuffer);
			queuedOutboundTargets.Remove(capsuleBuffer);
		}
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

	private void SubscribeCargoPort(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		cargoPort.OnCapsuleDocked += HandleCapsuleDocked;
		cargoPort.OnCargoUndocking += HandleCargoUndocking;
		cargoPort.OnCapsuleUndocked += HandleCapsuleDockUndocked;
		cargoPort.OnCargoContentChanged += HandleCargoContentChanged;
		if (cargoPort is InboundCargoPort)
			cargoPort.OnCargoQuantityZero += HandleCargoQuantityZero;
		else if (cargoPort is OutboundCargoPort)
			cargoPort.OnCargoQuantityOverPercent += HandleCargoQuantityOverPercent;
	}

	private void EvaluateCargoPortState(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		if (cargoPort.HasCapsule)
			OnCapsuleDocked(cargoPort);
		else
			OnCapsuleDockUndocked(cargoPort);
	}

	private void UnsubscribeCargoPort(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		cargoPort.OnCapsuleDocked -= HandleCapsuleDocked;
		cargoPort.OnCargoUndocking -= HandleCargoUndocking;
		cargoPort.OnCapsuleUndocked -= HandleCapsuleDockUndocked;
		cargoPort.OnCargoContentChanged -= HandleCargoContentChanged;
		if (cargoPort is InboundCargoPort)
			cargoPort.OnCargoQuantityZero -= HandleCargoQuantityZero;
		else if (cargoPort is OutboundCargoPort)
			cargoPort.OnCargoQuantityOverPercent -= HandleCargoQuantityOverPercent;
	}

	private void SubscribeCapsuleBuffer(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		capsuleBuffer.OnCapsuleDocked += HandleCapsuleDocked;
		capsuleBuffer.OnCapsuleUndocking += HandleCapsuleBufferUndocking;
		capsuleBuffer.OnCapsuleUndocked += HandleCapsuleDockUndocked;
		capsuleBuffer.OnCapsuleContentChanged += HandleCapsuleBufferContentChanged;
		capsuleBuffer.OnDockStateChanged += HandleCapsuleDockStateChanged;
	}

	private void EvaluateCapsuleDockState(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		if (capsuleBuffer.HasCapsule)
			OnCapsuleDocked(capsuleBuffer);
		else
			OnCapsuleDockUndocked(capsuleBuffer);

		OnCapsuleDockStateChanged(capsuleBuffer);
	}

	private void UnsubscribeCapsuleBuffer(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		capsuleBuffer.OnCapsuleDocked -= HandleCapsuleDocked;
		capsuleBuffer.OnCapsuleUndocking -= HandleCapsuleBufferUndocking;
		capsuleBuffer.OnCapsuleUndocked -= HandleCapsuleDockUndocked;
		capsuleBuffer.OnCapsuleContentChanged -= HandleCapsuleBufferContentChanged;
		capsuleBuffer.OnDockStateChanged -= HandleCapsuleDockStateChanged;
	}

	private void HandleCargoUndocking(CargoPort cargoPort, CargoCapsule capsule)
	{
		OnCapsuleDockUndocking(cargoPort, capsule);
	}

	private void HandleCargoContentChanged(CargoPort cargoPort)
	{
		MarkDockRoutingDirty(cargoPort);
	}

	private void HandleCargoQuantityZero(CargoPort cargoPort)
	{
		OnCapsuleQuantityZero(cargoPort);
	}

	private void HandleCargoQuantityOverPercent(CargoPort cargoPort)
	{
		OnCapsuleQuantityOverThreshold(cargoPort);
	}

	private void HandleCapsuleDocked(CapsuleDock dock)
	{
		OnCapsuleDocked(dock);
	}

	private void HandleCapsuleBufferUndocking(CapsuleBuffer capsuleBuffer, CargoCapsule capsule)
	{
		OnCapsuleDockUndocking(capsuleBuffer, capsule);
	}

	private void HandleCapsuleDockUndocked(CapsuleDock dock)
	{
		OnCapsuleDockUndocked(dock);
	}

	private void HandleCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleBufferContentChanged(capsuleBuffer);
	}

	private void HandleCapsuleDockStateChanged(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleDockStateChanged(capsuleBuffer);
	}

	private void HandleCapsuleLogisticsStateChanged(CargoCapsule capsule)
	{
		UpdateRegisteredCapsuleState(capsule);
		OnCapsuleStateChanged(capsule);
		ValidateCapsuleRelocationInvariants("capsule-logistics-state-changed", recoverOrphans: false);
	}

	private void OnCapsuleDocked(CapsuleDock dock)
	{
		if (dock == null)
			return;

		RegisterDockedCapsule(dock);
		CargoCapsule capsule = dock.DockedCapsule;
		if (capsule == null)
			return;
		if (capsule.RouteKind != CargoRouteKind.Standard)
			return;

		switch (dock.DockState)
		{
			case CapsuleDockState.IBStandby:
				OnIBStandbyDockDocked(dock, capsule);
				break;
			case CapsuleDockState.IB:
				OnIBDockDocked(dock, capsule);
				break;
			case CapsuleDockState.Empty:
				OnEmptyDockDocked(dock, capsule);
				break;
			case CapsuleDockState.OBStandby:
				OnOBStandbyDockDocked(dock, capsule);
				break;
			case CapsuleDockState.OB:
				OnOBDockDocked(dock, capsule);
				break;
		}

		OnCapsuleStateChanged(capsule);
	}

	protected virtual void OnIBStandbyDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		if (dock is not InboundCargoPort inboundPort || capsule == null)
			return;

		capsule.SetLogisticsState(dock.IsCapsuleEmpty()
			? CapsuleLogisticsState.Empty
			: CapsuleLogisticsState.IB);
		pendingInboundPorts.Add(inboundPort);
		MarkDockRoutingDirty(inboundPort);
	}

	protected virtual void OnIBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		if (dock is not CapsuleBuffer buffer || capsule == null || buffer.IsCapsuleEmpty())
			return;

		RemoveQueuedInboundTarget(buffer);

		MarkDockRoutingDirty(buffer);
	}

	protected virtual void OnEmptyDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		if (capsule == null)
			return;

		MarkDockRoutingDirty(dock);
	}

	protected virtual void OnOBStandbyDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		if (capsule == null)
			return;

		MarkDockRoutingDirty(dock);
	}

	protected virtual void OnOBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		if (capsule != null && dock is OutboundCargoPort)
			capsule.SetLogisticsState(CapsuleLogisticsState.OB);

		if (dock is OutboundCargoPort outboundPort)
			RemoveQueuedOutboundTarget(outboundPort);
		MarkDockRoutingDirty(dock);
	}

	private void OnCapsuleDockUndocking(CapsuleDock dock, CargoCapsule capsule)
	{
		UnregisterCapsule(capsule);
	}

	private void OnCapsuleDockUndocked(CapsuleDock dock)
	{
		if (dock == null)
			return;

		switch (dock)
		{
			case InboundCargoPort inboundPort:
				pendingInboundPorts.Remove(inboundPort);
				queuedInboundPorts.Remove(inboundPort);
				GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(inboundPort);
				break;

			case OutboundCargoPort outboundPort:
				RemoveQueuedOutboundTarget(outboundPort);
				break;

			case CapsuleBuffer capsuleBuffer:
				queuedOutboundBuffers.Remove(capsuleBuffer);
				GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(capsuleBuffer);
				TryEnqueuePendingInboundTasks();
				break;
		}

		MarkBuildingRoutingDirty();
	}

	protected virtual void OnCapsuleQuantityZero(CargoPort cargoPort)
	{
		if (cargoPort is not InboundCargoPort inboundPort)
			return;

		pendingInboundPorts.Remove(inboundPort);
		queuedInboundPorts.Remove(inboundPort);
		queuedInboundTargets.Remove(inboundPort);
		inboundPort.DockedCapsule?.SetLogisticsState(CapsuleLogisticsState.Empty);
		GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(inboundPort);
		MarkDockRoutingDirty(inboundPort);
	}

	protected virtual void OnCapsuleQuantityOverThreshold(CargoPort cargoPort)
	{
	}

	private HashSet<CargoCapsule> GetCapsulesByState(CapsuleLogisticsState state)
	{
		if (capsulesByState.TryGetValue(state, out HashSet<CargoCapsule> capsules) == false)
		{
			capsules = new HashSet<CargoCapsule>();
			capsulesByState[state] = capsules;
		}

		return capsules;
	}

	private void RegisterDockedCapsule(CapsuleDock dock)
	{
		CargoCapsule capsule = dock != null ? dock.DockedCapsule : null;
		if (capsule == null ||
			capsule.RouteKind != CargoRouteKind.Standard ||
			registeredCapsuleStates.ContainsKey(capsule))
			return;

		CapsuleLogisticsState state = capsule.LogisticsState;
		registeredCapsuleStates[capsule] = state;
		GetCapsulesByState(state).Add(capsule);
		capsule.OnLogisticsStateChanged += HandleCapsuleLogisticsStateChanged;
	}

	private void UnregisterCapsule(CargoCapsule capsule)
	{
		if (capsule == null || registeredCapsuleStates.TryGetValue(capsule, out CapsuleLogisticsState state) == false)
			return;

		if (capsulesByState.TryGetValue(state, out HashSet<CargoCapsule> capsules))
			capsules.Remove(capsule);

		registeredCapsuleStates.Remove(capsule);
		capsule.OnLogisticsStateChanged -= HandleCapsuleLogisticsStateChanged;
	}

	private void UpdateRegisteredCapsuleState(CargoCapsule capsule)
	{
		if (capsule == null || registeredCapsuleStates.TryGetValue(capsule, out CapsuleLogisticsState previousState) == false)
			return;

		CapsuleLogisticsState newState = capsule.LogisticsState;
		if (previousState == newState)
			return;

		if (capsulesByState.TryGetValue(previousState, out HashSet<CargoCapsule> previousCapsules))
			previousCapsules.Remove(capsule);

		registeredCapsuleStates[capsule] = newState;
		GetCapsulesByState(newState).Add(capsule);
	}

	protected virtual void OnCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		MarkDockRoutingDirty(capsuleBuffer);
	}

	protected virtual void OnCapsuleRoutingSettled(CapsuleBuffer capsuleBuffer)
	{
	}

	protected virtual void OnCapsuleDockStateChanged(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		MarkDockRoutingDirty(capsuleBuffer);
	}

	protected virtual void OnCapsuleEmpty(CapsuleBuffer capsuleBuffer)
	{
		MarkDockRoutingDirty(capsuleBuffer);
	}

	protected virtual void OnCapsuleOverThreshold(CapsuleBuffer capsuleBuffer)
	{
		MarkDockRoutingDirty(capsuleBuffer);
	}

	private void OnCapsuleStateChanged(CargoCapsule capsule)
	{
		MarkDockRoutingDirty(capsule?.CurrentDock);
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

	private void TryEnqueueInboundTask(InboundCargoPort cargoPort)
	{
		if (cargoPort == null || queuedInboundPorts.Contains(cargoPort) || TaskManager == null)
			return;

		CargoCapsule capsule = cargoPort.DockedCapsule;
		if (capsule == null || capsule.RouteKind != CargoRouteKind.Standard)
			return;
		CapsuleLogisticsState requiredState = cargoPort.IsCapsuleEmpty()
			? CapsuleLogisticsState.Empty
			: CapsuleLogisticsState.IB;
		WorkerTask.TaskType taskType = requiredState == CapsuleLogisticsState.Empty
			? WorkerTask.TaskType.CapsuleSupply
			: WorkerTask.TaskType.IB;

		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			cargoPort,
			cargoPort.DockState,
			requiredState,
			CapsuleDockState.Empty,
			CapsuleRelocateScope.SameBuilding,
			RuntimeBuildingId,
			onMatched: match => EnqueueCapsuleRelocationTask(match, taskType, CapsuleRelocationReason.RuleRouting),
			requireRuleMatchedTarget: true,
			evaluateLaunchReadiness: OutboundTargetStage == CargoProcessStage.LaunchReady));
	}

	private void TryEnqueuePendingInboundTasks()
	{
		if (pendingInboundPorts.Count <= 0)
			return;

		InboundCargoPort[] ports = new InboundCargoPort[pendingInboundPorts.Count];
		pendingInboundPorts.CopyTo(ports);
		for (int i = 0; i < ports.Length; ++i)
			TryEnqueueInboundTask(ports[i]);
	}

	private void TryEvaluateOutbound(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		if (TaskManager == null)
			return;

		if (capsuleBuffer.DockedCapsule?.LogisticsState != CapsuleLogisticsState.OB ||
			IsBufferOutboundReady(capsuleBuffer) == false ||
			queuedOutboundBuffers.Contains(capsuleBuffer))
		{
			GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(capsuleBuffer);
			return;
		}

		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			capsuleBuffer,
			capsuleBuffer.DockState,
			CapsuleLogisticsState.OB,
			CapsuleDockState.OB,
			CapsuleRelocateScope.SameBuilding,
			RuntimeBuildingId,
			onMatched: match => EnqueueCapsuleRelocationTask(match, WorkerTask.TaskType.OB, CapsuleRelocationReason.DestinationNeedsCapsule)));
	}

	internal void OnCapsuleRelocationTaskEnded(CapsuleRelocationTask task)
	{
		if (task == null || task.BuildingId != RuntimeBuildingId)
			return;

		switch (task.Type)
		{
			case WorkerTask.TaskType.IB:
				if (task.SourceDock is InboundCargoPort inboundPort &&
					queuedInboundTaskOwners.TryGetValue(inboundPort, out CapsuleRelocationTask inboundOwner) &&
					ReferenceEquals(inboundOwner, task))
				{
					queuedInboundPorts.Remove(inboundPort);
					queuedInboundTargets.Remove(inboundPort);
					queuedInboundTaskOwners.Remove(inboundPort);
				}
				break;

			case WorkerTask.TaskType.OB:
				if (task.SourceDock is CapsuleBuffer outboundBuffer &&
					queuedOutboundTaskOwners.TryGetValue(outboundBuffer, out CapsuleRelocationTask outboundOwner) &&
					ReferenceEquals(outboundOwner, task))
				{
					queuedOutboundBuffers.Remove(outboundBuffer);
					queuedOutboundTargets.Remove(outboundBuffer);
					queuedOutboundTaskOwners.Remove(outboundBuffer);
				}
				break;
		}

		CapsuleDock sourceDock = task.SourceDock;
		CapsuleRelocateCoordinator coordinator = GameContext.HasInstance
			? GameContext.Instance.CapsuleRelocateCoordinator
			: null;
		if (sourceDock != null && coordinator?.IsPlayerClaimed(sourceDock) == false)
			coordinator.MarkDirty(sourceDock);

		ValidateCapsuleRelocationInvariants("task-ended", recoverOrphans: false);
	}

	internal int ValidateCapsuleRelocationInvariants(string trigger, bool recoverOrphans)
	{
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
		if (recoverOrphans == false)
			return 0;
#endif
		CapsuleRelocateCoordinator coordinator = GameContext.HasInstance
			? GameContext.Instance.CapsuleRelocateCoordinator
			: null;
		TaskManager taskManager = TaskManager;
		List<(CapsuleDock Source, CapsuleRelocationTask Owner, CapsuleDock MappedTarget)> orphanedRelocations =
			recoverOrphans
				? new List<(CapsuleDock, CapsuleRelocationTask, CapsuleDock)>()
				: null;
		int violationCount = 0;

		InboundCargoPort[] inboundPorts = new InboundCargoPort[queuedInboundPorts.Count];
		queuedInboundPorts.CopyTo(inboundPorts);
		for (int i = 0; i < inboundPorts.Length; ++i)
		{
			InboundCargoPort source = inboundPorts[i];
			queuedInboundTaskOwners.TryGetValue(source, out CapsuleRelocationTask owner);
			queuedInboundTargets.TryGetValue(source, out CapsuleBuffer mappedTarget);
			bool hasTaskOwner = IsManagedCapsuleRelocationOwner(
				owner,
				WorkerTask.TaskType.IB,
				source,
				taskManager);
			if (hasTaskOwner)
				continue;

			violationCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			LogCapsuleRelocationInvariantViolation(
				trigger,
				"IB",
				source,
				owner,
				mappedTarget,
				taskManager,
				coordinator,
				recoverOrphans);
#endif
			if (recoverOrphans == false)
				continue;

			queuedInboundPorts.Remove(source);
			queuedInboundTargets.Remove(source);
			queuedInboundTaskOwners.Remove(source);
			orphanedRelocations.Add((source, owner, mappedTarget));
		}

		CapsuleBuffer[] outboundBuffers = new CapsuleBuffer[queuedOutboundBuffers.Count];
		queuedOutboundBuffers.CopyTo(outboundBuffers);
		for (int i = 0; i < outboundBuffers.Length; ++i)
		{
			CapsuleBuffer source = outboundBuffers[i];
			queuedOutboundTaskOwners.TryGetValue(source, out CapsuleRelocationTask owner);
			queuedOutboundTargets.TryGetValue(source, out OutboundCargoPort mappedTarget);
			bool hasTaskOwner = IsManagedCapsuleRelocationOwner(
				owner,
				WorkerTask.TaskType.OB,
				source,
				taskManager);
			if (hasTaskOwner)
				continue;

			violationCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			LogCapsuleRelocationInvariantViolation(
				trigger,
				"OB",
				source,
				owner,
				mappedTarget,
				taskManager,
				coordinator,
				recoverOrphans);
#endif
			if (recoverOrphans == false)
				continue;

			queuedOutboundBuffers.Remove(source);
			queuedOutboundTargets.Remove(source);
			queuedOutboundTaskOwners.Remove(source);
			orphanedRelocations.Add((source, owner, mappedTarget));
		}

		if (recoverOrphans == false)
			return violationCount;

		for (int i = 0; i < orphanedRelocations.Count; ++i)
		{
			(CapsuleDock source, CapsuleRelocationTask owner, CapsuleDock mappedTarget) =
				orphanedRelocations[i];
			bool sourceHasManagedRelocation =
				taskManager?.HasManagedCapsuleRelocationSource(source) == true ||
				taskManager?.HasManagedCapsuleRelocationTarget(source) == true;
			if (sourceHasManagedRelocation == false)
			{
				coordinator?.CancelPendingRequests(source);
				coordinator?.NotifyRelocationEnded(source, null);
			}

			CapsuleDock ownerTarget = owner?.TargetDock;
			TryReleaseOrphanedRelocationTarget(ownerTarget, taskManager, coordinator);
			if (ReferenceEquals(mappedTarget, ownerTarget) == false)
				TryReleaseOrphanedRelocationTarget(mappedTarget, taskManager, coordinator);

			if (sourceHasManagedRelocation == false &&
				source != null &&
				coordinator?.IsPlayerClaimed(source) != true)
			{
				ReevaluateCapsuleDockAvailability(source);
			}
		}

		return violationCount;
	}

	private static void TryReleaseOrphanedRelocationTarget(
		CapsuleDock target,
		TaskManager taskManager,
		CapsuleRelocateCoordinator coordinator)
	{
		if (target == null ||
			coordinator?.IsRelocationTargetActive(target) != true ||
			coordinator.IsPlayerClaimed(target) ||
			taskManager?.HasManagedCapsuleRelocationTarget(target) == true ||
			taskManager?.HasManagedCapsuleRelocationSource(target) == true)
		{
			return;
		}

		coordinator.NotifyRelocationTargetReleased(target);
	}

	private bool IsManagedCapsuleRelocationOwner(
		CapsuleRelocationTask task,
		WorkerTask.TaskType expectedType,
		CapsuleDock source,
		TaskManager taskManager)
	{
		return task != null &&
			task.Type == expectedType &&
			task.BuildingId == RuntimeBuildingId &&
			ReferenceEquals(task.SourceDock, source) &&
			taskManager?.IsManagingTask(task) == true;
	}

	private void LogCapsuleRelocationInvariantViolation(
		string trigger,
		string markerType,
		CapsuleDock source,
		CapsuleRelocationTask owner,
		CapsuleDock mappedTarget,
		TaskManager taskManager,
		CapsuleRelocateCoordinator coordinator,
		bool recovering)
	{
		string sourceName = source != null ? source.name : "None";
		string sourceEntityId = source != null ? source.GetEntityId().ToString() : "None";
		string sourcePosition = source != null ? source.GridPosition.ToString() : "None";
		string taskState = owner != null
			? $"{owner.Type}/{owner.CurrentStatus}/building={owner.BuildingId}/sourceMatch={ReferenceEquals(owner.SourceDock, source)}/managed={taskManager?.IsManagingTask(owner) == true}"
			: "None";
		string targetName = mappedTarget != null ? mappedTarget.name : "None";
		bool coordinatorActive = coordinator?.IsRelocationSourceActive(source) == true;
		bool coordinatorReserved = coordinator?.IsReserved(source) == true;
		bool playerClaimed = coordinator?.IsPlayerClaimed(source) == true;
		bool targetActive = coordinator?.IsRelocationTargetActive(mappedTarget) == true;
		UnityEngine.Debug.LogError(
			$"[CapsuleRelocationInvariant] trigger={trigger}, recovering={recovering}, building={RuntimeBuildingId}, marker={markerType}, source={sourceName}#{sourceEntityId}, position={sourcePosition}, task={taskState}, target={targetName}, coordinatorActive={coordinatorActive}, coordinatorReserved={coordinatorReserved}, playerClaimed={playerClaimed}, targetActive={targetActive}");
	}

	internal void ReevaluateCapsuleDockAvailability(CapsuleDock dock)
	{
		if (dock == null || TaskManager == null || GameContext.HasInstance == false)
			return;

		FacilityManager facilityManager = GameContext.Instance.FacilityMgr;
		if (facilityManager?.IsInvalidating(dock) == true)
			return;

		TaskManager.TryGetManagedCapsuleRelocationSource(
			dock,
			out CapsuleRelocationTask managedRelocation);

		CargoCapsule dockedCapsule = dock.DockedCapsule;
		if (dockedCapsule == null)
		{
			managedRelocation?.RevalidateReturnedRuleRoutingAssignment();
			GameContext.Instance.CapsuleRelocateCoordinator.CancelPendingRequests(dock);
			return;
		}

		if (dockedCapsule.RouteKind != CargoRouteKind.Standard)
			return;

		NormalizeCapsuleState(dock, dockedCapsule);
		CapsuleRelocateCoordinator coordinator = GameContext.Instance.CapsuleRelocateCoordinator;
		if (coordinator.IsPlayerClaimed(dock))
		{
			return;
		}
		if (managedRelocation != null)
		{
			if (managedRelocation.CurrentStatus == WorkerTask.Status.Returned &&
				managedRelocation.RevalidateReturnedRuleRoutingAssignment() == false)
			{
				TaskManager.InvalidateTask(managedRelocation, TaskInvalidationReason.RuleChanged);
				managedRelocation = null;
			}
			else
			{
				if (managedRelocation.CurrentStatus == WorkerTask.Status.Ready &&
					managedRelocation.IsReadyQueueAssignmentValid() == false)
				{
					TaskInvalidationReason reason = managedRelocation.Reason == CapsuleRelocationReason.RuleRouting
						? TaskInvalidationReason.RuleChanged
						: TaskInvalidationReason.DispatchInvalid;
					TaskManager.InvalidateTask(managedRelocation, reason);
				}
				return;
			}
		}
		if (coordinator.IsRelocationSourceActive(dock))
			return;

		if (dock is InboundCargoPort inboundPort)
		{
			pendingInboundPorts.Add(inboundPort);
			TryEnqueueInboundTask(inboundPort);
			return;
		}

		if (dock is not CapsuleBuffer capsuleBuffer)
			return;

		if (dockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
		{
			bool evaluateLaunchReadiness = OutboundTargetStage == CargoProcessStage.LaunchReady;
			bool isRuleMatched = CapsuleBufferService?.IsRuleMatchedBuffer(
				capsuleBuffer,
				dockedCapsule,
				evaluateLaunchReadiness) == true;
			coordinator.NotifyRuleRoutingEvaluated(
				RuntimeBuildingId,
				capsuleBuffer,
				isRuleMatched);
			TryEvaluateOutbound(capsuleBuffer);
			return;
		}

		TryEvaluateBufferRelocation(capsuleBuffer);
	}

	internal void ReevaluateCapsuleRouting()
	{
		for (int i = 0; i < occupiedCargoPorts.Count; ++i)
			ReevaluateCapsuleDockAvailability(occupiedCargoPorts[i]);
		for (int i = 0; i < occupiedCapsuleBuffers.Count; ++i)
			ReevaluateCapsuleDockAvailability(occupiedCapsuleBuffers[i]);
	}

	private void NormalizeCapsuleState(CapsuleDock dock, CargoCapsule capsule)
	{
		if (dock == null || capsule == null)
			return;

		CapsuleLogisticsState normalized;
		if (dock is InboundCargoPort || dock is Rocket)
			normalized = dock.IsCapsuleEmpty() ? CapsuleLogisticsState.Empty : CapsuleLogisticsState.IB;
		else if (dock is OutboundCargoPort)
			normalized = dock.IsCapsuleEmpty() ? CapsuleLogisticsState.Empty : CapsuleLogisticsState.OB;
		else if (dock is CapsuleBuffer buffer)
		{
			if (buffer.IsCapsuleEmpty())
				normalized = CapsuleLogisticsState.Empty;
			else if (TaskManager?.HasManagedPickingOutputDependency(buffer) == true)
				normalized = CapsuleLogisticsState.Inside;
			else
				normalized = IsBufferOutboundReady(buffer)
					? CapsuleLogisticsState.OB
					: CapsuleLogisticsState.Inside;
		}
		else
			return;

		capsule.SetLogisticsState(normalized);
	}

	private void TryEvaluateBufferRelocation(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null || TaskManager == null)
			return;

		CargoCapsule capsule = capsuleBuffer.DockedCapsule;
		if (capsule == null ||
			(capsule.LogisticsState != CapsuleLogisticsState.Empty &&
			 capsule.LogisticsState != CapsuleLogisticsState.Inside))
		{
			return;
		}

		bool evaluateLaunchReadiness = OutboundTargetStage == CargoProcessStage.LaunchReady;
		CapsuleRelocateCoordinator coordinator = GameContext.Instance.CapsuleRelocateCoordinator;
		if (CapsuleBufferService?.IsRuleMatchedBuffer(
				capsuleBuffer,
				capsule,
				evaluateLaunchReadiness) == true)
		{
			coordinator.CancelPendingRequests(capsuleBuffer);
			coordinator.NotifyRuleRoutingEvaluated(RuntimeBuildingId, capsuleBuffer, isRuleMatched: true);
			OnCapsuleRoutingSettled(capsuleBuffer);
			return;
		}

		coordinator.NotifyRuleRoutingEvaluated(RuntimeBuildingId, capsuleBuffer, isRuleMatched: false);

		WorkerTask.TaskType taskType = capsule.LogisticsState == CapsuleLogisticsState.Empty
			? WorkerTask.TaskType.CapsuleSupply
			: WorkerTask.TaskType.CapsuleClear;
		coordinator.RequestSend(new CapsuleRelocateSendRequest(
			capsuleBuffer,
			capsuleBuffer.DockState,
			capsule.LogisticsState,
			CapsuleDockState.Empty,
			CapsuleRelocateScope.SameBuilding,
			RuntimeBuildingId,
			onMatched: match => EnqueueCapsuleRelocationTask(match, taskType, CapsuleRelocationReason.RuleRouting),
			requireRuleMatchedTarget: true,
			evaluateLaunchReadiness: evaluateLaunchReadiness));
	}

	private bool EnqueueCapsuleRelocationTask(
		CapsuleRelocateMatch match,
		WorkerTask.TaskType taskType,
		CapsuleRelocationReason reason)
	{
		if (TaskManager == null || match.SourceDock == null || match.TargetDock == null)
			return false;

		CapsuleRelocationTask task = new(taskType, match.SourceDock, match.TargetDock, RuntimeBuildingId, reason);
		TaskManager.EnqueueTask(task);
		OnCapsuleRelocationTaskBuilt(task);
		return true;
	}

	internal void OnCapsuleRelocationTaskBuilt(CapsuleRelocationTask task)
	{
		if (task == null)
			return;

		switch (task.Type)
		{
			case WorkerTask.TaskType.IB:
				if (task.SourceDock is not InboundCargoPort sourcePort)
					return;

				queuedInboundPorts.Add(sourcePort);
				queuedInboundTaskOwners[sourcePort] = task;
				if (task.TargetDock is CapsuleBuffer targetBuffer)
					queuedInboundTargets[sourcePort] = targetBuffer;
				break;

			case WorkerTask.TaskType.CapsuleClear:
			case WorkerTask.TaskType.CapsuleSupply:
				break;

			case WorkerTask.TaskType.OB:
				if (task.SourceDock is not CapsuleBuffer sourceBuffer)
					return;

				queuedOutboundBuffers.Add(sourceBuffer);
				queuedOutboundTaskOwners[sourceBuffer] = task;
				if (task.TargetDock is OutboundCargoPort targetPort)
					queuedOutboundTargets[sourceBuffer] = targetPort;
				break;
		}
	}

	private void RemoveQueuedInboundTarget(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null || queuedInboundTargets.Count <= 0)
			return;

		InboundCargoPort[] ports = new InboundCargoPort[queuedInboundTargets.Count];
		queuedInboundTargets.Keys.CopyTo(ports, 0);
		for (int i = 0; i < ports.Length; ++i)
		{
			InboundCargoPort port = ports[i];
			if (port == null || queuedInboundTargets.TryGetValue(port, out CapsuleBuffer targetBuffer) == false || targetBuffer != capsuleBuffer)
				continue;

			queuedInboundTargets.Remove(port);
		}
	}

	private void RemoveQueuedOutboundTarget(OutboundCargoPort cargoPort)
	{
		if (cargoPort == null || queuedOutboundTargets.Count <= 0)
			return;

		CapsuleBuffer[] buffers = new CapsuleBuffer[queuedOutboundTargets.Count];
		queuedOutboundTargets.Keys.CopyTo(buffers, 0);
		for (int i = 0; i < buffers.Length; ++i)
		{
			CapsuleBuffer buffer = buffers[i];
			if (buffer == null || queuedOutboundTargets.TryGetValue(buffer, out OutboundCargoPort targetPort) == false || targetPort != cargoPort)
				continue;

			queuedOutboundTargets.Remove(buffer);
		}
	}

	// for item transfer
	private void HandleItemStatusAdded(uint itemId, ItemStatus status, IItemContainer container)
	{
		if (container == null)
			return;
		if (container is CapsuleBuffer capsuleBuffer)
			MarkDockRoutingDirty(capsuleBuffer);

		if (trackingItemStatus.Contains(status))
		{
			dirtyItemStateContainers.Add(container);
			OnTrackedItemStatusAdded(itemId, status, container);
		}
	}

	protected virtual void OnTrackedItemStatusAdded(uint itemId, ItemStatus status, IItemContainer container)
	{
	}

}
