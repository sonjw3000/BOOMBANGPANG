using System;
using UnityEngine;

public partial class LoadingTask
{
	public LoadingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new LoadingTaskSaveData
		{
			TargetPortId = targetPort != null && getPlaceableId != null ? getPlaceableId(targetPort.gameObject) : -1,
			IsLoadEnd = isLoadEnd,
		};
	}

	public void RestoreState(bool isLoadEnd)
	{
		this.isLoadEnd = isLoadEnd;
	}
}
