using System;
using UnityEngine;

public sealed partial class PickingTask
{
	public PickingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId, Func<OrderLine, int> registerOrderLine)
	{
		return new PickingTaskSaveData
		{
			Job = pickJob?.CaptureState(getPlaceableId, registerOrderLine),
			BuildingId = buildingId,
			IsPickingPhaseEnd = isPickingPhaseEnd,
			IsTaskEnd = isTaskEnd,
		};
	}
}
