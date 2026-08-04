using System;
using UnityEngine;

public sealed partial class WorkJob
{
	public void RestoreState(int currentLineIndex, WorkOp workType)
	{
		this.currentLineIndex = currentLineIndex;
		WorkType = workType;
	}

	public WorkJobSaveData CaptureState(Func<GameObject, int> getPlaceableId, Func<OrderLine, int> registerOrderLine)
	{
		WorkJobSaveData data = new()
		{
			JobId = JobID,
			WorkType = WorkType,
			CurrentLineIndex = currentLineIndex,
		};

		foreach (var line in Lines)
		{
			data.Lines.Add(new WorkLineSaveData
			{
				Action = line.Action,
				SourcePlaceableId = getPlaceableId != null && line.TargetComponent != null ? getPlaceableId(line.TargetComponent.gameObject) : -1,
				ItemId = line.ItemID,
				Quantity = line.Quantity,
				CompleteQuantity = line.CompleteQuantity,
				RelatedOrderLineId = registerOrderLine != null && line.RelatedOrderLine != null ? registerOrderLine(line.RelatedOrderLine) : -1,
				HasRequiredStatus = line.RequiredStatus.HasValue,
				RequiredStatus = line.RequiredStatus.GetValueOrDefault(),
				HasRequiredQuality = line.RequiredQuality.HasValue,
				RequiredQuality = line.RequiredQuality.GetValueOrDefault(),
				HasExcludedQuality = line.ExcludedQuality.HasValue,
				ExcludedQuality = line.ExcludedQuality.GetValueOrDefault(),
				HasConsumeSourcePickReservation = true,
				ConsumeSourcePickReservation = line.ConsumeSourcePickReservation,
			});
		}

		return data;
	}
}
