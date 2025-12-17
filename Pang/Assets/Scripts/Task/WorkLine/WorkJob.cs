using System.Collections.Generic;
using Unity.Mathematics;

//public enum WorkOp
//{
//	// 꺼내기 (picking의 pickjob/storing의 cargoport 이동
//	Take,

//	// 적치 (storing의 물건 분류
//	Put,
//}

public sealed class WorkLine
{
	public readonly ShelfBase Source;
	public readonly uint ItemID;
	public readonly int Quantity;

	public int3 GoalPosition => Source.InteractionPoints[0];

	public WorkLine(ShelfBase source, uint itemID, int quantity)
	{
		Source = source;
		ItemID = itemID;
		Quantity = quantity;
	}
}


public sealed class WorkJob
{
	private int currentLineIndex = 0;

	public readonly List<WorkLine> Lines;

	public readonly int JobID;

	public WorkJob(int jobID, List<WorkLine> lines)
	{
		JobID = jobID;
		Lines = lines;
	}

	public int CurrentLineIndex => currentLineIndex;

	public bool IsJobEnd => currentLineIndex >= Lines.Count;

	public void MoveToLextLine() => ++currentLineIndex;
}
