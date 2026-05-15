using System.Collections.Generic;
using System;
using Unity.Mathematics;
using UnityEngine;

public enum WorkOp
{
	// 꺼내기 (picking의 pickjob/storing의 cargoport 이동
	Picking,

	// 적치 (storing의 물건 분류
	Storing,

	Packing,
}

public sealed class WorkLine
{
	public readonly ShelfBase Source;
	public readonly uint ItemID;
	public readonly int Quantity;
	public readonly OrderLine RelatedOrderLine = null;
	public int CompleteQuantity = 0;

	public bool IsComplete => Quantity == CompleteQuantity;

	public WorkLine(ShelfBase source, uint itemID, int quantity, OrderLine relatedOrderLine = null)
	{
		Source = source;
		ItemID = itemID;
		Quantity = quantity;
		RelatedOrderLine = relatedOrderLine;
	}
}


public sealed class WorkJob
{
	private int currentLineIndex = 0;
	
	public readonly List<WorkLine> Lines;
	public readonly int JobID;
	public WorkOp WorkType;

	public int CurrentLineIndex => currentLineIndex;
	public bool IsJobEnd => currentLineIndex >= Lines.Count;

	public WorkLine CurrentLine => IsJobEnd ? null : Lines[currentLineIndex];

	public WorkJob(int jobID, List<WorkLine> lines, WorkOp workType)
	{
		JobID = jobID;
		Lines = lines;
		WorkType = workType;
	}

	public void MoveToNextLine()
	{
		++currentLineIndex;
	}

	public void ResetForPacking()
	{
		WorkType = WorkOp.Packing;
		currentLineIndex = 0;
	}

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
				SourcePlaceableId = getPlaceableId != null ? getPlaceableId(line.Source.gameObject) : -1,
				ItemId = line.ItemID,
				Quantity = line.Quantity,
				CompleteQuantity = line.CompleteQuantity,
				RelatedOrderLineId = registerOrderLine != null && line.RelatedOrderLine != null ? registerOrderLine(line.RelatedOrderLine) : -1,
			});
		}

		return data;
	}
}
