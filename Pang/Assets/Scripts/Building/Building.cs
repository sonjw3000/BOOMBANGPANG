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
	private bool isRegistered;

	private readonly List<GridCell> occupiedCells;

	private readonly List<IFacility> occupiedFacilities = new();
	private readonly List<CargoPort> occupiedCargoPorts = new();
	private readonly List<CapsuleBuffer> occupiedCapsuleBuffers = new();
	private readonly HashSet<uint> inputBuildingIds = new();
	private readonly HashSet<uint> outputBuildingIds = new();
	private readonly HashSet<InboundCargoPort> pendingInboundPorts = new();
	private readonly HashSet<InboundCargoPort> queuedInboundPorts = new();
	private readonly HashSet<CapsuleBuffer> pendingOutboundBuffers = new();
	private readonly HashSet<CapsuleBuffer> queuedOutboundBuffers = new();
	private readonly Dictionary<InboundCargoPort, CapsuleBuffer> queuedInboundTargets = new();
	private readonly Dictionary<CapsuleBuffer, OutboundCargoPort> queuedOutboundTargets = new();
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

	private TaskManager TaskManager => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private ZoneManager ZoneManager => GameContext.HasInstance ? GameContext.Instance.ZoneMgr : null;
	public Building(string displayName, List<GridCell> occupiedCells, BuildingType buildingType = BuildingType.Generic)
	{
		this.displayName = displayName;
		this.buildingType = buildingType;
		this.occupiedCells = occupiedCells ?? new List<GridCell>();
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
			UnsubscribeCargoPort(cargoPort);
			occupiedCargoPorts.Remove(cargoPort);
		}
		else if (facility is CapsuleBuffer capsuleBuffer)
		{
			UnsubscribeCapsuleBuffer(capsuleBuffer);
			occupiedCapsuleBuffers.Remove(capsuleBuffer);
			pendingOutboundBuffers.Remove(capsuleBuffer);
			queuedOutboundBuffers.Remove(capsuleBuffer);
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
				OnInboundCargoDocked(inboundCargoPort);
			else
				OnInboundCargoUndocked(inboundCargoPort);

			return;
		}

		if (cargoPort is OutboundCargoPort outboundCargoPort)
		{
			if (outboundCargoPort.CanPutBox())
				OnOutboundCargoUndocked(outboundCargoPort);
			else
				OnOutboundCargoDocked(outboundCargoPort);
		}
	}

	private void UnsubscribeCargoPort(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		cargoPort.OnCargoDocked -= HandleCargoDocked;
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
		capsuleBuffer.OnCapsuleUndocked -= HandleCapsuleBufferUndocked;
		capsuleBuffer.OnCapsuleContentChanged -= HandleCapsuleBufferContentChanged;
		capsuleBuffer.OnBufferStateChanged -= HandleCapsuleBufferStateChanged;
	}

	private void HandleCargoDocked(CargoPort cargoPort)
	{
		if (cargoPort is InboundCargoPort inboundCargoPort)
			OnInboundCargoDocked(inboundCargoPort);
		else if (cargoPort is OutboundCargoPort outboundCargoPort)
			OnOutboundCargoDocked(outboundCargoPort);
	}

	private void HandleCargoUndocked(CargoPort cargoPort)
	{
		if (cargoPort is InboundCargoPort inboundCargoPort)
			OnInboundCargoUndocked(inboundCargoPort);
		else if (cargoPort is OutboundCargoPort outboundCargoPort)
			OnOutboundCargoUndocked(outboundCargoPort);
	}

	private void HandleCargoQuantityZero(CargoPort cargoPort)
	{
		if (cargoPort is InboundCargoPort inboundCargoPort)
			OnInboundCargoQuantityZero(inboundCargoPort);
	}

	private void HandleCargoQuantityOverPercent(CargoPort cargoPort)
	{
		if (cargoPort is OutboundCargoPort outboundCargoPort)
			OnOutboundCargoQuantityOverPercent(outboundCargoPort);
	}

	private void HandleCapsuleBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		OnCapsuleBufferDocked(capsuleBuffer);
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

	protected virtual void OnInboundCargoDocked(InboundCargoPort cargoPort)
	{
		if (cargoPort == null || cargoPort.IsCapsuleEmpty())
			return;

		pendingInboundPorts.Add(cargoPort);
		TryEnqueueInboundTask(cargoPort);
	}

	protected virtual void OnInboundCargoUndocked(InboundCargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		pendingInboundPorts.Remove(cargoPort);
		queuedInboundPorts.Remove(cargoPort);
	}

	protected virtual void OnInboundCargoQuantityZero(InboundCargoPort cargoPort)
	{
		if (cargoPort == null)
			return;

		pendingInboundPorts.Remove(cargoPort);
		queuedInboundPorts.Remove(cargoPort);
		queuedInboundTargets.Remove(cargoPort);
	}

	protected virtual void OnOutboundCargoUndocked(OutboundCargoPort cargoPort)
	{
		RemoveQueuedOutboundTarget(cargoPort);
		TryEvaluatePendingOutboundBuffers();
	}

	protected virtual void OnOutboundCargoDocked(OutboundCargoPort cargoPort)
	{
		RemoveQueuedOutboundTarget(cargoPort);
	}

	protected virtual void OnOutboundCargoQuantityOverPercent(OutboundCargoPort cargoPort)
	{
	}

	protected virtual void OnCapsuleBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		RemoveQueuedInboundTarget(capsuleBuffer);
		TryEvaluateOutbound(capsuleBuffer);
	}

	protected virtual void OnCapsuleBufferUndocked(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		pendingOutboundBuffers.Remove(capsuleBuffer);
		queuedOutboundBuffers.Remove(capsuleBuffer);
		TryEnqueuePendingInboundTasks();
	}

	protected virtual void OnCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		TryEvaluateOutbound(capsuleBuffer);
	}

	protected virtual void OnCapsuleBufferStateChanged(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null)
			return;

		if (capsuleBuffer.CanReceiveFromInbound())
			TryEnqueuePendingInboundTasks();

		TryEvaluateOutbound(capsuleBuffer);
	}

	protected virtual bool CanDispatchBufferToOutbound(CapsuleBuffer capsuleBuffer)
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

				if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(candidate, InteractionKind.Put, from, GridService, out _, out int score) == false)
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

		int3 sourcePoint = ResolveInteractionOrigin(cargoPort, InteractionKind.Pick);
		CapsuleBuffer targetBuffer = ResolveInboundBufferTarget(sourcePoint);
		if (targetBuffer == null)
			return;

		TaskManager.EnqueueTask(new IBTask(cargoPort, RuntimeBuildingId, targetBuffer));
		queuedInboundPorts.Add(cargoPort);
		queuedInboundTargets[cargoPort] = targetBuffer;
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

		if (capsuleBuffer.CanDispatchToOutbound() == false || queuedOutboundBuffers.Contains(capsuleBuffer))
		{
			pendingOutboundBuffers.Remove(capsuleBuffer);
			return;
		}

		if (CanDispatchBufferToOutbound(capsuleBuffer) == false)
		{
			pendingOutboundBuffers.Remove(capsuleBuffer);
			return;
		}

		if (TaskManager == null)
			return;

		OutboundCargoPort targetPort = ResolveOutboundPortTarget(capsuleBuffer.GridPosition);
		if (targetPort == null)
		{
			pendingOutboundBuffers.Add(capsuleBuffer);
			return;
		}

		TaskManager.EnqueueTask(new OBTask(capsuleBuffer, RuntimeBuildingId, targetPort));
		queuedOutboundBuffers.Add(capsuleBuffer);
		queuedOutboundTargets[capsuleBuffer] = targetPort;
		pendingOutboundBuffers.Remove(capsuleBuffer);
	}

	private void TryEvaluatePendingOutboundBuffers()
	{
		if (pendingOutboundBuffers.Count <= 0)
			return;

		CapsuleBuffer[] buffers = new CapsuleBuffer[pendingOutboundBuffers.Count];
		pendingOutboundBuffers.CopyTo(buffers);
		for (int i = 0; i < buffers.Length; ++i)
			TryEvaluateOutbound(buffers[i]);
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

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(candidate, interactionKind, from, GridService, out _, out int score) == false)
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

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(candidate, InteractionKind.Put, from, GridService, out _, out int score) == false)
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
