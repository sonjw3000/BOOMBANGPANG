using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class RobotNavigationService : MonoBehaviour
{
	private static readonly uint[] EmptyHubIds = Array.Empty<uint>();

	private readonly List<NavigationHub> installedHubs = new();
	private readonly List<RelayNode> installedRelays = new();
	private readonly Dictionary<uint, NavigationHub> hubsById = new();
	private readonly Dictionary<uint, HashSet<int3>> coveredCellsByHub = new();
	private readonly List<uint[]> regionHubIds = new() { EmptyHubIds };

	private FacilityManager facilityManager;
	private GridService gridService;
	private PowerService powerService;
	private uint nextHubId = 1;
	private bool isBound;
	private bool isRebuilding;
	private bool rebuildRequested;

	public int CoverageVersion { get; private set; }
	public IReadOnlyList<NavigationHub> InstalledHubs => installedHubs;
	public IReadOnlyList<RelayNode> InstalledRelays => installedRelays;

	public event Action<int> OnCoverageChanged;

	private void OnEnable()
	{
		if (GameContext.HasInstance == false)
			return;

		GameContext context = GameContext.Instance;
		Bind(context.FacilityMgr, context.GridService, context.PowerSvc);
	}

	public void Bind(FacilityManager facilities, GridService grid, PowerService power)
	{
		if (isBound && facilityManager == facilities && gridService == grid && powerService == power)
			return;

		Unbind();
		facilityManager = facilities;
		gridService = grid;
		powerService = power;

		if (facilityManager != null)
		{
			facilityManager.SubscribeFacilityRegister<NavigationHub>(HandleHubRegistered, HandleHubUnregistered);
			facilityManager.SubscribeFacilityRegister<RelayNode>(HandleRelayRegistered, HandleRelayUnregistered);
		}

		if (powerService != null)
			powerService.OnPowerNetworkChanged += HandlePowerNetworkChanged;

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
		nextHubId = 1;
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
		OnCoverageChanged?.Invoke(CoverageVersion);
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
