using System.Collections.Generic;
using UnityEngine;

public partial class PackingStationService : FacilityService<PackingStation>
{
	private sealed class BuildingPackingState
	{
		public readonly List<PackingStation> Stations = new();
		public readonly LinkedList<PackingStation> WaitingQueue = new();
		public readonly HashSet<PackingStation> WaitingSet = new();
		public readonly HashSet<PackingStation> QueuedPackingTasks = new();
	}

	private static CapsuleBufferService CapsuleBufferService => GameContext.Instance.CapsuleBufferSvc;
	private static TaskManager TaskManager => GameContext.Instance.TaskMgr;

	private readonly Dictionary<uint, BuildingPackingState> statesByBuildingId = new();

	protected override void OnRegisterFacility(uint buildingId, PackingStation facility)
	{
		BuildingPackingState state = GetOrCreateState(buildingId);
		if (state.Stations.Contains(facility))
			return;

		state.Stations.Add(facility);
		RefreshWaitingStation(facility);
	}

	protected override void OnUnregisterFacility(uint buildingId, PackingStation facility)
	{
		if (facility == null || statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return;

		state.Stations.Remove(facility);
		RemoveWaitingStation(state, facility);
		state.QueuedPackingTasks.Remove(facility);

		if (state.Stations.Count <= 0 &&
			state.WaitingQueue.Count <= 0 &&
			state.QueuedPackingTasks.Count <= 0)
		{
			statesByBuildingId.Remove(buildingId);
		}
	}

	public bool TryReserveWaitingStation(AIWorker picker, out PackingStation station)
	{
		station = null;
		if (picker == null || TryGetBuildingId(picker.GridPosition, out uint buildingId) == false)
			return false;

		return TryReserveWaitingStation(buildingId, picker, out station);
	}

	public bool TryReserveWaitingStation(uint buildingId, AIWorker picker, out PackingStation station)
	{
		station = null;
		if (picker == null || statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return false;

		if (TryGetReservedStationForPicker(state, picker, out station))
			return true;

		if (TryReserveWaitingStation(state, picker, candidate => candidate.CurrentPackingWorker != null, out station))
			return true;

		return TryReserveWaitingStation(state, picker, candidate => candidate.CurrentPackingWorker == null, out station);
	}

	public void RefreshWaitingStation(PackingStation station)
	{
		if (station == null || TryGetState(station, out BuildingPackingState state) == false)
			return;

		if (station.CanRequestIncomingBox())
			EnqueueWaitingStation(state, station);
		else
			RemoveWaitingStation(state, station);
	}

	public void RequestPackingTaskIfNeeded(PackingStation packingStation)
	{
		if (packingStation == null || packingStation.HasWaitingBox == false || TryGetState(packingStation, out BuildingPackingState state) == false)
			return;

		if (state.QueuedPackingTasks.Contains(packingStation))
			return;

		if (packingStation.CurrentPackingWorker?.CurrentTask is PackingTask currentTask &&
			currentTask.TargetStation == packingStation)
		{
			return;
		}

		state.QueuedPackingTasks.Add(packingStation);
		TaskManager.EnqueueTask(new PackingTask(packingStation));
	}

	public void OnPackingTaskAssigned(PackingStation packingStation)
	{
		if (packingStation == null || TryGetState(packingStation, out BuildingPackingState state) == false)
			return;

		state.QueuedPackingTasks.Remove(packingStation);
	}

	public void OnPackingTaskCompleted(PackingStation packingStation)
	{
		if (packingStation == null)
			return;

		if (packingStation.HasWaitingBox)
			RequestPackingTaskIfNeeded(packingStation);
		else
			RefreshWaitingStation(packingStation);
	}

	public void OnPackingComplete(PackingStation packingStation)
	{
		if (packingStation == null || TryGetBuildingId(packingStation, out uint buildingId) == false)
			return;

		if (GameContext.Instance.BuildingMgr.TryGetBuilding(buildingId, out Building building) &&
			building is PackingBuilding packingBuilding)
		{
			packingBuilding.MarkPackingOutputDirty(packingStation);
		}
	}

	public void GetPendingPackingDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;

		foreach (var entry in statesByBuildingId)
			AccumulatePendingPackingDemand(entry.Value, ref sourceCount, ref itemQuantity);
	}

	public void GetPendingPackingDemand(uint buildingId, out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return;

		AccumulatePendingPackingDemand(state, ref sourceCount, ref itemQuantity);
	}

	private static void AccumulatePendingPackingDemand(
		BuildingPackingState state,
		ref int sourceCount,
		ref int itemQuantity)
	{
		List<PackingStation> stations = state.Stations;
		for (int i = 0; i < stations.Count; ++i)
		{
			BoxBase box = stations[i]?.WaitingBox?.Box;
			if (box == null)
				continue;

			++sourceCount;
			foreach (var itemTotal in box.ItemTotals)
				itemQuantity += Mathf.Max(0, itemTotal.Value);
		}
	}

	public bool TryClaimWaitingStation(uint buildingId, out PackingStation station)
	{
		station = null;
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return false;

		if (TryClaimWaitingStation(state, candidate => candidate.CurrentPackingWorker != null, out station))
			return true;

		return TryClaimWaitingStation(state, candidate => candidate.CurrentPackingWorker == null, out station);
	}

	public bool TryClaimWaitingStation(uint buildingId, FacilityFilter facilityFilter, out PackingStation station)
	{
		station = null;
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return false;

		bool MatchesRule(PackingStation candidate) =>
			candidate != null && facilityFilter.MatchesCurrentRules(candidate);

		if (TryClaimWaitingStation(state, candidate => MatchesRule(candidate) && candidate.CurrentPackingWorker != null, out station))
			return true;

		return TryClaimWaitingStation(state, candidate => MatchesRule(candidate) && candidate.CurrentPackingWorker == null, out station);
	}

	private BuildingPackingState GetOrCreateState(uint buildingId)
	{
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
		{
			state = new BuildingPackingState();
			statesByBuildingId[buildingId] = state;
		}

		return state;
	}

	private bool TryGetState(PackingStation station, out BuildingPackingState state)
	{
		state = null;
		return TryGetBuildingId(station, out uint buildingId) && statesByBuildingId.TryGetValue(buildingId, out state);
	}

	private static bool TryGetReservedStationForPicker(BuildingPackingState state, AIWorker picker, out PackingStation station)
	{
		station = null;
		if (picker == null || state == null)
			return false;

		for (int i = 0; i < state.Stations.Count; ++i)
		{
			PackingStation candidate = state.Stations[i];
			if (candidate == null || candidate.IncomingPickingWorker != picker)
				continue;

			station = candidate;
			return true;
		}

		return false;
	}

	private bool TryReserveWaitingStation(
		BuildingPackingState state,
		AIWorker picker,
		System.Predicate<PackingStation> predicate,
		out PackingStation station)
	{
		var node = state.WaitingQueue.First;
		while (node != null)
		{
			var next = node.Next;
			PackingStation candidate = node.Value;

			if (candidate == null)
			{
				state.WaitingQueue.Remove(node);
				node = next;
				continue;
			}

			if (candidate.CanRequestIncomingBox() == false)
			{
				RemoveWaitingStation(state, candidate);
				node = next;
				continue;
			}

			if (predicate(candidate) && candidate.TryReserveIncomingBox(picker))
			{
				RemoveWaitingStation(state, candidate);
				station = candidate;
				return true;
			}

			node = next;
		}

		station = null;
		return false;
	}

	private static void EnqueueWaitingStation(BuildingPackingState state, PackingStation station)
	{
		if (state == null || station == null || station.CanRequestIncomingBox() == false)
			return;

		if (state.WaitingSet.Add(station) == false)
			return;

		state.WaitingQueue.AddLast(station);
	}

	private static void RemoveWaitingStation(BuildingPackingState state, PackingStation station)
	{
		if (state == null || station == null)
			return;

		if (state.WaitingSet.Remove(station) == false)
			return;

		state.WaitingQueue.Remove(station);
	}

	private CapsuleBuffer FindClosestOutboundBuffer(uint buildingId, in Unity.Mathematics.int3 from)
	{
		CapsuleBuffer best = null;
		int bestDistance = int.MaxValue;

		foreach (CapsuleBuffer buffer in CapsuleBufferService.GetBuffers(buildingId))
			{
				if (buffer == null || buffer.CanReceiveOutboundItems() == false)
					continue;

				int distance = (int)Unity.Mathematics.math.lengthsq(buffer.GridPosition - from);
			if (distance >= bestDistance)
				continue;

			best = buffer;
			bestDistance = distance;
		}

		return best;
	}

	private CapsuleBuffer FindClosestOutboundBuffer(uint buildingId, in Unity.Mathematics.int3 from, FacilityFilter facilityFilter)
	{
		CapsuleBuffer best = null;
		int bestDistance = int.MaxValue;

		foreach (CapsuleBuffer buffer in CapsuleBufferService.GetBuffers(buildingId))
			{
				if (buffer == null || buffer.CanReceiveOutboundItems() == false)
					continue;

				if (facilityFilter.MatchesCurrentRules(buffer) == false)
					continue;

				int distance = (int)Unity.Mathematics.math.lengthsq(buffer.GridPosition - from);
			if (distance >= bestDistance)
				continue;

			best = buffer;
			bestDistance = distance;
		}

		return best;
	}

	public bool TryResolveOutboundBuffer(PackingStation sourceStation, out CapsuleBuffer targetBuffer)
	{
		targetBuffer = null;
		if (sourceStation == null || TryGetBuildingId(sourceStation, out uint buildingId) == false)
			return false;

		targetBuffer = FindClosestOutboundBuffer(buildingId, sourceStation.GridPosition);
		return targetBuffer != null;
	}

	public bool TryResolveOutboundBuffer(PackingStation sourceStation, FacilityFilter facilityFilter, out CapsuleBuffer targetBuffer)
	{
		targetBuffer = null;
		if (sourceStation == null || TryGetBuildingId(sourceStation, out uint buildingId) == false)
			return false;

		targetBuffer = FindClosestOutboundBuffer(buildingId, sourceStation.GridPosition, facilityFilter);
		return targetBuffer != null;
	}

	private bool TryClaimWaitingStation(
		BuildingPackingState state,
		System.Predicate<PackingStation> predicate,
		out PackingStation station)
	{
		var node = state.WaitingQueue.First;
		while (node != null)
		{
			var next = node.Next;
			PackingStation candidate = node.Value;
			if (candidate == null)
			{
				state.WaitingQueue.Remove(node);
				node = next;
				continue;
			}

			if (candidate.CanRequestIncomingBox() == false)
			{
				RemoveWaitingStation(state, candidate);
				node = next;
				continue;
			}

			if (predicate(candidate))
			{
				candidate.SetIncomingRequestSuspended(true);
				RemoveWaitingStation(state, candidate);
				station = candidate;
				return true;
			}

			node = next;
		}

		station = null;
		return false;
	}
}
