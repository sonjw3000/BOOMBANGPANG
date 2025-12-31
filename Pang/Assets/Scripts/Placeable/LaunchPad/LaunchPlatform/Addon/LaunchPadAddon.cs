using UnityEngine;

public class LaunchPadAddon : PlatformAddon
{
	private BoxBase cargoToLaunch = null;
	private Rocket rocket = null;

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
		if (cargoToLaunch != null)
		{
			// launch the box
			//boxToLaunch.LaunchFromPad();
			// clear the reference

			// todo
			// rocket launch effect
			// rocket animation
			// sound effect
			// 물량 조절
			//GameContext.Instance.WMSys.ItemLedger.Launch();

			Debug.Log("LaunchPadAddon: Launching cargo!");

			readyToLaunch = false;

			cargoToLaunch = null;
			rocket = null;
		}
	}

	private void Update()
	{
		if (readyToLaunch)
		{
			Launch();
		}
	}

}

