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

	public bool IsReadyToLaunch => cargoToLaunch != null && rocket != null;

	public bool TryLoad(in BoxBase cargo)
	{
		// todo
		// rocket을 추가해야함
		//if (rocket == null) return false;
		if (cargoToLaunch != null) return false;

		cargoToLaunch = cargo;
		readyToLaunch = true;

		return true;
	}

	private void Launch()
	{
		if (cargoToLaunch == null)
			return;
		// launch the box
		//boxToLaunch.LaunchFromPad();
		// clear the reference
		
		// todo
		// rocket launch effect
		// rocket animation
		// sound effect
		// 물량 조절
		//GameContext.Instance.WMSys.ItemLedger.Launch();

		foreach (var stack in cargoToLaunch.Stacks)
		{
			if (stack is ItemPackage pkg == false)
			{
				Debug.LogError("LaunchPad: This Stack in box is not packed!!");
				return;
			}

			OrderMgr.ChangeOrderStatus(pkg.RelatedOrderLine, OrderStatus.IndDelivery);
		}

		Debug.Log("LaunchPadAddon: Launching cargo!");

		// todo
		// weeks to seconds
		// delivery type(emergency/normal) 에 따라서 weeks를 조절해야함
		// 
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

}

