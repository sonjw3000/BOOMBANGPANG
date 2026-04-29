using System.Collections.Generic;
using Unity.Mathematics;

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

	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;

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
		var WorkLine = Lines[currentLineIndex];

		if (WorkType != WorkOp.Storing)
		{
			// change order status to order manager
			var status = WorkType == WorkOp.Packing ? OrderStatus.Picking : OrderStatus.Packaging;

			OrderMgr.ChangeOrderStatus(WorkLine.RelatedOrderLine, status);
		}

		++currentLineIndex;
	}

	public void ResetForPacking()
	{
		WorkType = WorkOp.Packing;
		currentLineIndex = 0;
	}
}
