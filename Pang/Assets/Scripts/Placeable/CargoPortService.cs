using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : MonoBehaviour
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
		port.OnItemPresentChanged += OnPortItemPresentChanged;
		//port.OnItemQuantityChanged += OnPortItemQuantityChanged;
		cargoPorts.Add(port);
	}

	public void UnregisterPort(CargoPort port)
	{
		port.OnItemPresentChanged -= OnPortItemPresentChanged;
		//port.OnItemQuantityChanged -= OnPortItemQuantityChanged;
		cargoPorts.Remove(port);
	}

	// to shelfs
	private void OnPortItemPresentChanged(ShelfBase port, uint itemId, bool present)
	{
		if (present)
			OnPortItemAdded(port, itemId);
		else
			OnPortItemRemoved(port, itemId);
	}

	//private void OnPortItemQuantityChanged(uint itemId, int quantityDelta)
	//{


	//}

	// event
	private void OnPortItemAdded(ShelfBase port, uint itemId)
	{
		if (cargoPortsByItem.TryGetValue(itemId, out var ports) == false)
		{
			ports = new();
			cargoPortsByItem.Add(itemId, ports);
		}

		ports.Add((CargoPort)port);
	}

	private void OnPortItemRemoved(ShelfBase port, uint itemId)
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
