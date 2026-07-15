using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public partial class CargoStorageAddon 
	: PlatformAddon
{
	[SerializeField] private Transform cargoStorageSlot;
	[SerializeField] private int maxCargoSlot = 10;

	// queue 식으로 사용한다
	private LinkedList<BoxBase> cargosToLaunch = new();
	public IEnumerable<BoxBase> CargosToLaunch => cargosToLaunch;

	static private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	static private OutboundWorkflowService OutboundWorkflow => GameContext.Instance.OBWorkflowSvc;

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
			if (stack == null || stack.HasStatus(ItemStatus.Packed) == false)
			{
				Debug.LogError("CargoStorage: This Stack in box is not packed!!");
				return false;
			}
		}

		cargosToLaunch.AddLast(cargo);
		cargo.OnInvalidated -= HandleStoredCargoInvalidated;
		cargo.OnInvalidated += HandleStoredCargoInvalidated;

		cargo.transform.SetParent(transform);
		cargo.transform.SetLocalPositionAndRotation(Vector3.zero + new Vector3(0, cargosToLaunch.Count, 0), Quaternion.identity);

		int reported = OutboundWorkflow.ReportOutboundProgressFromManifest(cargo, PackageOutboundStage.Shipping);
		if (reported <= 0)
			Debug.LogWarning("[CargoStorageAddon] Stored packed cargo without manifest shipping progress.");

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
		cargo.OnInvalidated -= HandleStoredCargoInvalidated;
		cargosToLaunch.Remove(cargo);
	}

	private void HandleStoredCargoInvalidated(BoxBase cargo)
	{
		if (cargo == null || cargosToLaunch.Contains(cargo) == false)
			return;

		cargo.OnInvalidated -= HandleStoredCargoInvalidated;
		cargo.transform.SetParent(null, true);
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

}
