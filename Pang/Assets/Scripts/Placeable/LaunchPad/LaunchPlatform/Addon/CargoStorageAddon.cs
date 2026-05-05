using System.Collections.Generic;
using UnityEngine;

public class CargoStorageAddon 
	: PlatformAddon
{
	[SerializeField] private int maxCargoSlot = 10;

	// queue 식으로 사용한다
	private LinkedList<BoxBase> cargosToLaunch = new();

	static private OrderManager OrderMgr => GameContext.Instance.OrderMgr;

	public void StoreCargo(BoxBase cargo)
	{
		cargosToLaunch.AddLast(cargo);
		
		foreach (var stack in cargo.Stacks)
		{
			if (stack is ItemPackage pkg == false)
			{
				Debug.LogError("CargoStorage: This Stack in box is not packed!!");
				return;
			}
			OrderMgr.ChangeOrderStatus(pkg.RelatedOrderLine, OrderStatus.Shipping);
		}

	}


	private void Update()
	{
		for (var it = cargosToLaunch.First; it != null; )
		{
			var next = it.Next;

			if (station.TryGetLaunchablePad(it.Value, out var pad))
			{
				pad.TryLoad(it.Value);
				cargosToLaunch.Remove(it);
			}

			it = next;
		}
	}

}
