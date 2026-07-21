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

public enum WorkLineAction
{
	Pick,
	Put,
}

public enum WorkPlanResult
{
	Issued,
	Waiting,
	SwitchPhase,
	Completed,
}

public sealed class WorkLine
{
	public readonly WorkLineAction Action;
	public readonly IItemContainer Container;
	public readonly IGridPlaceable Target;
	public readonly uint ItemID;
	public readonly int Quantity;
	public readonly OrderLine RelatedOrderLine = null;
	public readonly ItemStatus? RequiredStatus = null;
	public readonly bool ConsumeSourcePickReservation;
	public int CompleteQuantity = 0;

	public bool IsComplete => Quantity == CompleteQuantity;
	public Component TargetComponent => Target as Component;
	public string TargetName => TargetComponent != null ? TargetComponent.name : "None";

	public WorkLine(ShelfBase source, uint itemID, int quantity, OrderLine relatedOrderLine = null, bool consumeSourcePickReservation = true)
		: this(WorkLineAction.Pick, source, source, itemID, quantity, relatedOrderLine, consumeSourcePickReservation: consumeSourcePickReservation)
	{
	}

	public WorkLine(
		WorkLineAction action,
		IItemContainer container,
		IGridPlaceable target,
		uint itemID,
		int quantity,
		OrderLine relatedOrderLine = null,
		ItemStatus? requiredStatus = null,
		bool consumeSourcePickReservation = true)
	{
		Action = action;
		Container = container;
		Target = target;
		ItemID = itemID;
		Quantity = quantity;
		RelatedOrderLine = relatedOrderLine;
		RequiredStatus = requiredStatus;
		ConsumeSourcePickReservation = consumeSourcePickReservation;
	}
}


public sealed partial class WorkJob
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

}
