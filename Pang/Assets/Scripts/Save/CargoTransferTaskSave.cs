using System;
using UnityEngine;

public static class CargoTransferTaskSaveExtensions
{
	public static CargoTransferTaskSaveData CaptureState(this CargoTransferTask task, Func<GameObject, int> getPlaceableId)
	{
		return task == null ? null : new CargoTransferTaskSaveData
		{
			SourcePortId = getPlaceableId != null && task.SourcePort != null ? getPlaceableId(task.SourcePort.gameObject) : -1,
			TargetPortId = getPlaceableId != null && task.TargetPort != null ? getPlaceableId(task.TargetPort.gameObject) : -1,
		};
	}
}
