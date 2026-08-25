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
		public readonly LinkedList<PackingStation> CompletedOutputQueue = new();
		public readonly HashSet<PackingStation> CompletedOutputSet = new();
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
		RefreshStationState(facility);
	}

	protected override void OnUnregisterFacility(uint buildingId, PackingStation facility)
	{
		if (facility == null || statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return;

		state.Stations.Remove(facility);
		RemoveWaitingStation(state, facility);
		RemoveCompletedOutput(state, facility);
		state.QueuedPackingTasks.Remove(facility);

		if (state.Stations.Count <= 0 &&
			state.WaitingQueue.Count <= 0 &&
			state.CompletedOutputQueue.Count <= 0 &&
			state.QueuedPackingTasks.Count <= 0)
		{
			statesByBuildingId.Remove(buildingId);
		}

		if (GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.EvaluatePackingOutputWork(buildingId);
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

	public void RefreshStationState(PackingStation station)
	{
		if (station == null ||
			TryGetState(station, out uint buildingId, out BuildingPackingState state) == false)
		{
			return;
		}

		RefreshWaitingStation(station);
		if (station.EndPackingBox != null)
			EnqueueCompletedOutput(state, station);
		else
			RemoveCompletedOutput(state, station);

		if (GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.EvaluatePackingOutputWork(buildingId);
	}

	public void ReconcileRestoredIncomingRequests()
	{
		foreach (BuildingPackingState state in statesByBuildingId.Values)
		{
			for (int i = 0; i < state.Stations.Count; ++i)
			{
				PackingStation station = state.Stations[i];
				if (station?.IncomingRequestSuspended != true)
					continue;

				AIWorker packingWorker = station.CurrentPackingWorker;
				if (packingWorker != null && packingWorker.ShouldPrioritizeRecovery())
					continue;

				// A PackingInput task only claims a station transiently. Its current
				// WorkLine is intentionally not persisted, so that claim has no owner
				// after restore and must become eligible for a fresh Rule-based query.
				station.SetIncomingRequestSuspended(false);
			}
		}
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
		RefreshStationState(packingStation);
	}

	public bool HasCompletedOutput(uint buildingId)
	{
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return false;

		PruneCompletedOutputs(state);
		return state.CompletedOutputQueue.Count > 0;
	}

	public bool TryClaimCompletedOutput(uint buildingId, out PackingStation station)
	{
		station = null;
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return false;

		while (state.CompletedOutputQueue.First != null)
		{
			PackingStation candidate = state.CompletedOutputQueue.First.Value;
			state.CompletedOutputQueue.RemoveFirst();
			state.CompletedOutputSet.Remove(candidate);
			if (candidate?.EndPackingBox == null)
				continue;

			station = candidate;
			return true;
		}

		return false;
	}

	public void ReturnCompletedOutput(uint buildingId, PackingStation station)
	{
		if (station?.EndPackingBox == null ||
			statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false ||
			state.Stations.Contains(station) == false)
		{
			return;
		}

		EnqueueCompletedOutput(state, station);
		if (GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.EvaluatePackingOutputWork(buildingId);
	}

	public void GetCompletedOutputDemand(uint buildingId, out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;
		if (statesByBuildingId.TryGetValue(buildingId, out BuildingPackingState state) == false)
			return;

		PruneCompletedOutputs(state);
		foreach (PackingStation station in state.CompletedOutputQueue)
		{
			BoxBase box = station?.EndPackingBox?.Box;
			if (box == null)
				continue;

			++sourceCount;
			foreach (var itemTotal in box.ItemTotals)
				itemQuantity += Mathf.Max(0, itemTotal.Value);
		}
	}

	public void GetCompletedOutputDemand(out int sourceCount, out int itemQuantity)
	{
		sourceCount = 0;
		itemQuantity = 0;
		foreach (uint buildingId in statesByBuildingId.Keys)
		{
			GetCompletedOutputDemand(buildingId, out int buildingSources, out int buildingQuantity);
			sourceCount += buildingSources;
			itemQuantity += buildingQuantity;
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
		return TryGetState(station, out _, out state);
	}

	private bool TryGetState(
		PackingStation station,
		out uint buildingId,
		out BuildingPackingState state)
	{
		buildingId = 0;
		state = null;
		if (station == null)
			return false;

		foreach (KeyValuePair<uint, BuildingPackingState> entry in statesByBuildingId)
		{
			if (entry.Value.Stations.Contains(station) == false)
				continue;

			buildingId = entry.Key;
			state = entry.Value;
			return true;
		}

		return false;
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

	private static void EnqueueCompletedOutput(BuildingPackingState state, PackingStation station)
	{
		if (state == null || station?.EndPackingBox == null || state.CompletedOutputSet.Add(station) == false)
			return;

		state.CompletedOutputQueue.AddLast(station);
	}

	private static void RemoveCompletedOutput(BuildingPackingState state, PackingStation station)
	{
		if (state == null || station == null || state.CompletedOutputSet.Remove(station) == false)
			return;

		state.CompletedOutputQueue.Remove(station);
	}

	private static void PruneCompletedOutputs(BuildingPackingState state)
	{
		if (state == null)
			return;

		LinkedListNode<PackingStation> node = state.CompletedOutputQueue.First;
		while (node != null)
		{
			LinkedListNode<PackingStation> next = node.Next;
			PackingStation station = node.Value;
			if (station?.EndPackingBox == null || state.Stations.Contains(station) == false)
			{
				state.CompletedOutputQueue.Remove(node);
				state.CompletedOutputSet.Remove(station);
			}

			node = next;
		}
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
