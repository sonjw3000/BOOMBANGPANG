using System;
using UnityEngine;

public static class CapsuleTransferTaskSaveExtensions
{
	public static CapsuleTransferTaskSaveData CaptureState(this IBTask task, Func<GameObject, int> getPlaceableId)
	{
		return task == null ? null : new CapsuleTransferTaskSaveData
		{
			IsInbound = true,
			BuildingId = task.BuildingId,
			SourcePlaceableId = getPlaceableId != null && task.SourcePort != null ? getPlaceableId(task.SourcePort.gameObject) : -1,
			TargetPlaceableId = getPlaceableId != null && task.TargetBuffer != null ? getPlaceableId(task.TargetBuffer.gameObject) : -1,
		};
	}

	public static CapsuleTransferTaskSaveData CaptureState(this OBTask task, Func<GameObject, int> getPlaceableId)
	{
		return task == null ? null : new CapsuleTransferTaskSaveData
		{
			IsInbound = false,
			BuildingId = task.BuildingId,
			SourcePlaceableId = getPlaceableId != null && task.SourceBuffer != null ? getPlaceableId(task.SourceBuffer.gameObject) : -1,
			TargetPlaceableId = getPlaceableId != null && task.TargetPort != null ? getPlaceableId(task.TargetPort.gameObject) : -1,
		};
	}
}
