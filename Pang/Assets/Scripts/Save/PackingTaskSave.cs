using System;
using UnityEngine;

public partial class PackingTask
{
	public PackingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new PackingTaskSaveData
		{
			TargetStationId = targetStation != null && getPlaceableId != null ? getPlaceableId(targetStation.gameObject) : -1,
			IsTaskEnd = isTaskEnd,
		};
	}

	public void RestoreState(bool isTaskEnd)
	{
		this.isTaskEnd = isTaskEnd;
	}
}
