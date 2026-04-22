using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEditorInternal.Profiling.Memory.Experimental;
using System.Linq;


public struct LocalGrid
{
	public int3 Min;
	public int3 Max;
}

public enum NodeVisitedState : byte
{
	None,
	Open,
	Closed,
}

public class PathNode
{
	public int3 Position;
	public FacingDirection Direction;
}

public struct PathNodeRecord
{
	public int ParentIndex;		// state index of parent node
	public int GCost;
	public int HCost;

	public int HeapIndex;		// self index in heap
	public NodeVisitedState VisitState;

	public readonly int FCost => GCost + HCost;

	public static int CompareByFCost(in PathNodeRecord a, in PathNodeRecord b)
	{
		int fCostComparison = a.FCost.CompareTo(b.FCost);
		return fCostComparison;
	}

	public static PathNodeRecord CreateDefault()
	{
		return new PathNodeRecord
		{
			ParentIndex = -1,
			GCost = int.MaxValue,
			HCost = 0,
			HeapIndex = -1,
			VisitState = NodeVisitedState.None
		};
	}

	public void Reset()
	{
		ParentIndex = -1;
		GCost = int.MaxValue;
		HCost = 0;
		HeapIndex = -1;
		VisitState = NodeVisitedState.None;
	}
}

public class PathRequest
{
	public FindRoute target;
	public int3 startPosition;
	public int3 endPosition;
	public FacingDirection startFacingDirection;

	public readonly int MovementCost;
	public readonly int RotationCost;

	public PathRequest(FindRoute target, int3 startPosition, int3 endPosition, FacingDirection startFacingDirection)
	{
		this.target = target;
		this.startPosition = startPosition;
		this.endPosition = endPosition;
		this.startFacingDirection = startFacingDirection;


		MovementCost = 1;
		RotationCost = 2;

		//MovementCost = (int)((1.0f / target.GetMovementSpeed()) * 100.0f);
		//RotationCost = (int)((90.0f / target.GetRotationSpeed()) * 100.0f);
	}

	public static LocalGrid BuildLocalGrid(in int3 start, in int3 end, int margin)
	{
		int3 min = new int3(
			math.min(start.x, end.x) - margin,
			math.min(start.y, end.y) - margin,
			math.min(start.z, end.z) - margin
		);

		int3 max = new int3(
			math.max(start.x, end.x) + margin,
			math.max(start.y, end.y) + margin,
			math.max(start.z, end.z) + margin
		);

		return new LocalGrid
		{
			Min = min,
			Max = max,
		};
	}

	public static int GetStateSize(in LocalGrid grid)
	{
		int xLength = grid.Max.x - grid.Min.x + 1;
		int yLength = grid.Max.y - grid.Min.y + 1;
		int zLength = grid.Max.z - grid.Min.z + 1;

		return xLength * yLength * zLength;
	}
}

public class SearchBuffer
{
	// todo
	// 해당 state index는 LocalGrid 기준으로 계산 되어야함
	private PathNodeRecord[] RecordsStates;
	//public LocalGrid LocalMap;

	// open set은 stateIndex를 담고있음
	public readonly ArrayHeap<int> OpenSet;

	public int3 mapSize;

	public static int CompareFromHeap(int a, int b, IReadOnlyList<PathNodeRecord> records)
	{
		return PathNodeRecord.CompareByFCost(records[a], records[b]);
	}

	public SearchBuffer(in int3 mapSize)
	{
		this.mapSize = mapSize;
		int size = mapSize.x * mapSize.y * mapSize.z * Enum.GetValues(typeof(FacingDirection)).Length;

		RecordsStates = new PathNodeRecord[size];
		OpenSet = new ArrayHeap<int>((a, b) => CompareFromHeap(a, b, RecordsStates), (index, newIndex) => RecordsStates[index].HeapIndex = newIndex);

		for (int i = 0; i < size; ++i)
		{
			RecordsStates[i] = PathNodeRecord.CreateDefault();
		}
	}

	public void ResetBuffer()
	{
		for (int i = 0; i < RecordsStates.Length; ++i)
		{
			RecordsStates[i].Reset();
		}
		OpenSet.Reset();
	}

	public int GetStateIndex(in int3 position, in FacingDirection direction)
	{
		int xLength = mapSize.x;
		int yLength = mapSize.y;
		int zLength = mapSize.z;
		int directionIndex = (int)direction;

		return (position.x + (position.z * xLength) + (position.y * xLength * zLength)) * Enum.GetValues(typeof(FacingDirection)).Length + directionIndex;

		//int xLength = LocalMap.Max.x - LocalMap.Min.x + 1;
		//int yLength = LocalMap.Max.y - LocalMap.Min.y + 1;
		//int zLength = LocalMap.Max.z - LocalMap.Min.z + 1;
		//int localX = position.x - LocalMap.Min.x;
		//int localY = position.y - LocalMap.Min.y;
		//int localZ = position.z - LocalMap.Min.z;
		//return ((localX * yLength * zLength) + (localY * zLength) + localZ) * Enum.GetValues(typeof(FacingDirection)).Length + directionIndex;
	}

	public int3 GetPosition(int stateIndex)
	{
		int directionCount = Enum.GetValues(typeof(FacingDirection)).Length;
		int positionIndex = stateIndex / directionCount;

		int xLength = mapSize.x;
		int yLength = mapSize.y;
		int zLength = mapSize.z;

		int x = positionIndex % xLength;
		int z = (positionIndex / xLength) % zLength;
		int y = positionIndex / (xLength * zLength);
		return new int3(x, y, z);
	}

	public FacingDirection GetFacingDirection(int stateIndex)
	{
		int directionCount = Enum.GetValues(typeof(FacingDirection)).Length;
		int directionIndex = stateIndex % directionCount;
		return (FacingDirection)directionIndex;
	}

	public ref PathNodeRecord GetStateRecordByStateIndex(int stateIndex)
	{
		return ref RecordsStates[stateIndex];
	}
	//int localX
}

public sealed class PathSearchJob
{
	private GridService GridService => GameContext.Instance.GridService;

	private SearchBuffer buffer;
	private PathRequest request;

	public SearchBuffer Buffer => buffer;

	private int3 currentPosition;
	private FacingDirection currentDirection;

	//private int MoveSpeed => GameContext.Instance.WMSys.WorkPolicyService.GetMoveSpeed();;
	private const int RotationCost = 2;

	//private int RotationCost => 

	public void Setup(PathRequest request, SearchBuffer buffer)
	{
		this.request = request;
		this.buffer = buffer;

		if (request != null && buffer != null)
		{
			buffer.ResetBuffer();

			currentPosition = request.startPosition;
			currentDirection = request.startFacingDirection;

			int stateIndex = buffer.GetStateIndex(currentPosition, currentDirection);
			ref PathNodeRecord startRecord = ref buffer.GetStateRecordByStateIndex(stateIndex);
			startRecord.VisitState = NodeVisitedState.Open;
			startRecord.HCost = 0;
			startRecord.GCost = 0;

			buffer.OpenSet.Push(stateIndex);
		}
	}

	public bool Execute(int budget)
	{
		if (request == null || buffer == null)
		{
			throw new InvalidOperationException("PathSearchJob is not properly initialized.");
		}

		// todo
		// A* 알고리즘 구현
		// LocalGrid 기준으로 계산되어야함
		while (budget-- > 0 && buffer.OpenSet.Count > 0)
		{
			// do a*
			buffer.OpenSet.Pop(out int currentStateIndex);

			currentPosition = buffer.GetPosition(currentStateIndex);
			currentDirection = buffer.GetFacingDirection(currentStateIndex);

			if (math.all(currentPosition == request.endPosition))
			{
				return true;
			}

			ref PathNodeRecord currentNode = ref buffer.GetStateRecordByStateIndex(currentStateIndex);
			currentNode.VisitState = NodeVisitedState.Closed;

			int befG = currentNode.GCost;

			CheckNode(befG, currentPosition + currentDirection.ForwardDirection(), currentDirection, false);
			CheckNode(befG, currentPosition + currentDirection.LeftDirection(), currentDirection.TurnLeft(), true);
			CheckNode(befG, currentPosition + currentDirection.RightDirection(), currentDirection.TurnRight(), true);
		}

		return false;
	}

	private void CheckNode(int befG, int3 pos, FacingDirection dir, bool rotation)
	{
		if (GridService.IsBlocked(pos))
			return;

		int stateIndex = buffer.GetStateIndex(pos, dir);
		ref PathNodeRecord nodeRecord = ref buffer.GetStateRecordByStateIndex(stateIndex);

		if (nodeRecord.VisitState == NodeVisitedState.Closed)
			return;

		int G = GetG(pos, request.endPosition, rotation) + befG;
		int H = GetH(pos, request.endPosition);

		if (nodeRecord.VisitState == NodeVisitedState.None)
		{
			nodeRecord.ParentIndex = buffer.GetStateIndex(currentPosition, currentDirection);
			nodeRecord.GCost = G;
			nodeRecord.HCost = H;
			nodeRecord.VisitState = NodeVisitedState.Open;
			buffer.OpenSet.Push(stateIndex);
		}
		else if (nodeRecord.VisitState == NodeVisitedState.Open)
		{
			if (G < nodeRecord.GCost)
			{
				nodeRecord.ParentIndex = buffer.GetStateIndex(currentPosition, currentDirection);
				nodeRecord.GCost = G;
				nodeRecord.HCost = H;
				buffer.OpenSet.DecreaseKey(nodeRecord.HeapIndex);
			}
		}

	}

	private int GetG(in int3 node, in int3 goal, bool rotation)
	{
		// 모든 노드의 이동 비용은 동일하나 rotation시간의 뭐시기가 더 들어감
		int distanceCost = request.MovementCost;
		int rotationCost = rotation ? request.RotationCost : 0;

		return distanceCost + rotationCost;
	}

	private int GetH(in int3 node, in int3 goal)
	{
		// Manhattan distance + rot
		int3 abs = math.abs(node - goal);

		// todo
		// y축에 대해선 좀 다르게 해줘야함
		int distance = abs.x + abs.z;

		// todo
		// abs.x z값이 0이 아니라면 rotation cost 추가하기 + 거리에 따른 감쇠 넣기

		return distance;
	}

	private PathResultBuffer BuildResult()
	{
		int goalPos = buffer.GetStateIndex(request.endPosition, FacingDirection.North);

		int index = -1;
		for (int i = 0; i < Enum.GetValues(typeof(FacingDirection)).Length; ++i)
		{
			if (buffer.GetStateRecordByStateIndex(goalPos + i).VisitState != NodeVisitedState.None)
			{
				index = goalPos + i;
				break;
			}
		}

		if (index == -1)
		{
			Debug.LogError("Failed to build path result: No valid goal state found in buffer.");
			return null;
		}

		var result = new PathResultBuffer();

		var nodeRecord = buffer.GetStateRecordByStateIndex(index);
		while (true)
		{
			result.AddNode(buffer.GetPosition(index), buffer.GetFacingDirection(index));
			if (nodeRecord.ParentIndex == -1)
				break;

			index = nodeRecord.ParentIndex;
			nodeRecord = buffer.GetStateRecordByStateIndex(index);
		}

		return result;
	}

	public void SetPath()
	{
		var result = BuildResult();
		request.target.OnPathFound(result);
	}
}

public class PathResultBuffer
{
	static public ItemPool<PathNode> resultPool;
	public static void InitializePool(int capacity) => resultPool = new ItemPool<PathNode>(capacity, () => { return new(); });
	private static PathNode GetItem() => resultPool.Get();


	public LinkedList<PathNode> Path = new();
	public int CurrentIndex = 0;

	public bool IsGoalReached => CurrentIndex >= Path.Count;
	public PathNode CurrentNode => IsGoalReached ? null : Path.ElementAt(CurrentIndex);

	public void MoveToNextNode()
	{
		if (!IsGoalReached)
			CurrentIndex++;
	}

	public void AddNode(in int3 position, FacingDirection direction)
	{
		var node = GetItem();
		node.Position = position;
		node.Direction = direction;
		Path.AddFirst(node);
	}

	public void Clear()
	{
		foreach (var node in Path)
		{
			resultPool.Release(node);
		}
		Path.Clear();
		CurrentIndex = 0;
	}

}

