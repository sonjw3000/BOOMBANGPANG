using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : GridPlaceableManager<CargoPort>, ICollectSupplySource
{
	//private List<CargoPort> cargoPorts = new();

	public event System.Action<ShelfBase, uint, bool> OnItemPresentChanged;
	public event System.Action<ShelfBase, uint, int> OnItemQuantityChanged;
	public event System.Action<ShelfBase, uint, int> OnReserveQuantityChanged;

	protected override void OnRegister(CargoPort port)
	{
		port.OnItemPresentChanged += HandlePresentChange;
		port.OnItemQuantityChanged += HandleItemQuantityChanged;
		port.OnItemReservedPickChanged += HandleReserveQuantityChanged;
	}

	protected override void OnUnregister(CargoPort port)
	{
		port.OnItemPresentChanged -= HandlePresentChange;
		port.OnItemQuantityChanged -= HandleItemQuantityChanged;
		port.OnItemReservedPickChanged -= HandleReserveQuantityChanged;
	}

	private void HandlePresentChange(ShelfBase port, uint itemId, bool present)
	{
		OnItemPresentChanged?.Invoke(port, itemId, present);
	}

	private void HandleItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		OnItemQuantityChanged?.Invoke(port, itemId, quantityDelta);
	}

	private void HandleReserveQuantityChanged(ShelfBase port, uint itemId, int reservedQuantityDelta)
	{
		OnReserveQuantityChanged?.Invoke(port, itemId, reservedQuantityDelta);
	}
	// to shelfs

	public CargoPort GetClosestAvailableTargetForBox(in int3 pos, InteractionKind interactionKind, BoxBase box)
	{
		CargoPort target = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < items.Count; ++i)
		{
			CargoPort candidate = items[i];
			if (candidate.IsInteractionAvailable(interactionKind) == false ||
				CanAcceptAllStacks(candidate, box) == false)
				continue;

			int3 boxPos = candidate.GridPosition;
			int3 posDelta = new int3(pos.x - boxPos.x, 0, pos.z - boxPos.z);
			posDelta.x *= posDelta.x;
			posDelta.y *= posDelta.y;
			posDelta.z *= posDelta.z;

			int sum = posDelta.x + posDelta.y + posDelta.z;
			if (posPowMin > sum)
			{
				posPowMin = sum;
				target = candidate;
			}
		}

		return target;
	}

	private static bool CanAcceptAllStacks(CargoPort port, BoxBase box)
	{
		if (port == null || box == null)
			return false;

		if (box.Stacks.Count > port.MaxStack - port.Stacks.Count)
			return false;

		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			if (port.CanAcceptStack(box.Stacks[i]) == false)
				return false;
		}

		return true;
	}

	public IEnumerable<ShelfBase> GetSources(uint itemId)
	{
		for (int i = 0; i < items.Count; ++i)
		{
			CargoPort port = items[i];
			if (port != null && port.GetPickableQuantity(itemId) > 0)
				yield return port;
		}
	}

}
