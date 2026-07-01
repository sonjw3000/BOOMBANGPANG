public partial class RocketService
{
	public RocketServiceSaveData CaptureState()
	{
		RocketServiceSaveData data = new();

		foreach (Rocket rocket in activeRockets)
		{
			if (rocket == null || rocket.State == Rocket.RocketState.Deactivated)
				continue;

			if (GridService != null && GridService.IsPlacedObject(rocket.gameObject))
				continue;

			data.ActiveRockets.Add(rocket.CaptureState());
		}

		return data;
	}

	public void RestoreState(RocketServiceSaveData data)
	{
		if (data == null)
			return;

		if (data.ActiveRockets == null)
			return;

		foreach (RocketSaveData rocketData in data.ActiveRockets)
		{
			if (rocketData == null || rocketData.State == Rocket.RocketState.Deactivated)
				continue;

			if (rocketPool.Count <= 0)
				InstantiateNewRocket();

			if (rocketPool.TryDequeue(out Rocket rocket) == false || rocket == null)
			{
				UnityEngine.Debug.LogWarning("[RocketService] Failed to restore active rocket because the pool returned no rocket.");
				continue;
			}

			rocket.transform.SetParent(transform, true);
			rocket.gameObject.SetActive(true);
			rocket.RestoreState(rocketData);
			rocket.enabled = rocket.State == Rocket.RocketState.Landing ||
				rocket.State == Rocket.RocketState.Launching;

			activeRockets.Add(rocket);
		}
	}

	public void ResetRuntimeState()
	{
		foreach (Rocket rocket in activeRockets)
		{
			if (rocket == null)
				continue;

			rocket.TryUndockCapsule(out _);
			rocket.gameObject.SetActive(false);
			if (rocketPoolParent != null)
				rocket.transform.SetParent(rocketPoolParent.transform, false);

			if (rocketPool.Contains(rocket) == false)
				rocketPool.Enqueue(rocket);
		}

		activeRockets.Clear();
	}
}
