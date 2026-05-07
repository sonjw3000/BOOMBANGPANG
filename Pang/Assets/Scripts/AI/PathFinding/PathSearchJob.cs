using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;


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
	public int ParentIndex;     // state index of parent node
	public int GCost;
	public int HCost;

	public int HeapIndex;       // self index in heap
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
	public readonly FindRoute target;
	public readonly int3 startPosition;
	public readonly int3 endPosition;
	public readonly FacingDirection startFacingDirection;

	public readonly int MovementCost;
	public readonly int RotationCost;

	public readonly FindRoute AvoidTarget = null;
	public bool IsSubPathRequest => AvoidTarget != null;

	public PathRequest(FindRoute target, int3 startPosition, int3 endPosition, FacingDirection startFacingDirection, FindRoute avoidTarget = null)
	{
		this.target = target;
		this.startPosition = startPosition;
		this.endPosition = endPosition;
		this.startFacingDirection = startFacingDirection;
		this.AvoidTarget = avoidTarget;

		MovementCost = 1;
		RotationCost = 2;

		//MovementCost = (int)((1.0f / target.GetMovementSpeed()) * 100.0f);
		//RotationCost = (int)((90.0f / target.GetRotationSpeed()) * 100.0f);
	}

	public static LocalGrid BuildLocalGrid(in int3 start, in int3 end, int margin)
	{
		int3 min = new(
			math.min(start.x, end.x) - margin,
			math.min(start.y, end.y) - margin,
			math.min(start.z, end.z) - margin
		);

		int3 max = new(
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

			CheckNode(befG, currentPosition + currentDirection.ForwardDirection(), currentDirection, 0);
			CheckNode(befG, currentPosition + currentDirection.LeftDirection(), currentDirection.TurnLeft(), 1);
			CheckNode(befG, currentPosition + currentDirection.RightDirection(), currentDirection.TurnRight(), 1);
			CheckNode(befG, currentPosition + currentDirection.BackwardDirection(), currentDirection.TurnAround(), 2);
		}

		if (buffer.OpenSet.Count == 0)
			return true;

		return false;
	}

	private void CheckNode(int befG, int3 pos, FacingDirection dir, int rotationAmount)
	{
		if (GridService.IsBlocked(pos))
		{
			// SubPath 요청인 경우, 최종 목적지(endPosition)가 점유(예약)되어 있더라도 탐색을 허용합니다.
			// 우회 경로의 목적지는 원래 유효했던 경로의 노드이므로 정적 장애물일 가능성이 없으며,
			// 타일 예약으로 인해 경로 탐색이 실패하여 무한 루프에 빠지는 것을 방지하기 위한 조치입니다.
			if (!(request.IsSubPathRequest && math.all(pos == request.endPosition)))
				return;
		}

		if (request.IsSubPathRequest)
		{
			var reservedRoute = GridService.GetReservedFindRoute(pos);
			if (reservedRoute != null && request.target.BlockingRoutes.Contains(reservedRoute))
				return;
		}

		int stateIndex = buffer.GetStateIndex(pos, dir);
		ref PathNodeRecord nodeRecord = ref buffer.GetStateRecordByStateIndex(stateIndex);

		if (nodeRecord.VisitState == NodeVisitedState.Closed)
			return;

		int G = GetG(pos, request.endPosition, rotationAmount) + befG;
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

	private int GetG(in int3 node, in int3 goal, int rotationAmount)
	{
		// 모든 노드의 이동 비용은 동일하나 rotation시간의 뭐시기가 더 들어감
		int distanceCost = request.MovementCost;
		int rotationCost = rotationAmount * request.RotationCost;
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
			// Debug.LogWarning("Failed to build path result: No valid goal state found in buffer.");
			return new PathResultBuffer(new LinkedList<PathNode>(), request.target, request.AvoidTarget);
		}

		LinkedList<PathNode> path = new();

		var nodeRecord = buffer.GetStateRecordByStateIndex(index);
		while (true)
		{
			PathNode node = PathResultBuffer.GetNewNode(buffer.GetPosition(index), buffer.GetFacingDirection(index));
			path.AddFirst(node);

			if (nodeRecord.ParentIndex == -1)
				break;

			index = nodeRecord.ParentIndex;
			nodeRecord = buffer.GetStateRecordByStateIndex(index);
		}

		var result = new PathResultBuffer(path, request.target, request.AvoidTarget);
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
	// statics
	private static ItemPool<PathNode> resultPool;
	private static PathNode GetItem() => resultPool.Get();
	public static void InitializePool(int capacity) => resultPool = new ItemPool<PathNode>(capacity, () => { return new(); });

	private readonly FindRoute target = null;
	private readonly FindRoute toAvoid = null;
	private LinkedListNode<PathNode> currentNode = null;

	private PathResultBuffer parentBuffer = null;
	private PathResultBuffer subPathResult = null;

	private LinkedList<PathNode> path = new();
	public int CurrentIndex = 0;

	static public PathNode GetNewNode(in int3 position, FacingDirection direction)
	{
		var node = GetItem();
		node.Position = position;
		node.Direction = direction;
		return node;
	}

	public PathResultBuffer(LinkedList<PathNode> path, FindRoute target, FindRoute toAvoid = null)
	{
		this.path = path;
		this.target = target;
		this.toAvoid = toAvoid;

		currentNode = path.First;
	}

	public LinkedList<PathNode> Path => path;
	public bool IsGoalReached => CurrentIndex >= path.Count;
	public LinkedListNode<PathNode> CurrentLinkedListNode
	{
		get
		{
			if (IsGoalReached)
				return null;

			var leaf = FindLeafBuffer(this);

			return leaf.currentNode;
		}
	}
	public PathNode CurrentNode => CurrentLinkedListNode?.Value;
	public PathNode NextNode
	{
		get
		{
			PathResultBuffer leaf = FindLeafBuffer(this);

			if (leaf.currentNode.Next != null)
				return leaf.currentNode.Next.Value;

			if (leaf.parentBuffer != null && leaf.parentBuffer.currentNode.Next != null)
				return leaf.parentBuffer.currentNode.Next.Value;

			return null;
		}
	}
	public PathResultBuffer ParentBuffer => parentBuffer;
	public PathResultBuffer SubPathResult
	{
		get => subPathResult;
		set
		{
			if (subPathResult != null && value == null)
			{
				subPathResult.Clear();
				subPathResult.parentBuffer = null;
				subPathResult = null;
				return;
			}

			AppendSubPath(value);
		}
	}

	static public PathResultBuffer FindLeafBuffer(PathResultBuffer buffer)
	{
		PathResultBuffer leaf = buffer;

		while (leaf.subPathResult != null)
		{
			leaf = leaf.subPathResult;
		}

		return leaf;
	}

	private void AppendSubPath(PathResultBuffer subPath)
	{
		PathResultBuffer leaf = FindLeafBuffer(this);

		leaf.subPathResult = subPath;
		subPath.parentBuffer = leaf;

		// subPath의 노드와 본인의 노드가 교차되는 부분을 찾아 교차점까지만 경로를 설정 후 제거
		Dictionary<int3, bool> pathSet = new();

		for (var node = currentNode?.Next; node != null; node = node.Next)
		{
			pathSet[node.Value.Position] = true;
		}

		for (var node = subPath.path.First; node != subPath.path.Last; node = node.Next)
		{
			if (pathSet.ContainsKey(node.Value.Position) == false)
				continue;

			// 이후의 노드들은 제거
			for (var toRemove = node.Next; toRemove != null;)
			{
				var next = toRemove.Next;
				resultPool.Release(toRemove.Value);
				subPath.path.Remove(toRemove);
				toRemove = next;
			}
			break;
		}
	}

	public void MoveToNextNode()
	{
		if (IsGoalReached)
			return;

		PathResultBuffer leaf = FindLeafBuffer(this);

		while (true)
		{
			var parent = leaf.parentBuffer;

			leaf.currentNode = leaf.currentNode.Next;
			++leaf.CurrentIndex;

			if (leaf.IsGoalReached == false || parent == null)
				break;

			parent.SubPathResult = null;

			leaf = parent;
		}
	}

	//public void AddNode()
	//{
	//	path.AddFirst(node);
	//}

	public void Clear()
	{
		foreach (var node in path)
		{
			resultPool.Release(node);
		}
		path.Clear();

		if (toAvoid != null)
			target.RemoveBlocked(toAvoid);

		CurrentIndex = 0;
		currentNode = null;
	}
}

