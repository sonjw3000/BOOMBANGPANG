using System.Collections.Generic;
using Unity.Mathematics;

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

	private bool overrideCapsuleThreshold = false;
	private float capsuleThresholdPercent = 80.0f;

	private bool isRegistered;

	private readonly List<GridCell> occupiedCells;

	private readonly List<IFacility> occupiedFacilities = new();
	private readonly List<CargoPort> occupiedCargoPorts = new();
	private readonly List<CapsuleBuffer> occupiedCapsuleBuffers = new();
	private readonly HashSet<uint> inputBuildingIds = new();
	private readonly HashSet<uint> outputBuildingIds = new();
	private readonly HashSet<InboundCargoPort> pendingInboundPorts = new();
	private readonly HashSet<InboundCargoPort> queuedInboundPorts = new();
	private readonly HashSet<CapsuleBuffer> queuedOutboundBuffers = new();
	private readonly HashSet<CapsuleBuffer> queuedBufferRelocationSources = new();
	private readonly HashSet<CapsuleBuffer> queuedBufferRelocationTargets = new();
	private readonly Dictionary<InboundCargoPort, CapsuleBuffer> queuedInboundTargets = new();
	private readonly Dictionary<CapsuleBuffer, OutboundCargoPort> queuedOutboundTargets = new();
	private readonly Dictionary<CapsuleLogisticsState, HashSet<CargoCapsule>> capsulesByState = new();
	private readonly Dictionary<CargoCapsule, CapsuleLogisticsState> registeredCapsuleStates = new();
	// todo
	// airlock 추가시에 적용
	// private List<Airlock> airlocks = new List<Airlock>();
	public string DisplayName => displayName;
	public BuildingType Type => buildingType;
	public uint RuntimeBuildingId => runtimeBuildingId;
	public BuildingState State => state;
	public BuildingWorkScope WorkScope => workScope;
	public IReadOnlyList<GridCell> OccupiedCells => occupiedCells;
	public IReadOnlyList<IFacility> OccupiedFacilities => occupiedFacilities;
	public IReadOnlyList<CargoPort> OccupiedCargoPorts => occupiedCargoPorts;
	public IReadOnlyCollection<InboundCargoPort> PendingInboundPorts => pendingInboundPorts;
	public IReadOnlyList<CapsuleBuffer> OccupiedCapsuleBuffers => occupiedCapsuleBuffers;
	public IReadOnlyCollection<uint> InputBuildingIds => inputBuildingIds;
	public IReadOnlyCollection<uint> OutputBuildingIds => outputBuildingIds;

	public bool OverrideCapsuleThreshold => overrideCapsuleThreshold;
	public float CapsuleThresholdPercent => capsuleThresholdPercent;

	private TaskManager TaskManager => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private ZoneManager ZoneManager => GameContext.HasInstance ? GameContext.Instance.ZoneMgr : null;
	private OutboundWorkflowService OutboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
	
	public Building(string displayName, List<GridCell> occupiedCells, BuildingType buildingType = BuildingType.Generic)
	{
		this.displayName = displayName;
		this.buildingType = buildingType;
		this.occupiedCells = occupiedCells ?? new List<GridCell>();
	}

	public void SetOverrideCapsuleThreshold(bool value)
	{
		overrideCapsuleThreshold = value;
	}

	public void SetCapsuleThresholdPercent(float value)
	{
		capsuleThresholdPercent = UnityEngine.Mathf.Clamp(value, 0.0f, 100.0f);
	}

	internal void AssignRuntimeBuildingId(uint id)
	{
		runtimeBuildingId = id;
	}

	internal void SetRegistered(bool registered)
	{
		isRegistered = registered;
	}

	public void Rename(string newDisplayName)
	{
		displayName = newDisplayName;
	}

	public void SetState(BuildingState newState)
	{
		state = newState;
	}

	public void SetWorkScope(BuildingWorkScope newWorkScope)
	{
		workScope = newWorkScope;
	}

	internal bool HasInputBuilding(uint buildingId) => buildingId != 0 && inputBuildingIds.Contains(buildingId);
	internal bool HasOutputBuilding(uint buildingId) => buildingId != 0 && outputBuildingIds.Contains(buildingId);

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
			EvaluateCapsuleBufferState(capsuleBuffer);
		}

		return true;
	}

	internal bool UnregisterFacility(IFacility facility)
	{
		if (facility == null)
			return false;

		bool removed = occupiedFacilities.Remove(facility);
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
			queuedBufferRelocationSources.Remove(capsuleBuffer);
			queuedBufferRelocationTargets.Remove(capsuleBuffer);
			RemoveQueuedInboundTarget(capsuleBuffer);
			queuedOutboundTargets.Remove(capsuleBuffer);
		}

		return removed;
	}

	private void SubscribeCargoPort(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		cargoPort.OnCargoDocked += HandleCargoDocked;
		cargoPort.OnCargoUndocking += HandleCargoUndocking;
		cargoPort.OnCargoUndocked += HandleCargoUndocked;
		if (cargoPort is InboundCargoPort)
			cargoPort.OnCargoQuantityZero += HandleCargoQuantityZero;
		else if (cargoPort is OutboundCargoPort)
			cargoPort.OnCargoQuantityOverPercent += HandleCargoQuantityOverPercent;
	}

	private void EvaluateCargoPortState(CargoPort cargoPort)
	{
			if (cargoPort is InboundCargoPort inboundCargoPort)
			{
				if (inboundCargoPort.HasCapsule && inboundCargoPort.IsCapsuleEmpty() == false)
					OnInboundPortDocked(inboundCargoPort);
				else
					OnInboundPortUndocked(inboundCargoPort);

			return;
		}

			if (cargoPort is OutboundCargoPort outboundCargoPort)
			{
				if (outboundCargoPort.CanPutBox())
					OnOutboundPortUndocked(outboundCargoPort);
				else
					OnOutboundPortDocked(outboundCargoPort);
			}
	}

	private void UnsubscribeCargoPort(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		cargoPort.OnCargoDocked -= HandleCargoDocked;
		cargoPort.OnCargoUndocking -= HandleCargoUndocking;
		cargoPort.OnCargoUndocked -= HandleCargoUndocked;
		if (cargoPort is InboundCargoPort)
			cargoPort.OnCargoQuantityZero -= HandleCargoQuantityZero;
		else if (cargoPort is OutboundCargoPort)
			cargoPort.OnCargoQuantityOverPercent -= HandleCargoQuantityOverPercent;
	}

	private void SubscribeCapsuleBuffer(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		capsuleBuffer.OnCapsuleDocked += HandleCapsuleBufferDocked;
		capsuleBuffer.OnCapsuleUndocking += HandleCapsuleBufferUndocking;
		capsuleBuffer.OnCapsuleUndocked += HandleCapsuleBufferUndocked;
		capsuleBuffer.OnCapsuleContentChanged += HandleCapsuleBufferContentChanged;
		capsuleBuffer.OnBufferStateChanged += HandleCapsuleBufferStateChanged;
	}

	private void EvaluateCapsuleBufferState(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		if (capsuleBuffer.HasCapsule)
			OnCapsuleBufferDocked(capsuleBuffer);
		else
			OnCapsuleBufferUndocked(capsuleBuffer);

		OnCapsuleBufferStateChanged(capsuleBuffer);
	}

	private void UnsubscribeCapsuleBuffer(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		capsuleBuffer.OnCapsuleDocked -= HandleCapsuleBufferDocked;
		capsuleBuffer.OnCapsuleUndocking -= HandleCapsuleBufferUndocking;
		capsuleBuffer.OnCapsuleUndocked -= HandleCapsuleBufferUndocked;
		capsuleBuffer.OnCapsuleContentChanged -= HandleCapsuleBufferContentChanged;
		capsuleBuffer.OnBufferStateChanged -= HandleCapsuleBufferStateChanged;
	}

	private void HandleCargoDocked(CargoPort cargoPort)
	{
		if (cargoPort is InboundCargoPort inboundCargoPort)
			OnInboundPortDocked(inboundCargoPort);
		else if (cargoPort is OutboundCargoPort outboundCargoPort)
			OnOutboundPortDocked(outboundCargoPort);
	}

	private void HandleCargoUndocking(CargoPort cargoPort, CargoCapsule capsule)
	{
		UnregisterCapsule(capsule);
	}

	private void HandleCargoUndocked(CargoPort cargoPort)
	{
		if (cargoPort is InboundCargoPort inboundCargoPort)
			OnInboundPortUndocked(inboundCargoPort);
		else if (cargoPort is OutboundCargoPort outboundCargoPort)
			OnOutboundPortUndocked(outboundCargoPort);
	}

	private void HandleCargoQuantityZero(CargoPort cargoPort)
	{
		if (cargoPort is InboundCargoPort inboundCargoPort)
			OnInboundPortQuantityZero(inboundCargoPort);
	}

	private void HandleCargoQuantityOverPercent(CargoPort cargoPort)
	{
		if (cargoPort is OutboundCargoPort outboundCargoPort)
			OnOutboundPortQuantityOverPercent(outboundCargoPort);
	}

	private void HandleCapsuleBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleBufferDocked(capsuleBuffer);
	}

	private void HandleCapsuleBufferUndocking(CapsuleBuffer capsuleBuffer, CargoCapsule capsule)
	{
		OnCapsuleBufferUndocking(capsuleBuffer, capsule);
	}

	private void HandleCapsuleBufferUndocked(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleBufferUndocked(capsuleBuffer);
	}

	private void HandleCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleBufferContentChanged(capsuleBuffer);
	}

	private void HandleCapsuleBufferStateChanged(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleBufferStateChanged(capsuleBuffer);
	}

	private void HandleCapsuleLogisticsStateChanged(CargoCapsule capsule)
	{
		UpdateRegisteredCapsuleState(capsule);
		OnCapsuleStateChanged(capsule);
	}

	protected virtual void OnInboundPortDocked(InboundCargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		RegisterDockedCapsule(cargoPort);
		CargoCapsule capsule = cargoPort.DockedCapsule;
		if (capsule == null)
			return;

		if (capsule.LogisticsState != CapsuleLogisticsState.IB)
			capsule.SetLogisticsState(CapsuleLogisticsState.IB);
		else
			OnCapsuleStateChanged(capsule);
	}

	protected virtual void OnInboundPortUndocked(InboundCargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		pendingInboundPorts.Remove(cargoPort);
		queuedInboundPorts.Remove(cargoPort);
		TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetInboundRequestKey(cargoPort));
	}

	protected virtual void OnInboundPortQuantityZero(InboundCargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		pendingInboundPorts.Remove(cargoPort);
		queuedInboundPorts.Remove(cargoPort);
		queuedInboundTargets.Remove(cargoPort);
		cargoPort.DockedCapsule?.SetLogisticsState(CapsuleLogisticsState.Empty);
		TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetInboundRequestKey(cargoPort));
	}

	protected virtual void OnOutboundPortUndocked(OutboundCargoPort cargoPort)
	{
		RemoveQueuedOutboundTarget(cargoPort);
	}

	protected virtual void OnOutboundPortDocked(OutboundCargoPort cargoPort)
	{
		RegisterDockedCapsule(cargoPort);
		RemoveQueuedOutboundTarget(cargoPort);
	}

	protected virtual void OnOutboundPortQuantityOverPercent(OutboundCargoPort cargoPort)
	{
	}

	protected virtual void OnCapsuleBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		RegisterDockedCapsule(capsuleBuffer);
		switch (capsuleBuffer?.BufferState)
		{
			case CapsuleBufferState.IBOnly:
				OnInboundBufferDocked(capsuleBuffer);
				break;
			case CapsuleBufferState.Empty:
				OnEmptyBufferDocked(capsuleBuffer);
				break;
			case CapsuleBufferState.OBOnly:
				OnOutboundBufferDocked(capsuleBuffer);
				break;
		}

		OnCapsuleStateChanged(capsuleBuffer?.DockedCapsule);
		RemoveQueuedInboundTarget(capsuleBuffer);
		queuedBufferRelocationTargets.Remove(capsuleBuffer);
		TryEvaluatePackingIngress(capsuleBuffer);
	}

	protected virtual void OnInboundBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null || capsuleBuffer.IsCapsuleEmpty())
			return;

		if (capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.IB)
			capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.IB);
	}

	protected virtual void OnEmptyBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null)
			return;

		if (capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.Empty)
			capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		else
			OnCapsuleStateChanged(capsuleBuffer.DockedCapsule);
	}

	protected virtual void OnOutboundBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null)
			return;

		if (capsuleBuffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.Empty)
			capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.OBStandby);
		else
			OnCapsuleStateChanged(capsuleBuffer.DockedCapsule);
	}

	protected virtual void OnCapsuleBufferUndocking(CapsuleBuffer capsuleBuffer, CargoCapsule capsule)
	{
		UnregisterCapsule(capsule);
	}

	protected virtual void OnCapsuleBufferUndocked(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		queuedOutboundBuffers.Remove(capsuleBuffer);
		queuedBufferRelocationSources.Remove(capsuleBuffer);
		queuedBufferRelocationTargets.Remove(capsuleBuffer);
		TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetOutboundRequestKey(capsuleBuffer));
		TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetBufferRelocationRequestKey(WorkerTask.TaskType.CapsuleClear, capsuleBuffer));
		TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetBufferRelocationRequestKey(WorkerTask.TaskType.CapsuleSupply, capsuleBuffer));
		TryEnqueuePendingInboundTasks();
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
		if (capsule == null || registeredCapsuleStates.ContainsKey(capsule))
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

	private bool TryFindDockedCapsule(
		CapsuleLogisticsState state,
		System.Predicate<CargoCapsule> predicate,
		out CargoCapsule capsule)
	{
		capsule = null;
		if (capsulesByState.TryGetValue(state, out HashSet<CargoCapsule> capsules) == false)
			return false;

		foreach (CargoCapsule candidate in capsules)
		{
			if (candidate == null || candidate.CurrentBuffer == null)
				continue;

			if (predicate != null && predicate(candidate) == false)
				continue;

			capsule = candidate;
			return true;
		}

		return false;
	}

	protected virtual void OnCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule != null)
		{
			CargoCapsule capsule = capsuleBuffer.DockedCapsule;
			if (capsule.LogisticsState == CapsuleLogisticsState.IB && capsuleBuffer.IsCapsuleEmpty())
				OnCapsuleEmpty(capsuleBuffer);
			else if (capsule.LogisticsState == CapsuleLogisticsState.OBStandby && IsBufferOutboundReady(capsuleBuffer))
				OnCapsuleOverThreshold(capsuleBuffer);
		}

		TryEvaluatePackingIngress(capsuleBuffer);
	}

	protected virtual void OnCapsuleBufferStateChanged(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		if (capsuleBuffer.HasCapsule)
		{
			switch (capsuleBuffer.BufferState)
			{
				case CapsuleBufferState.IBOnly:
					OnInboundBufferDocked(capsuleBuffer);
					break;
				case CapsuleBufferState.Empty:
					OnEmptyBufferDocked(capsuleBuffer);
					break;
				case CapsuleBufferState.OBOnly:
					OnOutboundBufferDocked(capsuleBuffer);
					break;
			}
		}

		if (capsuleBuffer.CanReceiveFromInbound())
			TryEnqueuePendingInboundTasks();

		TryEvaluatePackingIngress(capsuleBuffer);
	}

	protected virtual void OnCapsuleEmpty(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null ||
			capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.IB)
		{
			return;
		}

		capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.Empty);
	}

	protected virtual void OnCapsuleOverThreshold(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null ||
			capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OBStandby)
		{
			return;
		}

		capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.OB);
	}

	private void OnCapsuleStateChanged(CargoCapsule capsule)
	{
		if (capsule == null)
			return;

		switch (capsule.LogisticsState)
		{
			case CapsuleLogisticsState.IB:
				if (capsule.CurrentDock is InboundCargoPort inboundPort && capsule.Stacks.Count > 0)
				{
					pendingInboundPorts.Add(inboundPort);
					TryEnqueueInboundTask(inboundPort);
				}
				break;

			case CapsuleLogisticsState.Empty:
				TryEvaluateEmptyCapsuleRelocations();
				break;

			case CapsuleLogisticsState.OBStandby:
				if (capsule.CurrentBuffer != null && IsBufferOutboundReady(capsule.CurrentBuffer))
					OnCapsuleOverThreshold(capsule.CurrentBuffer);
				break;

			case CapsuleLogisticsState.OB:
				if (capsule.CurrentBuffer != null)
					TryEvaluateOutbound(capsule.CurrentBuffer);
				break;
		}
	}

	protected virtual bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		return false;
	}

	internal CapsuleBuffer ResolveInboundBufferTarget(in int3 from, ZoneFilter zoneFilter = default)
	{
		return FindClosestCapsuleBuffer(
			from,
			InteractionKind.Put,
			candidate =>
				candidate != null &&
				candidate.CanReceiveFromInbound() &&
				queuedInboundTargets.ContainsValue(candidate) == false &&
				zoneFilter.Matches(ZoneManager, candidate));
	}

	internal OutboundCargoPort ResolveOutboundPortTarget(in int3 from, ZoneFilter zoneFilter = default)
	{
		return FindClosestOutboundPort(
			from,
			candidate =>
				candidate != null &&
				candidate.CanPutBox() &&
				queuedOutboundTargets.ContainsValue(candidate) == false &&
				zoneFilter.Matches(ZoneManager, candidate));
	}

	internal InboundCargoPort ResolveLinkedInboundPortTarget(in int3 from, ZoneFilter zoneFilter = default)
	{
		if (GridService == null || BuildingManager == null || outputBuildingIds.Count <= 0)
			return null;

		InboundCargoPort bestCandidate = null;
		int bestScore = int.MaxValue;
		foreach (uint targetBuildingId in outputBuildingIds)
		{
			if (BuildingManager.TryGetBuilding(targetBuildingId, out Building targetBuilding) == false || targetBuilding == null)
				continue;

			for (int i = 0; i < targetBuilding.occupiedCargoPorts.Count; ++i)
			{
				if (targetBuilding.occupiedCargoPorts[i] is not InboundCargoPort candidate ||
					candidate.CanPutBox() == false ||
					zoneFilter.Matches(ZoneManager, candidate) == false)
					continue;

				if (InteractionPointSelector.TryGetInteractionPoint(candidate, InteractionKind.Put, from, out _, out int score) == false)
					continue;

				if (score >= bestScore)
					continue;

				bestScore = score;
				bestCandidate = candidate;
			}
		}

		return bestCandidate;
	}

	private void TryEnqueueInboundTask(InboundCargoPort cargoPort)
	{
		if (cargoPort == null || queuedInboundPorts.Contains(cargoPort) || TaskManager == null)
			return;

		if (cargoPort.IsCapsuleEmpty())
			return;

		TaskManager.EnqueueTaskBuildRequest(new CapsuleRelocationTaskBuildRequest(
			cargoPort,
			RuntimeBuildingId,
			WorkerTask.TaskType.IB,
			CapsuleRelocationReason.SourceMustClear));
	}

	private static int3 ResolveInteractionOrigin(BoxInteraction interactionTarget, InteractionKind interactionKind)
	{
		if (interactionTarget == null)
			return default;

		if (interactionTarget.InteractionPointMap != null &&
			interactionTarget.InteractionPointMap.ContainsKey(interactionKind) &&
			interactionTarget.InteractionPointMap[interactionKind] != null &&
			interactionTarget.InteractionPointMap[interactionKind].Count > 0)
		{
			return interactionTarget.GetClosestInteractionPoint(interactionKind, interactionTarget.GridPosition);
		}

		return interactionTarget.GridPosition;
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

		if (IsBufferOutboundReady(capsuleBuffer) == false || queuedOutboundBuffers.Contains(capsuleBuffer))
		{
			TaskManager.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetOutboundRequestKey(capsuleBuffer));
			return;
		}

		TaskManager.EnqueueTaskBuildRequest(new CapsuleRelocationTaskBuildRequest(
			capsuleBuffer,
			RuntimeBuildingId,
			WorkerTask.TaskType.OB,
			CapsuleRelocationReason.DestinationNeedsCapsule));
	}

	private void TryEvaluateBufferRelocation(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null || TaskManager == null)
			return;

		if (capsuleBuffer.DockedCapsule == null ||
			capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.Empty)
		{
			return;
		}

		if (capsuleBuffer.BufferState == CapsuleBufferState.IBOnly)
		{
			TryEnqueueBufferRelocation(
				capsuleBuffer,
				WorkerTask.TaskType.CapsuleClear,
				CapsuleBufferState.Empty,
				CapsuleRelocationReason.StateMismatch);
			return;
		}

		if (capsuleBuffer.BufferState == CapsuleBufferState.Empty)
		{
			TryEnqueueBufferRelocation(
				capsuleBuffer,
				WorkerTask.TaskType.CapsuleSupply,
				CapsuleBufferState.OBOnly,
				CapsuleRelocationReason.DestinationNeedsCapsule);
		}
	}

	private void TryEvaluateEmptyCapsuleRelocations()
	{
		if (TaskManager == null ||
			capsulesByState.TryGetValue(CapsuleLogisticsState.Empty, out HashSet<CargoCapsule> capsules) == false ||
			capsules.Count <= 0)
		{
			return;
		}

		List<CargoCapsule> candidates = new(capsules);
		for (int i = 0; i < candidates.Count; ++i)
			TryEvaluateBufferRelocation(candidates[i]?.CurrentBuffer);
	}

	private void TryEnqueueBufferRelocation(
		CapsuleBuffer sourceBuffer,
		WorkerTask.TaskType taskType,
		CapsuleBufferState targetState,
		CapsuleRelocationReason reason)
	{
		if (sourceBuffer == null ||
			sourceBuffer.HasCapsule == false ||
			sourceBuffer.DockedCapsule == null ||
			sourceBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.Empty ||
			sourceBuffer.IsCapsuleEmpty() == false ||
			queuedBufferRelocationSources.Contains(sourceBuffer))
		{
			TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetBufferRelocationRequestKey(taskType, sourceBuffer));
			return;
		}

		CapsuleBuffer targetBuffer = FindClosestCapsuleBuffer(
			sourceBuffer.GridPosition,
			InteractionKind.Put,
			candidate =>
				candidate != null &&
				candidate.BufferState == targetState &&
				candidate.CanPutBox() &&
				queuedBufferRelocationTargets.Contains(candidate) == false);

		if (targetBuffer == null)
		{
			TaskManager?.CancelTaskBuildRequest(CapsuleRelocationTaskBuildRequest.GetBufferRelocationRequestKey(taskType, sourceBuffer));
			return;
		}

		TaskManager.EnqueueTaskBuildRequest(new CapsuleRelocationTaskBuildRequest(
			sourceBuffer,
			RuntimeBuildingId,
			taskType,
			reason,
			targetBuffer));
	}

	protected virtual void TryEvaluatePackingIngress(CapsuleBuffer capsuleBuffer)
	{
	}

	internal bool CanBuildOutboundTaskRequest(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null && capsuleBuffer.CanDispatchToOutbound() && IsBufferOutboundReady(capsuleBuffer);
	}

	internal virtual bool CanBuildWaterTaskRequest(CapsuleBuffer capsuleBuffer)
	{
		return false;
	}

	internal virtual bool CanBuildWaterTaskRequest(PackingStation packingStation)
	{
		return false;
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
				if (task.TargetDock is CapsuleBuffer targetBuffer)
					queuedInboundTargets[sourcePort] = targetBuffer;
				break;

			case WorkerTask.TaskType.CapsuleClear:
			case WorkerTask.TaskType.CapsuleSupply:
				if (task.SourceDock is CapsuleBuffer bufferSource)
					queuedBufferRelocationSources.Add(bufferSource);
				if (task.TargetDock is CapsuleBuffer bufferTarget)
					queuedBufferRelocationTargets.Add(bufferTarget);
				break;

			case WorkerTask.TaskType.OB:
				if (task.SourceDock is not CapsuleBuffer sourceBuffer)
					return;

				queuedOutboundBuffers.Add(sourceBuffer);
				if (task.TargetDock is OutboundCargoPort targetPort)
					queuedOutboundTargets[sourceBuffer] = targetPort;
				break;
		}
	}

	private CapsuleBuffer FindClosestCapsuleBuffer(
		in int3 from,
		InteractionKind interactionKind,
		System.Predicate<CapsuleBuffer> predicate)
	{
		if (GridService == null)
			return null;

		CapsuleBuffer bestCandidate = null;
		int bestScore = int.MaxValue;

		for (int i = 0; i < occupiedCapsuleBuffers.Count; ++i)
		{
			CapsuleBuffer candidate = occupiedCapsuleBuffers[i];
			if (candidate == null)
				continue;

			if (predicate != null && predicate(candidate) == false)
				continue;

			if (InteractionPointSelector.TryGetInteractionPoint(candidate, interactionKind, from, out _, out int score) == false)
				continue;

			if (score >= bestScore)
				continue;

			bestScore = score;
			bestCandidate = candidate;
		}

		return bestCandidate;
	}

	private OutboundCargoPort FindClosestOutboundPort(in int3 from, System.Predicate<OutboundCargoPort> predicate)
	{
		if (GridService == null)
			return null;

		OutboundCargoPort bestCandidate = null;
		int bestScore = int.MaxValue;

		for (int i = 0; i < occupiedCargoPorts.Count; ++i)
		{
			if (occupiedCargoPorts[i] is not OutboundCargoPort candidate)
				continue;

			if (predicate != null && predicate(candidate) == false)
				continue;

			if (InteractionPointSelector.TryGetInteractionPoint(candidate, InteractionKind.Put, from, out _, out int score) == false)
				continue;

			if (score >= bestScore)
				continue;

			bestScore = score;
			bestCandidate = candidate;
		}

		return bestCandidate;
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
}
