using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : GridPlaceableManager<CargoPort>
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

}
