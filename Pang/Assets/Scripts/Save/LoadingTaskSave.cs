using System;
using UnityEngine;

public partial class LoadingTask
{
	public LoadingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new LoadingTaskSaveData
		{
			TargetPortId = SourcePort != null && getPlaceableId != null ? getPlaceableId(SourcePort.gameObject) : -1,
			TargetStationId = TargetStation != null && getPlaceableId != null ? getPlaceableId(TargetStation.gameObject) : -1,
			IsLoadEnd = isLoadEnd,
		};
	}

	public void RestoreState(bool isLoadEnd)
	{
		this.isLoadEnd = isLoadEnd;
	}
}
