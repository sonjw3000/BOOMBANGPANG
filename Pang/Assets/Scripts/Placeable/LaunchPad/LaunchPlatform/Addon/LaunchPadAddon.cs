using UnityEngine;

public class LaunchPadAddon : PlatformAddon
{
	private BoxBase cargoToLaunch = null;
	private Rocket rocket = null;

	private static OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private static OrderDeliveryManager OrderDelivery => GameContext.Instance.OrderDelivery;
	private static GameTime GameTime => GameContext.Instance.GameTime;

	private bool readyToLaunch = false;

	public bool IsReady => readyToLaunch;
	public BoxBase CargoToLaunch => cargoToLaunch;

	public bool IsReadyToLaunch => cargoToLaunch != null && rocket != null;

	public bool TryLoad(BoxBase cargo)
	{
		// todo
		// rocket을 추가해야함
		//if (rocket == null) return false;
		if (cargoToLaunch != null) return false;

		// todo
		// rocket의 cargo point에 cargo를 넣어야함

		cargoToLaunch = cargo;
		readyToLaunch = true;

		return true;
	}

	private void Launch()
	{
		if (cargoToLaunch == null)
			return;

		// Spawn and launch visual rocket
		var visualRocket = GameContext.Instance.RocketMgr.GetRocketForLaunch(transform.position);
		if (visualRocket != null)
		{
			// Attach cargo to rocket for visual effect
			cargoToLaunch.transform.SetParent(visualRocket.transform);
			cargoToLaunch.transform.localPosition = Vector3.up * 1.0f; // Offset to sit on rocket
			visualRocket.Launch();
		}
		
		foreach (var stack in cargoToLaunch.Stacks)
		{
			if (stack is ItemPackage pkg == false)
			{
				Debug.LogError("LaunchPad: This Stack in box is not packed!!");
				return;
			}

			Debug.Log($"OrderID: {pkg.RelatedOrderLine.ParentOrder.OrderID} / item: {pkg.ItemID}, qty: {pkg.Quantity} Launched!!");
			OrderMgr.ReportInDelivery(pkg.RelatedOrderLine, pkg.Quantity);
		}

		OrderDelivery.DeliverCargo(cargoToLaunch, GameTime.WeekToSeconds(4));

		readyToLaunch = false;
		cargoToLaunch = null;

		rocket = null;
	}

	private void Update()
	{
		if (readyToLaunch)
		{
			Launch();
		}
	}

	public void RestoreState(BoxBase cargo, bool ready)
	{
		cargoToLaunch = cargo;
		readyToLaunch = ready;
	}

}

