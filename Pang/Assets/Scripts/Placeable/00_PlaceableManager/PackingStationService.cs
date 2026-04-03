using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PackingStationService : MonoBehaviour
{
	static private CargoPortService CargoService => GameContext.Instance.OBWorkflowMgr.CargoPorts;
	static private TaskManager TaskManager => GameContext.Instance.TaskMgr;

	// all data
	private List<PackingStation> packingStations = new();

	// queue for waiting totebox
	private Queue<PackingStation> waitingQueue = new();

	public PackingStation GetAvailableStationToWork(in int3 workerPos)
	{
		// get nearest station
		PackingStation nearestStation = null;
		float dist = float.PositiveInfinity;

		foreach (var station in packingStations)
		{
			if (station.IsNoWorkerAssigned)
			{
				float d = math.distance(workerPos, station.GridPosition);
				if (d < dist)
				{
					nearestStation = station;
					dist = d;
				}
			}
		}

		return nearestStation;
	}

	public bool TryGetWaitingStation(out PackingStation station)
	{
		if (waitingQueue.Count > 0)
		{
			station = waitingQueue.Dequeue();
			return true;
		}
		station = null;
		return false;
	}

	// if worker arrived and there's no box to pick, it will be called
	public void Enqueue(PackingStation packingStation)
	{
		waitingQueue.Enqueue(packingStation);
	}

	public void Register(PackingStation packingStation)
	{
		packingStations.Add(packingStation);
	}

	public void UnRegister(PackingStation packingStation)
	{
		packingStations.Remove(packingStation);
	}

	public void OnPackingComplete(PackingStation packingStation)
	{
		var port = CargoService.GetClosestAvailablePort(packingStation.GridPosition);

		TransferContext from = new TransferContext(packingStation, TransferObjectType.Box);
		TransferContext to = new TransferContext(port, TransferObjectType.Item);

		TaskManager.EnqueueTask(new WaterTask(from, to));
	}
}
