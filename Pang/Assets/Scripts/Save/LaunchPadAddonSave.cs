public partial class LaunchPadAddon
{
	public void RestoreState(BoxBase cargo, bool ready)
	{
		if (cargoToLaunch != null)
			cargoToLaunch.OnInvalidated -= HandleCargoInvalidated;

		cargoToLaunch = cargo;
		if (cargoToLaunch != null)
		{
			cargoToLaunch.OnInvalidated -= HandleCargoInvalidated;
			cargoToLaunch.OnInvalidated += HandleCargoInvalidated;
		}

		readyToLaunch = ready;
	}
}
