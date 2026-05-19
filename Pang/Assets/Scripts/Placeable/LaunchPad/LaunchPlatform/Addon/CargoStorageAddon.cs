using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CargoStorageAddon 
	: PlatformAddon
{
	[SerializeField] private Transform cargoStorageSlot;
	[SerializeField] private int maxCargoSlot = 10;

	// queue 식으로 사용한다
	private LinkedList<BoxBase> cargosToLaunch = new();
	public IEnumerable<BoxBase> CargosToLaunch => cargosToLaunch;

	static private OrderManager OrderMgr => GameContext.Instance.OrderMgr;

	public bool CanStoreCargo(BoxBase cargo)
	{
		return cargo != null && cargosToLaunch.Count < maxCargoSlot;
	}

	public bool TryStoreCargo(BoxBase cargo)
	{
		if (CanStoreCargo(cargo) == false)
			return false;

		foreach (var stack in cargo.Stacks)
		{
			if (stack is not ItemPackage)
			{
				Debug.LogError("CargoStorage: This Stack in box is not packed!!");
				return false;
			}
		}

		cargosToLaunch.AddLast(cargo);

		cargo.transform.SetParent(transform);
		cargo.transform.SetLocalPositionAndRotation(Vector3.zero + new Vector3(0, cargosToLaunch.Count, 0), Quaternion.identity);

		foreach (var stack in cargo.Stacks)
		{
			ItemPackage pkg = (ItemPackage)stack;
			pkg.ReportOutboundProgress(OrderMgr, PackageOutboundStage.Shipping);
		}

		return true;
	}

	private void RemoveCargo(BoxBase cargo)
	{
		if (cargo == null)
		{
			Debug.LogWarning("[CargoStorageAddon] Tried to remove null cargo");
			return;
		}

		if (cargosToLaunch.Contains(cargo) == false)
		{
			Debug.LogWarning("[CargoStorageAddon] Tried to remove not containing cargo");
			return;
		}

		cargo.transform.SetParent(null);
		cargosToLaunch.Remove(cargo);
	}

	private void Update()
	{
		for (var it = cargosToLaunch.First; it != null; )
		{
			var next = it.Next;

			if (station.TryGetAddon<LaunchPadAddon>(out var pad) && pad.TryLoad(it.Value))
				RemoveCargo(it.Value);

			it = next;
		}
	}

	public void RestoreState(IEnumerable<BoxBase> cargos)
	{
		cargosToLaunch.Clear();
		if (cargos == null)
			return;

		foreach (var cargo in cargos)
			TryStoreCargo(cargo);
	}

}
