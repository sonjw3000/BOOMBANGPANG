using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class RobotNavigationService : MonoBehaviour
{
	private sealed class RobotAllocation
	{
		public int RegionId;
		public Dictionary<uint, int> Shares = new();
	}

	private sealed class TransitionRecord
	{
		public RobotWorker Robot;
		public int CoverageVersion;
		public int TargetRegionId;
		public Dictionary<uint, int> TargetShares;
		public Dictionary<uint, int> ReservedIncreases;
	}

	private static readonly uint[] EmptyHubIds = Array.Empty<uint>();

	private readonly List<NavigationHub> installedHubs = new();
	private readonly List<RelayNode> installedRelays = new();
	private readonly Dictionary<uint, NavigationHub> hubsById = new();
	private readonly Dictionary<uint, HashSet<int3>> coveredCellsByHub = new();
	private readonly List<uint[]> regionHubIds = new() { EmptyHubIds };
	private readonly Dictionary<RobotWorker, RobotAllocation> robotAllocations = new();
	private readonly Dictionary<uint, int> assignedComputeByHub = new();
	private readonly Dictionary<uint, int> reservedComputeByHub = new();
	private readonly Dictionary<int, TransitionRecord> transitions = new();
	private readonly HashSet<RobotWorker> waitingRobots = new();

	private FacilityManager facilityManager;
	private GridService gridService;
	private PowerService powerService;
	private WorkerManager workerManager;
	private uint nextHubId = 1;
	private int nextTransitionId = 1;
	private bool isBound;
	private bool isRebuilding;
	private bool rebuildRequested;

	public int CoverageVersion { get; private set; }
	public IReadOnlyList<NavigationHub> InstalledHubs => installedHubs;
	public IReadOnlyList<RelayNode> InstalledRelays => installedRelays;
	public int GetAssignedCompute(uint hubId) => GetCompute(assignedComputeByHub, hubId);
	public int GetReservedCompute(uint hubId) => GetCompute(reservedComputeByHub, hubId);

	public event Action<int> OnCoverageChanged;

	private void OnEnable()
	{
		if (GameContext.HasInstance == false)
			return;

		GameContext context = GameContext.Instance;
		Bind(context.FacilityMgr, context.GridService, context.PowerSvc, context.WorkerMgr);
	}

	public void Bind(FacilityManager facilities, GridService grid, PowerService power, WorkerManager workers)
	{
		if (isBound && facilityManager == facilities && gridService == grid && powerService == power && workerManager == workers)
			return;

		Unbind();
		facilityManager = facilities;
		gridService = grid;
		powerService = power;
		workerManager = workers;

		if (facilityManager != null)
		{
			facilityManager.SubscribeFacilityRegister<NavigationHub>(HandleHubRegistered, HandleHubUnregistered);
			facilityManager.SubscribeFacilityRegister<RelayNode>(HandleRelayRegistered, HandleRelayUnregistered);
		}

		if (powerService != null)
			powerService.OnPowerNetworkChanged += HandlePowerNetworkChanged;

		if (workerManager != null)
		{
			workerManager.OnWorkerRegistered += HandleWorkerRegistered;
			workerManager.OnWorkerUnregistered += HandleWorkerUnregistered;
			workerManager.OnWorkerOperationalStateChanged += HandleWorkerOperationalStateChanged;
		}

		isBound = true;
		RebuildRegisteredFacilities();
		RebuildRuntimeState();
	}

	public void Unbind()
	{
		if (isBound == false)
			return;

		if (facilityManager != null)
		{
			facilityManager.UnsubscribeFacilityRegister<NavigationHub>(HandleHubRegistered, HandleHubUnregistered);
			facilityManager.UnsubscribeFacilityRegister<RelayNode>(HandleRelayRegistered, HandleRelayUnregistered);
		}

		if (powerService != null)
			powerService.OnPowerNetworkChanged -= HandlePowerNetworkChanged;

		if (workerManager != null)
		{
			workerManager.OnWorkerRegistered -= HandleWorkerRegistered;
			workerManager.OnWorkerUnregistered -= HandleWorkerUnregistered;
			workerManager.OnWorkerOperationalStateChanged -= HandleWorkerOperationalStateChanged;
		}

		isBound = false;
	}

	private void OnDisable()
	{
		Unbind();
	}

	public void ResetRuntimeState()
	{
		ClearGridRegionCache();

		for (int i = 0; i < installedRelays.Count; ++i)
			installedRelays[i]?.SetConnected(false);

		for (int i = 0; i < installedHubs.Count; ++i)
		{
			NavigationHub hub = installedHubs[i];
			if (hub == null)
				continue;

			hub.SetActiveRelayCount(0);
			hub.SetRuntimeHubId(0);
		}

		installedHubs.Clear();
		installedRelays.Clear();
		hubsById.Clear();
		coveredCellsByHub.Clear();
		regionHubIds.Clear();
		regionHubIds.Add(EmptyHubIds);
		CancelAllTransitions();
		robotAllocations.Clear();
		assignedComputeByHub.Clear();
		reservedComputeByHub.Clear();
		waitingRobots.Clear();
		RefreshHubComputeUsage();
		nextHubId = 1;
		nextTransitionId = 1;
		IncrementCoverageVersion();
	}

	public void RebuildRuntimeState()
	{
		if (isRebuilding)
		{
			rebuildRequested = true;
			return;
		}

		isRebuilding = true;
		try
		{
			do
			{
				rebuildRequested = false;
				RebuildCoverage();
			}
			while (rebuildRequested);
		}
		finally
		{
			isRebuilding = false;
		}
	}

	public bool TryGetHub(uint hubId, out NavigationHub hub)
	{
		hub = null;
		return hubId != 0 && hubsById.TryGetValue(hubId, out hub) && hub != null;
	}

	public bool IsCellCovered(in int3 position)
	{
		return gridService?.GetCell(position)?.NavigationRegionId > 0;
	}

	public bool IsCellCoveredByHub(uint hubId, in int3 position)
	{
		return hubId != 0 &&
			coveredCellsByHub.TryGetValue(hubId, out HashSet<int3> cells) &&
			cells.Contains(position);
	}

	public IReadOnlyList<uint> GetRegionHubIds(int navigationRegionId)
	{
		if (navigationRegionId <= 0 || navigationRegionId >= regionHubIds.Count)
			return EmptyHubIds;

		return regionHubIds[navigationRegionId];
	}

	public int GetNavigationRegionId(in int3 position)
	{
		return gridService?.GetCell(position)?.NavigationRegionId ?? 0;
	}

	public bool CanRobotTraverseCell(RobotWorker robot, in int3 position)
	{
		return robot == null || robot.IsPlayerOverride || robot.RequiresNavigationCoverage == false || IsCellCovered(position);
	}

	public bool CanRunAutomatic(RobotWorker robot, out RobotNavigationWaitReason reason)
	{
		reason = RobotNavigationWaitReason.None;
		if (robot == null || robot.IsPlayerOverride || robot.NavigationDependency == RobotNavigationDependency.FullyAutonomous)
			return true;

		int regionId = GetNavigationRegionId(robot.GridPosition);
		if (robot.RequiresNavigationCoverage && regionId == 0)
		{
			if (robotAllocations.ContainsKey(robot))
			{
				ReleaseAllocation(robot);
				RefreshHubComputeUsage();
			}
			reason = RobotNavigationWaitReason.Coverage;
			return false;
		}

		if (robotAllocations.TryGetValue(robot, out RobotAllocation currentAllocation) &&
			currentAllocation.RegionId == regionId &&
			robot.NavigationCoverageVersion == CoverageVersion)
		{
			return true;
		}

		return TryAcquireCurrentAllocation(robot, out reason);
	}

	public bool CanAcceptNewAutomaticTask(RobotWorker robot, out RobotNavigationWaitReason reason)
	{
		if (CanRunAutomatic(robot, out reason) == false)
			return false;

		if (robot == null || robot.RequiresOrchestrationCompute == false ||
			robotAllocations.TryGetValue(robot, out RobotAllocation allocation) == false)
		{
			return true;
		}

		foreach (uint hubId in allocation.Shares.Keys)
		{
			if (TryGetHub(hubId, out NavigationHub hub) && hub.IsComputeOverloaded)
			{
				reason = RobotNavigationWaitReason.OrchestrationCapacity;
				return false;
			}
		}

		return true;
	}

	public bool TryGetRobotComputeShares(RobotWorker robot, out IReadOnlyDictionary<uint, int> shares)
	{
		if (robot != null && robotAllocations.TryGetValue(robot, out RobotAllocation allocation))
		{
			shares = allocation.Shares;
			return true;
		}

		shares = null;
		return false;
	}

	public void RegisterWaitingRobot(RobotWorker robot)
	{
		if (robot != null)
			waitingRobots.Add(robot);
	}

	public void UnregisterWaitingRobot(RobotWorker robot)
	{
		if (robot != null)
			waitingRobots.Remove(robot);
	}

	public void ReconcileManualMovement(RobotWorker robot, in int3 position)
	{
		if (robot == null)
			return;

		int regionId = GetNavigationRegionId(position);
		if (robot.NavigationCoverageVersion == CoverageVersion && robot.NavigationRegionId == regionId)
			return;

		ReleaseAllocation(robot);
		robot.SetNavigationCache(regionId, CoverageVersion);
		RefreshHubComputeUsage();
		RefreshWaitingRobotStates(robot);
	}

	public bool ReconcileExternalRelocation(
		RobotWorker robot,
		in int3 position,
		out RobotNavigationWaitReason reason)
	{
		reason = RobotNavigationWaitReason.None;
		if (robot == null)
			return true;

		if (robot.IsPlayerOverride)
		{
			ReconcileManualMovement(robot, position);
			return true;
		}

		int regionId = GetNavigationRegionId(position);
		if (robot.NavigationDependency == RobotNavigationDependency.FullyAutonomous)
		{
			ReleaseAllocation(robot);
			robot.SetNavigationCache(regionId, CoverageVersion);
			RefreshHubComputeUsage();
			return true;
		}

		if (robot.RequiresNavigationCoverage && regionId == 0)
		{
			ReleaseAllocation(robot);
			RefreshHubComputeUsage();
			RefreshWaitingRobotStates(robot);
			reason = RobotNavigationWaitReason.Coverage;
			return false;
		}

		if (TryAcquireCurrentAllocation(robot, out reason))
		{
			RefreshWaitingRobotStates(robot);
			return true;
		}

		ReleaseAllocation(robot);
		RefreshHubComputeUsage();
		RefreshWaitingRobotStates(robot);
		return false;
	}

	public bool CanBeginAutomaticRoute(RobotWorker robot, in int3 goalPosition, out RobotNavigationWaitReason reason)
	{
		if (CanRunAutomatic(robot, out reason) == false)
			return false;

		if (robot != null && robot.IsPlayerOverride == false && robot.RequiresNavigationCoverage && IsCellCovered(goalPosition) == false)
		{
			reason = RobotNavigationWaitReason.Coverage;
			return false;
		}

		return true;
	}

	public bool TryReserveTransition(
		RobotWorker robot,
		in int3 targetPosition,
		out NavigationTransitionReservation reservation,
		out RobotNavigationWaitReason reason)
	{
		reservation = default;
		reason = RobotNavigationWaitReason.None;
		if (robot == null || robot.IsPlayerOverride || robot.NavigationDependency == RobotNavigationDependency.FullyAutonomous)
			return true;

		int targetRegionId = GetNavigationRegionId(targetPosition);
		if (robot.RequiresNavigationCoverage && targetRegionId == 0)
		{
			reason = RobotNavigationWaitReason.Coverage;
			return false;
		}

		if (targetRegionId == robot.NavigationRegionId)
			return true;

		if (CanRunAutomatic(robot, out reason) == false)
			return false;

		Dictionary<uint, int> targetShares = BuildShares(robot, targetRegionId);
		RobotAllocation current = robotAllocations[robot];
		Dictionary<uint, int> increases = BuildPositiveDelta(current.Shares, targetShares);
		if (CanReserveIncreases(increases) == false)
		{
			reason = RobotNavigationWaitReason.OrchestrationCapacity;
			return false;
		}

		foreach (KeyValuePair<uint, int> entry in increases)
			AddCompute(reservedComputeByHub, entry.Key, entry.Value);

		int transitionId = NextTransitionId();
		transitions[transitionId] = new TransitionRecord
		{
			Robot = robot,
			CoverageVersion = CoverageVersion,
			TargetRegionId = targetRegionId,
			TargetShares = targetShares,
			ReservedIncreases = increases,
		};
		reservation = new NavigationTransitionReservation(transitionId);
		RefreshHubComputeUsage();
		return true;
	}

	public bool ValidateTransition(
		in NavigationTransitionReservation reservation,
		out RobotNavigationWaitReason reason)
	{
		reason = RobotNavigationWaitReason.None;
		if (reservation.RequiresCommit == false)
			return true;

		if (transitions.TryGetValue(reservation.Id, out TransitionRecord record) == false || record.Robot == null)
		{
			reason = RobotNavigationWaitReason.Coverage;
			return false;
		}

		if (record.CoverageVersion != CoverageVersion)
		{
			reason = IsCellCovered(record.Robot.GridPosition)
				? RobotNavigationWaitReason.OrchestrationCapacity
				: RobotNavigationWaitReason.Coverage;
			return false;
		}

		return true;
	}

	public bool CommitTransition(in NavigationTransitionReservation reservation)
	{
		if (reservation.RequiresCommit == false)
			return true;

		if (transitions.TryGetValue(reservation.Id, out TransitionRecord record) == false)
			return false;

		transitions.Remove(reservation.Id);
		ReleaseReservedIncreases(record.ReservedIncreases);
		if (record.Robot == null || record.CoverageVersion != CoverageVersion)
		{
			RefreshHubComputeUsage();
			return false;
		}

		ReplaceAllocation(record.Robot, record.TargetRegionId, record.TargetShares);
		RefreshHubComputeUsage();
		RefreshWaitingRobotStates();
		return true;
	}

	public void CancelTransition(in NavigationTransitionReservation reservation)
	{
		if (reservation.RequiresCommit == false || transitions.TryGetValue(reservation.Id, out TransitionRecord record) == false)
			return;

		transitions.Remove(reservation.Id);
		ReleaseReservedIncreases(record.ReservedIncreases);
		RefreshHubComputeUsage();
		RefreshWaitingRobotStates(record.Robot);
	}

	private bool TryAcquireCurrentAllocation(RobotWorker robot, out RobotNavigationWaitReason reason)
	{
		reason = RobotNavigationWaitReason.None;
		if (robot == null)
			return false;

		int regionId = GetNavigationRegionId(robot.GridPosition);
		if (robot.RequiresNavigationCoverage && regionId == 0)
		{
			reason = RobotNavigationWaitReason.Coverage;
			return false;
		}

		Dictionary<uint, int> shares = BuildShares(robot, regionId);
		Dictionary<uint, int> currentShares = robotAllocations.TryGetValue(robot, out RobotAllocation current)
			? current.Shares
			: null;
		Dictionary<uint, int> increases = BuildPositiveDelta(currentShares, shares);
		if (CanReserveIncreases(increases) == false)
		{
			reason = RobotNavigationWaitReason.OrchestrationCapacity;
			return false;
		}

		ReplaceAllocation(robot, regionId, shares);
		RefreshHubComputeUsage();
		return true;
	}

	private Dictionary<uint, int> BuildShares(RobotWorker robot, int regionId)
	{
		Dictionary<uint, int> shares = new();
		if (robot == null || robot.RequiresOrchestrationCompute == false || robot.RequiredNavigationCompute <= 0)
			return shares;

		IReadOnlyList<uint> hubIds = GetRegionHubIds(regionId);
		if (hubIds.Count == 0)
			return shares;

		int baseShare = robot.RequiredNavigationCompute / hubIds.Count;
		int remainder = robot.RequiredNavigationCompute % hubIds.Count;
		for (int i = 0; i < hubIds.Count; ++i)
		{
			int share = baseShare + (i < remainder ? 1 : 0);
			if (share > 0)
				shares[hubIds[i]] = share;
		}

		return shares;
	}

	private static Dictionary<uint, int> BuildPositiveDelta(
		IReadOnlyDictionary<uint, int> current,
		IReadOnlyDictionary<uint, int> target)
	{
		Dictionary<uint, int> result = new();
		if (target == null)
			return result;

		foreach (KeyValuePair<uint, int> entry in target)
		{
			int currentValue = current != null && current.TryGetValue(entry.Key, out int value) ? value : 0;
			int increase = entry.Value - currentValue;
			if (increase > 0)
				result[entry.Key] = increase;
		}

		return result;
	}

	private bool CanReserveIncreases(IReadOnlyDictionary<uint, int> increases)
	{
		if (increases == null)
			return true;

		foreach (KeyValuePair<uint, int> entry in increases)
		{
			if (TryGetHub(entry.Key, out NavigationHub hub) == false || hub.IsOperational == false)
				return false;

			int assigned = GetCompute(assignedComputeByHub, entry.Key);
			int reserved = GetCompute(reservedComputeByHub, entry.Key);
			if ((long)assigned + reserved + entry.Value > hub.ComputeCapacity)
				return false;
		}

		return true;
	}

	private void ReplaceAllocation(RobotWorker robot, int regionId, Dictionary<uint, int> shares)
	{
		if (robotAllocations.TryGetValue(robot, out RobotAllocation allocation) == false)
		{
			allocation = new RobotAllocation();
			robotAllocations[robot] = allocation;
		}

		foreach (KeyValuePair<uint, int> entry in allocation.Shares)
			AddCompute(assignedComputeByHub, entry.Key, -entry.Value);

		allocation.RegionId = Mathf.Max(0, regionId);
		allocation.Shares = shares ?? new Dictionary<uint, int>();
		foreach (KeyValuePair<uint, int> entry in allocation.Shares)
			AddCompute(assignedComputeByHub, entry.Key, entry.Value);

		robot.SetNavigationCache(allocation.RegionId, CoverageVersion);
	}

	private void ReleaseAllocation(RobotWorker robot)
	{
		if (robot == null || robotAllocations.TryGetValue(robot, out RobotAllocation allocation) == false)
			return;

		foreach (KeyValuePair<uint, int> entry in allocation.Shares)
			AddCompute(assignedComputeByHub, entry.Key, -entry.Value);

		robotAllocations.Remove(robot);
		robot.SetNavigationCache(0, CoverageVersion);
	}

	private void ReconcileRobotAllocations()
	{
		CancelAllTransitions();
		List<RobotWorker> previouslyAllocated = new(robotAllocations.Keys);
		previouslyAllocated.RemoveAll(robot => robot == null || robot.IsOperational == false || IsRegistered(robot) == false);
		previouslyAllocated.Sort((a, b) => a.WorkerID.CompareTo(b.WorkerID));

		robotAllocations.Clear();
		assignedComputeByHub.Clear();
		reservedComputeByHub.Clear();

		for (int i = 0; i < previouslyAllocated.Count; ++i)
		{
			RobotWorker robot = previouslyAllocated[i];
			int regionId = GetNavigationRegionId(robot.GridPosition);
			if (robot.RequiresNavigationCoverage && regionId == 0)
			{
				robot.SetNavigationCache(0, CoverageVersion);
				continue;
			}

			// Topology loss may overload a surviving hub. Existing robots retain service;
			// only new positive reservations are rejected until the overload is cleared.
			ReplaceAllocation(robot, regionId, BuildShares(robot, regionId));
		}

		if (workerManager != null)
		{
			List<RobotWorker> unallocated = new();
			for (int i = 0; i < workerManager.Workers.Count; ++i)
			{
				if (workerManager.Workers[i] is RobotWorker robot &&
					robot.IsOperational &&
					robot.IsPlayerOverride == false &&
					robotAllocations.ContainsKey(robot) == false)
					unallocated.Add(robot);
			}

			unallocated.Sort((a, b) => a.WorkerID.CompareTo(b.WorkerID));
			for (int i = 0; i < unallocated.Count; ++i)
				TryAcquireCurrentAllocation(unallocated[i], out _);
		}

		RefreshHubComputeUsage();
		RefreshAllRobotStates();
	}

	private bool IsRegistered(RobotWorker robot)
	{
		if (robot == null || workerManager == null)
			return false;

		for (int i = 0; i < workerManager.Workers.Count; ++i)
		{
			if (workerManager.Workers[i] == robot)
				return true;
		}

		return false;
	}

	private void RefreshAllRobotStates()
	{
		if (workerManager == null)
			return;

		for (int i = 0; i < workerManager.Workers.Count; ++i)
		{
			if (workerManager.Workers[i] is not RobotWorker robot || robot.IsOperational == false || robot.IsPlayerOverride)
				continue;

			RobotNavigationWaitReason reason;
			bool canRun = robot.CurrentTask == null
				? CanAcceptNewAutomaticTask(robot, out reason)
				: CanRunAutomatic(robot, out reason);
			if (canRun)
				robot.EndNavigationWait();
			else
				robot.BeginNavigationWait(reason);
		}
	}

	private void RefreshWaitingRobotStates(RobotWorker excludedRobot = null)
	{
		if (waitingRobots.Count == 0)
			return;

		List<RobotWorker> snapshot = new(waitingRobots);
		snapshot.Sort((a, b) =>
		{
			if (a == null)
				return b == null ? 0 : 1;
			if (b == null)
				return -1;
			return a.WorkerID.CompareTo(b.WorkerID);
		});

		for (int i = 0; i < snapshot.Count; ++i)
		{
			RobotWorker robot = snapshot[i];
			if (robot == null)
			{
				waitingRobots.Remove(robot);
				continue;
			}

			if (robot == excludedRobot || robot.IsOperational == false || robot.IsPlayerOverride)
				continue;

			RobotNavigationWaitReason reason;
			bool canRun = robot.CurrentTask == null
				? CanAcceptNewAutomaticTask(robot, out reason)
				: CanRunAutomatic(robot, out reason);
			if (canRun)
				robot.EndNavigationWait();
			else
				robot.BeginNavigationWait(reason);
		}
	}

	private void CancelAllTransitions()
	{
		transitions.Clear();
		reservedComputeByHub.Clear();
	}

	private void ReleaseReservedIncreases(IReadOnlyDictionary<uint, int> increases)
	{
		if (increases == null)
			return;

		foreach (KeyValuePair<uint, int> entry in increases)
			AddCompute(reservedComputeByHub, entry.Key, -entry.Value);
	}

	private void RefreshHubComputeUsage()
	{
		for (int i = 0; i < installedHubs.Count; ++i)
		{
			NavigationHub hub = installedHubs[i];
			if (hub != null)
				hub.SetComputeUsage(GetCompute(assignedComputeByHub, hub.RuntimeHubId), GetCompute(reservedComputeByHub, hub.RuntimeHubId));
		}
	}

	private static int GetCompute(IReadOnlyDictionary<uint, int> source, uint hubId)
	{
		return source != null && source.TryGetValue(hubId, out int value) ? Mathf.Max(0, value) : 0;
	}

	private static void AddCompute(Dictionary<uint, int> source, uint hubId, int delta)
	{
		if (hubId == 0 || delta == 0)
			return;

		int next = GetCompute(source, hubId) + delta;
		if (next > 0)
			source[hubId] = next;
		else
			source.Remove(hubId);
	}

	private int NextTransitionId()
	{
		if (nextTransitionId <= 0 || nextTransitionId == int.MaxValue)
			nextTransitionId = 1;

		while (transitions.ContainsKey(nextTransitionId))
			++nextTransitionId;

		return nextTransitionId++;
	}

	public bool TryAssignRelay(RelayNode relay, NavigationHub hub)
	{
		if (relay == null || hub == null || installedRelays.Contains(relay) == false || installedHubs.Contains(hub) == false)
			return false;

		relay.SetOwnerHubId(hub.RuntimeHubId);
		RebuildRuntimeState();
		return true;
	}

	public bool CanInstallRelay(NavigationHub hub, in int3 position)
	{
		return hub != null && hub.IsOperational && IsCellCoveredByHub(hub.RuntimeHubId, position);
	}

	private void RebuildRegisteredFacilities()
	{
		installedHubs.Clear();
		installedRelays.Clear();
		hubsById.Clear();
		nextHubId = 1;

		if (facilityManager == null)
			return;

		IReadOnlyList<uint> buildingIds = facilityManager.GetBuildingIds();
		for (int buildingIndex = 0; buildingIndex < buildingIds.Count; ++buildingIndex)
		{
			uint buildingId = buildingIds[buildingIndex];
			IReadOnlyList<NavigationHub> hubs = facilityManager.GetFacilities<NavigationHub>(buildingId);
			for (int hubIndex = 0; hubIndex < hubs.Count; ++hubIndex)
				AddHub(hubs[hubIndex]);

			IReadOnlyList<RelayNode> relays = facilityManager.GetFacilities<RelayNode>(buildingId);
			for (int relayIndex = 0; relayIndex < relays.Count; ++relayIndex)
				AddUnique(installedRelays, relays[relayIndex]);
		}
	}

	private void RebuildCoverage()
	{
		ClearGridRegionCache();
		coveredCellsByHub.Clear();
		regionHubIds.Clear();
		regionHubIds.Add(EmptyHubIds);

		for (int relayIndex = 0; relayIndex < installedRelays.Count; ++relayIndex)
			installedRelays[relayIndex]?.SetConnected(false);

		List<NavigationHub> orderedHubs = new(installedHubs);
		orderedHubs.RemoveAll(hub => hub == null);
		orderedHubs.Sort((a, b) => a.RuntimeHubId.CompareTo(b.RuntimeHubId));

		for (int hubIndex = 0; hubIndex < orderedHubs.Count; ++hubIndex)
		{
			NavigationHub hub = orderedHubs[hubIndex];
			HashSet<int3> coveredCells = new();
			coveredCellsByHub[hub.RuntimeHubId] = coveredCells;
			if (hub.IsOperational == false)
			{
				hub.SetActiveRelayCount(0);
				continue;
			}

			AddCoverage(coveredCells, hub.GridPosition, hub.CoverageRadius);
			int connectedRelayCount = ConnectRelays(hub, coveredCells);
			hub.SetActiveRelayCount(connectedRelayCount);
		}

		BuildCellRegionCache(orderedHubs);
		for (int relayIndex = 0; relayIndex < installedRelays.Count; ++relayIndex)
		{
			if (TryAutoAssignRelay(installedRelays[relayIndex]))
				rebuildRequested = true;
		}
		IncrementCoverageVersion();
	}

	private int ConnectRelays(NavigationHub hub, HashSet<int3> coveredCells)
	{
		List<RelayNode> candidates = new();
		for (int relayIndex = 0; relayIndex < installedRelays.Count; ++relayIndex)
		{
			RelayNode relay = installedRelays[relayIndex];
			if (relay != null && relay.OwnerHubId == hub.RuntimeHubId && relay.IsOperational)
				candidates.Add(relay);
		}

		candidates.Sort((a, b) => CompareRelayOrder(hub, a, b));
		int connectedCount = 0;
		bool expanded;
		do
		{
			expanded = false;
			for (int relayIndex = 0; relayIndex < candidates.Count; ++relayIndex)
			{
				RelayNode relay = candidates[relayIndex];
				if (relay.IsConnected || connectedCount >= hub.RelayCapacity)
					continue;

				if (coveredCells.Contains(relay.GridPosition) == false)
					continue;

				relay.SetConnected(true);
				++connectedCount;
				AddCoverage(coveredCells, relay.GridPosition, relay.CoverageRadius);
				expanded = true;
			}
		}
		while (expanded && connectedCount < hub.RelayCapacity);

		return connectedCount;
	}

	private void BuildCellRegionCache(IReadOnlyList<NavigationHub> orderedHubs)
	{
		Dictionary<int3, List<uint>> influencesByCell = new();
		for (int hubIndex = 0; hubIndex < orderedHubs.Count; ++hubIndex)
		{
			NavigationHub hub = orderedHubs[hubIndex];
			if (coveredCellsByHub.TryGetValue(hub.RuntimeHubId, out HashSet<int3> cells) == false)
				continue;

			foreach (int3 cellPosition in cells)
			{
				if (influencesByCell.TryGetValue(cellPosition, out List<uint> hubIds) == false)
				{
					hubIds = new List<uint>();
					influencesByCell[cellPosition] = hubIds;
				}

				hubIds.Add(hub.RuntimeHubId);
			}
		}

		foreach (KeyValuePair<int3, List<uint>> entry in influencesByCell)
		{
			int regionId = GetOrCreateRegion(entry.Value);
			gridService?.GetCell(entry.Key)?.SetNavigationRegionId(regionId);
		}
	}

	private int GetOrCreateRegion(List<uint> hubIds)
	{
		for (int regionId = 1; regionId < regionHubIds.Count; ++regionId)
		{
			uint[] candidate = regionHubIds[regionId];
			if (candidate.Length != hubIds.Count)
				continue;

			bool equals = true;
			for (int hubIndex = 0; hubIndex < candidate.Length; ++hubIndex)
			{
				if (candidate[hubIndex] == hubIds[hubIndex])
					continue;

				equals = false;
				break;
			}

			if (equals)
				return regionId;
		}

		uint[] region = hubIds.ToArray();
		regionHubIds.Add(region);
		return regionHubIds.Count - 1;
	}

	private void AddCoverage(HashSet<int3> coveredCells, in int3 center, int radius)
	{
		if (coveredCells == null || gridService == null)
			return;

		int clampedRadius = Mathf.Max(0, radius);
		for (int y = -clampedRadius; y <= clampedRadius; ++y)
		{
			int yRemainder = clampedRadius - math.abs(y);
			for (int x = -yRemainder; x <= yRemainder; ++x)
			{
				int zRemainder = yRemainder - math.abs(x);
				for (int z = -zRemainder; z <= zRemainder; ++z)
				{
					int3 position = center + new int3(x, y, z);
					if (gridService.GetCell(position) != null)
						coveredCells.Add(position);
				}
			}
		}
	}

	private void ClearGridRegionCache()
	{
		if (gridService?.Map == null)
			return;

		int3 size = gridService.MapSize;
		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
					gridService.Map[x, y, z]?.SetNavigationRegionId(0);
			}
		}
	}

	private void IncrementCoverageVersion()
	{
		CoverageVersion = CoverageVersion == int.MaxValue ? 1 : CoverageVersion + 1;
		ReconcileRobotAllocations();
		OnCoverageChanged?.Invoke(CoverageVersion);
	}

	private void HandleWorkerRegistered(AIWorker worker)
	{
		if (worker is not RobotWorker robot)
			return;
		if (robot.IsOperational == false)
		{
			robot.SetNavigationCache(0, CoverageVersion);
			return;
		}

		if (CanAcceptNewAutomaticTask(robot, out RobotNavigationWaitReason reason))
			robot.EndNavigationWait();
		else
			robot.BeginNavigationWait(reason);
	}

	private void HandleWorkerUnregistered(AIWorker worker)
	{
		if (worker is not RobotWorker robot)
			return;

		CancelTransitionsFor(robot);
		waitingRobots.Remove(robot);
		ReleaseAllocation(robot);
		RefreshHubComputeUsage();
		RefreshWaitingRobotStates();
	}

	private void HandleWorkerOperationalStateChanged(
		AIWorker worker,
		WorkerOperationalState previousState,
		WorkerOperationalState nextState)
	{
		if (worker is not RobotWorker robot || previousState == nextState)
			return;

		if (nextState != WorkerOperationalState.Active)
		{
			CancelTransitionsFor(robot);
			waitingRobots.Remove(robot);
			ReleaseAllocation(robot);
			robot.EndNavigationWait();
			RefreshHubComputeUsage();
			RefreshWaitingRobotStates();
			return;
		}

		if (CanAcceptNewAutomaticTask(robot, out RobotNavigationWaitReason reason))
			robot.EndNavigationWait();
		else
			robot.BeginNavigationWait(reason);
		RefreshWaitingRobotStates(robot);
	}

	private void CancelTransitionsFor(RobotWorker robot)
	{
		if (robot == null || transitions.Count == 0)
			return;

		List<int> ids = new();
		foreach (KeyValuePair<int, TransitionRecord> entry in transitions)
		{
			if (entry.Value.Robot == robot)
				ids.Add(entry.Key);
		}

		for (int i = 0; i < ids.Count; ++i)
		{
			TransitionRecord record = transitions[ids[i]];
			transitions.Remove(ids[i]);
			ReleaseReservedIncreases(record.ReservedIncreases);
		}
	}

	private void HandleHubRegistered(uint buildingId, IFacility facility)
	{
		if (facility is not NavigationHub hub)
			return;

		AddHub(hub);
		RebuildRuntimeState();
	}

	private void HandleHubUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is not NavigationHub hub)
			return;

		installedHubs.Remove(hub);
		hubsById.Remove(hub.RuntimeHubId);
		hub.SetActiveRelayCount(0);
		hub.SetComputeUsage(0, 0);
		hub.SetRuntimeHubId(0);
		RebuildRuntimeState();
	}

	private void HandleRelayRegistered(uint buildingId, IFacility facility)
	{
		if (facility is not RelayNode relay)
			return;

		AddUnique(installedRelays, relay);
		TryAutoAssignRelay(relay);
		RebuildRuntimeState();
	}

	private void HandleRelayUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is not RelayNode relay)
			return;

		installedRelays.Remove(relay);
		relay.SetConnected(false);
		RebuildRuntimeState();
	}

	private void HandlePowerNetworkChanged()
	{
		RebuildRuntimeState();
	}

	private void AddHub(NavigationHub hub)
	{
		if (hub == null || installedHubs.Contains(hub))
			return;

		if (hub.RuntimeHubId == 0 || hubsById.ContainsKey(hub.RuntimeHubId))
			hub.SetRuntimeHubId(nextHubId++);
		else
			nextHubId = math.max(nextHubId, hub.RuntimeHubId + 1);

		installedHubs.Add(hub);
		hubsById[hub.RuntimeHubId] = hub;
	}

	private bool TryAutoAssignRelay(RelayNode relay)
	{
		if (relay == null || relay.OwnerHubId != 0)
			return false;

		NavigationHub candidate = null;
		for (int hubIndex = 0; hubIndex < installedHubs.Count; ++hubIndex)
		{
			NavigationHub hub = installedHubs[hubIndex];
			if (hub == null || IsCellCoveredByHub(hub.RuntimeHubId, relay.GridPosition) == false)
				continue;

			if (candidate != null)
				return false;

			candidate = hub;
		}

		if (candidate != null)
		{
			relay.SetOwnerHubId(candidate.RuntimeHubId);
			return true;
		}

		return false;
	}

	private static int CompareRelayOrder(NavigationHub hub, RelayNode a, RelayNode b)
	{
		int aDistance = ManhattanDistance(hub.GridPosition, a.GridPosition);
		int bDistance = ManhattanDistance(hub.GridPosition, b.GridPosition);
		int comparison = aDistance.CompareTo(bDistance);
		if (comparison != 0)
			return comparison;

		comparison = a.GridPosition.x.CompareTo(b.GridPosition.x);
		if (comparison != 0)
			return comparison;

		comparison = a.GridPosition.y.CompareTo(b.GridPosition.y);
		return comparison != 0 ? comparison : a.GridPosition.z.CompareTo(b.GridPosition.z);
	}

	private static int ManhattanDistance(in int3 a, in int3 b)
	{
		int3 delta = math.abs(a - b);
		return delta.x + delta.y + delta.z;
	}

	private static void AddUnique<T>(List<T> list, T value) where T : class
	{
		if (value != null && list.Contains(value) == false)
			list.Add(value);
	}
}
