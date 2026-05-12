using System.Collections.Generic;
using UnityEngine;

public class PackingStationService : MonoBehaviour
{
	static private CargoPortService CargoService => GameContext.Instance.OBWorkflowMgr.CargoPorts;
	static private TaskManager TaskManager => GameContext.Instance.TaskMgr;

	private readonly List<PackingStation> packingStations = new();
	private readonly LinkedList<PackingStation> waitingQueue = new();
	private readonly HashSet<PackingStation> waitingSet = new();
	private readonly HashSet<PackingStation> queuedPackingTasks = new();

	public bool TryReserveWaitingStation(AIWorker picker, out PackingStation station)
	{
		if (TryReserveWaitingStation(picker, candidate => candidate.CurrentPackingWorker != null, out station))
			return true;

		return TryReserveWaitingStation(picker, candidate => candidate.CurrentPackingWorker == null, out station);
	}

	private bool TryReserveWaitingStation(AIWorker picker, System.Predicate<PackingStation> predicate, out PackingStation station)
	{
		var node = waitingQueue.First;
		while (node != null)
		{
			var next = node.Next;
			var candidate = node.Value;

			if (candidate == null)
			{
				waitingQueue.Remove(node);
				node = next;
				continue;
			}

			if (candidate.CanRequestIncomingBox() == false)
			{
				RemoveWaitingStation(candidate);
				node = next;
				continue;
			}

			if (predicate(candidate) && candidate.TryReserveIncomingBox(picker))
			{
				RemoveWaitingStation(candidate);
				station = candidate;
				return true;
			}

			node = next;
		}

		station = null;
		return false;
	}

	public void RefreshWaitingStation(PackingStation station)
	{
		if (station == null)
			return;

		if (station.CanRequestIncomingBox())
			EnqueueWaitingStation(station);
		else
			RemoveWaitingStation(station);
	}

	public void EnqueueWaitingStation(PackingStation station)
	{
		if (station == null || station.CanRequestIncomingBox() == false)
			return;

		if (waitingSet.Add(station) == false)
			return;

		waitingQueue.AddLast(station);
	}

	public void RemoveWaitingStation(PackingStation station)
	{
		if (station == null)
			return;

		if (waitingSet.Remove(station) == false)
			return;

		waitingQueue.Remove(station);
	}

	public void Register(PackingStation packingStation)
	{
		packingStations.Add(packingStation);
		RefreshWaitingStation(packingStation);
	}

	public void UnRegister(PackingStation packingStation)
	{
		packingStations.Remove(packingStation);
		RemoveWaitingStation(packingStation);
		queuedPackingTasks.Remove(packingStation);
	}

	public void RequestPackingTaskIfNeeded(PackingStation packingStation)
	{
		if (packingStation == null || packingStation.HasWaitingBox == false)
			return;

		if (queuedPackingTasks.Contains(packingStation))
			return;

		if (packingStation.CurrentPackingWorker?.CurrentTask is PackingTask currentTask &&
			currentTask.TargetStation == packingStation)
		{
			return;
		}

		queuedPackingTasks.Add(packingStation);
		TaskManager.EnqueueTask(new PackingTask(packingStation));
	}

	public void OnPackingTaskAssigned(PackingStation packingStation)
	{
		if (packingStation != null)
			queuedPackingTasks.Remove(packingStation);
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
		var port = CargoService.GetClosestAvailableTarget(packingStation.GridPosition, InteractionKind.Put);

		TransferContext from = new TransferContext(packingStation, TransferObjectType.Box);
		TransferContext to = new TransferContext(port, TransferObjectType.Item);

		TaskManager.EnqueueTask(new WaterTask(from, to));
	}
}
