using System;
using UnityEngine;

public sealed partial class PickingTask
{
	public PickingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId, Func<OrderLine, int> registerOrderLine)
	{
		return new PickingTaskSaveData
		{
			Job = pickJob?.CaptureState(getPlaceableId, registerOrderLine),
			IsPickingPhaseEnd = isPickingPhaseEnd,
			IsTaskEnd = isTaskEnd,
		};
	}

	public void RestoreState(bool isPickingPhaseEnd, bool isTaskEnd)
	{
		this.isPickingPhaseEnd = isPickingPhaseEnd;
		this.isTaskEnd = isTaskEnd;
	}
}
