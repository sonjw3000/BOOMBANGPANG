using System;
using UnityEngine;

public partial class StoringTask
{
	public StoringTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId, Func<OrderLine, int> registerOrderLine)
	{
		return new StoringTaskSaveData
		{
			BuildingId = buildingId,
			Job = storeJob?.CaptureState(getPlaceableId, registerOrderLine),
			CurrentPhase = CurrentPhase,
			IsJobEnd = IsJobEnd,
			PlacingLine = placingLine == null ? null : new WorkLineSaveData
			{
				SourcePlaceableId = getPlaceableId != null ? getPlaceableId(placingLine.Source.gameObject) : -1,
				ItemId = placingLine.ItemID,
				Quantity = placingLine.Quantity,
				CompleteQuantity = placingLine.CompleteQuantity,
				RelatedOrderLineId = registerOrderLine != null && placingLine.RelatedOrderLine != null ? registerOrderLine(placingLine.RelatedOrderLine) : -1,
			},
		};
	}

	public void RestoreState(Phase currentPhase, bool isJobEnd, WorkLine placingLine)
	{
		CurrentPhase = currentPhase;
		IsJobEnd = isJobEnd;
		this.placingLine = placingLine;
	}
}
