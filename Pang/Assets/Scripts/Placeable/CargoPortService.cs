using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService
{
	private List<CargoPort> cargoPorts = new();

	private readonly Dictionary<uint, List<CargoPort>> cargoPortsByItem = new();

	public Dictionary<uint, List<CargoPort>> CargoPortsByItem => cargoPortsByItem;

	private InboundWorkflowManager IBMgr => GameContext.Instance.IBWorkflowMgr;


	public CargoPort GetClosestAvailablePort(in int3 pos)
	{
		CargoPort port = null;
		int maxDist = int.MaxValue;

		foreach (CargoPort p in cargoPorts)
		{
			int3 portPos = p.GridPosition;
			int3 posDiff = pos - portPos;
			int dist =
				posDiff.x * posDiff.x +
				posDiff.y * posDiff.y +
				posDiff.z * posDiff.z;

			if (dist < maxDist)
			{
				maxDist = dist;
				port = p;
			}
		}

		return port;
	}

	public void RegisterPort(CargoPort port)
	{
		port.OnItemRegistered += OnPortItemAdded;
		port.OnItemUnregistered += OnPortItemRemoved;
		cargoPorts.Add(port);
	}

	public void UnregisterPort(CargoPort port)
	{
		port.OnItemRegistered -= OnPortItemAdded;
		port.OnItemUnregistered -= OnPortItemRemoved;
		cargoPorts.Remove(port);
	}

	// event
	public void OnPortItemAdded(ShelfBase port, uint itemId)
	{
		if (cargoPortsByItem.TryGetValue(itemId, out var ports) == false)
		{
			ports = new();
			cargoPortsByItem.Add(itemId, ports);
		}

		ports.Add((CargoPort)port);
	}

	public void OnPortItemRemoved(ShelfBase port, uint itemId)
	{
		if (cargoPortsByItem.TryGetValue(itemId, out var ports) == false)
		{
			// should not happen
			Debug.LogError("ERROR!! No id here but tried to remove port");
			cargoPortsByItem[itemId] = new();
		}
		cargoPortsByItem[itemId].Remove((CargoPort)port);
	}

}
