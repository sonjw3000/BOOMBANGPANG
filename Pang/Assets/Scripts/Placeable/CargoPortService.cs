using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : MonoBehaviour
{
	private List<CargoPort> cargoPorts = new();

	public event System.Action<ShelfBase, uint, bool> OnItemPresentChanged;
	public event System.Action<ShelfBase, uint, int> OnItemQuantityChanged;

	public CargoPort GetClosestAvailablePort(in int3 pos)
	{
		CargoPort port = null;
		int maxDist = int.MaxValue;

		foreach (CargoPort p in cargoPorts)
		{
			if (p.InputReady == false)
				continue;

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
		port.OnItemPresentChanged += HandlePresentChange;
		port.OnItemQuantityChanged += HandleItemQuantityChanged;
		cargoPorts.Add(port);
	}

	public void UnregisterPort(CargoPort port)
	{
		port.OnItemPresentChanged -= HandlePresentChange;
		port.OnItemQuantityChanged -= HandleItemQuantityChanged;
		cargoPorts.Remove(port);
	}

	private void HandlePresentChange(ShelfBase port, uint itemId, bool present)
	{
		OnItemPresentChanged?.Invoke(port, itemId, present);
	}

	private void HandleItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		OnItemQuantityChanged?.Invoke(port, itemId, quantityDelta);
	}

	// to shelfs

}
