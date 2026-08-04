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
			PlacingLine = currentLine == null ? null : new WorkLineSaveData
			{
				Action = currentLine.Action,
				SourcePlaceableId = getPlaceableId != null && currentLine.TargetComponent != null ? getPlaceableId(currentLine.TargetComponent.gameObject) : -1,
				ItemId = currentLine.ItemID,
				Quantity = currentLine.Quantity,
				CompleteQuantity = currentLine.CompleteQuantity,
				RelatedOrderLineId = registerOrderLine != null && currentLine.RelatedOrderLine != null ? registerOrderLine(currentLine.RelatedOrderLine) : -1,
				HasRequiredStatus = currentLine.RequiredStatus.HasValue,
				RequiredStatus = currentLine.RequiredStatus.GetValueOrDefault(),
				HasRequiredQuality = currentLine.RequiredQuality.HasValue,
				RequiredQuality = currentLine.RequiredQuality.GetValueOrDefault(),
				HasExcludedQuality = currentLine.ExcludedQuality.HasValue,
				ExcludedQuality = currentLine.ExcludedQuality.GetValueOrDefault(),
				HasConsumeSourcePickReservation = true,
				ConsumeSourcePickReservation = currentLine.ConsumeSourcePickReservation,
			},
		};
	}

	public void RestoreState(Phase currentPhase, bool isJobEnd, WorkLine currentLine)
	{
		CurrentPhase = currentPhase;
		IsJobEnd = isJobEnd;
		this.currentLine = currentLine;
	}
}
