public partial class LaunchPadAddon
{
	public void RestoreState(BoxBase cargo, bool ready)
	{
		cargoToLaunch = cargo;
		readyToLaunch = ready;
	}
}
