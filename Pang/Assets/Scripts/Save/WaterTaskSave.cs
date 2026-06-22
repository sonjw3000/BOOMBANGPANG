using System;
using UnityEngine;

public partial class WaterTask
{
	public WaterTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new WaterTaskSaveData
		{
			From = CaptureTransferContext(from, getPlaceableId),
			To = CaptureTransferContext(to, getPlaceableId),
			WorkPhase = workPhase,
			HasPicked = hasPicked,
		};
	}

	public void RestoreState(bool workPhase, bool hasPicked)
	{
		this.workPhase = workPhase;
		this.hasPicked = hasPicked;
	}

	private static TransferContextSaveData CaptureTransferContext(TransferContext context, Func<GameObject, int> getPlaceableId)
	{
		if (context?.target is not Component targetComponent)
			return null;

		return new TransferContextSaveData
		{
			TargetPlaceableId = getPlaceableId != null ? getPlaceableId(targetComponent.gameObject) : -1,
			TransferType = context.transferType,
		};
	}
}
