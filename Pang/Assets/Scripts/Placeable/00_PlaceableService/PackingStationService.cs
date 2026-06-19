using System.Collections.Generic;
using UnityEngine;

public class PackingStationService : FacilityService<PackingStation>
{
	private sealed class BuildingPackingState
	{
		public readonly List<PackingStation> Stations = new();
		public readonly LinkedList<PackingStation> WaitingQueue = new();
		public readonly HashSet<PackingStation> WaitingSet = new();
		public readonly HashSet<PackingStation> QueuedPackingTasks = new();
	}

	private static CargoPortService CargoPortService => GameContext.Instance.CargoPortSvc;
	private static TaskManager TaskManager => GameContext.Instance.TaskMgr;

	private readonly Dictionary<uint, BuildingPackingState> statesByBuildingId = new();

	protected override void OnRegisterFacility(uint buildingId, PackingStation facility)
	{
		if (facility == null)
			return;

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

		CargoPort port = CargoPortService.FindClosestAvailablePort(
			packingStation.GridPosition,
			InteractionKind.Put,
			buildingId,
			candidate => candidate is OutboundCargoPort);
		if (port == null)
			return;

		TransferContext from = new TransferContext(packingStation, TransferObjectType.Box);
		TransferContext to = new TransferContext(port, TransferObjectType.Box);

		TaskManager.EnqueueTask(new WaterTask(from, to));
	}

	public void ResetRuntimeState()
	{
		statesByBuildingId.Clear();
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
}
