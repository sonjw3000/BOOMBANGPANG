using System;
using UnityEngine;

public static class CapsuleTransferTaskSaveExtensions
{
	public static CapsuleTransferTaskSaveData CaptureState(this CapsuleRelocationTask task, Func<GameObject, int> getPlaceableId)
	{
		return task == null ? null : new CapsuleTransferTaskSaveData
		{
			HasTaskType = true,
			TaskType = task.Type,
			IsInbound = task.Type == WorkerTask.TaskType.IB,
			BuildingId = task.BuildingId,
			SourcePlaceableId = getPlaceableId != null && task.SourceDock != null ? getPlaceableId(task.SourceDock.gameObject) : -1,
			TargetPlaceableId = getPlaceableId != null && task.TargetDock != null ? getPlaceableId(task.TargetDock.gameObject) : -1,
		};
	}
}
