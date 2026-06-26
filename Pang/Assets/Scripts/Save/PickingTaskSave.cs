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
				CurrentPlaceLine = currentPlaceLine != null ? new WorkLineSaveData
				{
					Action = currentPlaceLine.Action,
					SourcePlaceableId = currentPlaceLine.TargetComponent != null ? getPlaceableId(currentPlaceLine.TargetComponent.gameObject) : -1,
					ItemId = currentPlaceLine.ItemID,
					Quantity = currentPlaceLine.Quantity,
					CompleteQuantity = currentPlaceLine.CompleteQuantity,
					RelatedOrderLineId = registerOrderLine != null && currentPlaceLine.RelatedOrderLine != null ? registerOrderLine(currentPlaceLine.RelatedOrderLine) : -1,
				} : null,
				PlacingLineIndex = placingLineIndex,
			};
		}
	}
