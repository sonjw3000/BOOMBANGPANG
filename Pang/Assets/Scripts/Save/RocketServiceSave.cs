public partial class RocketService
{
	public RocketServiceSaveData CaptureState()
	{
		return new RocketServiceSaveData();
	}

	public void RestoreState(RocketServiceSaveData data)
	{
	}

	public void ResetRuntimeState()
	{
		activeRockets.Clear();
	}
}
