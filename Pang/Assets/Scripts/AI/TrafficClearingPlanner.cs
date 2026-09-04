using System;
using System.Collections.Generic;
using Unity.Mathematics;

internal readonly struct TrafficClearingMove
{
	public readonly FindRoute Route;
	public readonly int3 FromCell;
	public readonly int3 ToCell;

	public TrafficClearingMove(FindRoute route, in int3 fromCell, in int3 toCell)
	{
		Route = route;
		FromCell = fromCell;
		ToCell = toCell;
	}
}

internal sealed class TrafficClearingPlanDefinition
{
	public FindRoute PriorityOwner;
	public FindRoute PassingRoute;
	public readonly List<FindRoute> Participants = new();
	public readonly List<TrafficClearingMove> Moves = new();
	public readonly HashSet<int3> ProtectedCells = new();
	public readonly HashSet<int3> ReservedCells = new();
	public int3 ReleaseCell;
}

internal static class TrafficClearingPlanner
{
	// Joint positions grow combinatorially even with a short move limit.
	private const int MaxSearchStates = 4096;
	private sealed class SearchNode
	{
		public int3[] Positions;
		public SearchNode Parent;
		public int MovedIndex = -1;
		public int3 FromCell;
		public int3 ToCell;
		public int Depth;
	}

	private readonly struct StateKey : IEquatable<StateKey>
	{
		private readonly int count;
		private readonly int3 first;
		private readonly int3 second;
		private readonly int3 third;
		private readonly int3 fourth;

		public StateKey(int3[] positions)
		{
			count = positions?.Length ?? 0;
			first = count > 0 ? positions[0] : default;
			second = count > 1 ? positions[1] : default;
			third = count > 2 ? positions[2] : default;
			fourth = count > 3 ? positions[3] : default;
		}

		public bool Equals(StateKey other) =>
			count == other.count &&
			first.Equals(other.first) &&
			second.Equals(other.second) &&
			third.Equals(other.third) &&
			fourth.Equals(other.fourth);

		public override bool Equals(object obj) => obj is StateKey other && Equals(other);

		public override int GetHashCode()
		{
			HashCode hash = new();
			hash.Add(count);
			hash.Add(first);
			hash.Add(second);
			hash.Add(third);
			hash.Add(fourth);
			return hash.ToHashCode();
		}
	}

	private static readonly int3[] CardinalDirections =
	{
		new(1, 0, 0),
		new(-1, 0, 0),
		new(0, 0, 1),
		new(0, 0, -1),
	};

	public static bool TryBuild(
		GridService grid,
		ISet<int3> unavailableCells,
		FindRoute passingRoute,
		FindRoute firstBlocker,
		FindRoute priorityOwner,
		Func<FindRoute, bool> canMove,
		int maxParticipants,
		int searchRadius,
		int maxMoves,
		int protectedCellCount,
		out TrafficClearingPlanDefinition definition)
	{
		definition = null;
		if (grid == null || passingRoute == null || firstBlocker == null || priorityOwner == null || canMove == null)
			return false;
		if (maxParticipants <= 0 || maxParticipants > 4 || searchRadius <= 0 || maxMoves <= 0 || protectedCellCount <= 1)
			return false;

		int3 directionAway = firstBlocker.TrafficFromCell - passingRoute.TrafficFromCell;
		int manhattan = math.abs(directionAway.x) + math.abs(directionAway.y) + math.abs(directionAway.z);
		if (manhattan != 1)
			return false;

		List<int3> protectedPath = new(protectedCellCount);
		passingRoute.CollectUpcomingTrafficCells(protectedPath, protectedCellCount);
		if (protectedPath.Count <= 1)
			return false;

		TrafficClearingPlanDefinition candidate = new()
		{
			PriorityOwner = priorityOwner,
			PassingRoute = passingRoute,
		};
		for (int i = 0; i < protectedPath.Count; ++i)
			candidate.ProtectedCells.Add(protectedPath[i]);

		HashSet<FindRoute> participantSet = new();
		if (TryAddParticipant(firstBlocker, passingRoute, priorityOwner, canMove, candidate.Participants, participantSet) == false)
			return false;

		int3 cursor = firstBlocker.TrafficFromCell + directionAway;
		while (candidate.Participants.Count < maxParticipants)
		{
			FindRoute blocker = grid.GetBlockingFindRoute(cursor);
			if (blocker == null)
				break;
			if (TryAddParticipant(blocker, passingRoute, priorityOwner, canMove, candidate.Participants, participantSet) == false)
				return false;

			cursor += directionAway;
		}

		int3[] initialPositions = new int3[candidate.Participants.Count];
		HashSet<int3> originalCells = new();
		for (int i = 0; i < candidate.Participants.Count; ++i)
		{
			initialPositions[i] = candidate.Participants[i].TrafficFromCell;
			originalCells.Add(initialPositions[i]);
			candidate.ReservedCells.Add(initialPositions[i]);
		}

		int lastOriginalIndex = -1;
		for (int i = 0; i < protectedPath.Count; ++i)
		{
			if (originalCells.Contains(protectedPath[i]))
				lastOriginalIndex = i;
		}
		if (lastOriginalIndex < 0 || lastOriginalIndex + 1 >= protectedPath.Count)
			return false;
		candidate.ReleaseCell = protectedPath[lastOriginalIndex + 1];
		for (int i = 0; i <= lastOriginalIndex + 1; ++i)
		{
			int3 cell = protectedPath[i];
			FindRoute blocker = grid.GetBlockingFindRoute(cell);
			if ((blocker != null && blocker != passingRoute && participantSet.Contains(blocker) == false) ||
				(unavailableCells != null && unavailableCells.Contains(cell)) || grid.IsBlocked(cell))
			{
				return false;
			}
			candidate.ReservedCells.Add(cell);
		}

		SearchNode goal = FindPlan(
			grid,
			unavailableCells,
			passingRoute,
			priorityOwner,
			candidate.Participants,
			participantSet,
			candidate.ProtectedCells,
			initialPositions,
			firstBlocker.TrafficFromCell,
			searchRadius,
			maxMoves);
		if (goal == null)
			return false;

		Stack<TrafficClearingMove> reversedMoves = new();
		for (SearchNode node = goal; node?.Parent != null; node = node.Parent)
		{
			reversedMoves.Push(new TrafficClearingMove(
				candidate.Participants[node.MovedIndex],
				node.FromCell,
				node.ToCell));
		}
		while (reversedMoves.Count > 0)
		{
			TrafficClearingMove move = reversedMoves.Pop();
			candidate.Moves.Add(move);
			candidate.ReservedCells.Add(move.ToCell);
		}

		if (candidate.Moves.Count == 0)
			return false;

		definition = candidate;
		return true;
	}

	private static bool TryAddParticipant(
		FindRoute route,
		FindRoute passingRoute,
		FindRoute priorityOwner,
		Func<FindRoute, bool> canMove,
		List<FindRoute> participants,
		HashSet<FindRoute> participantSet)
	{
		if (route == null || route == passingRoute || route == priorityOwner || participantSet.Contains(route))
			return false;
		if (canMove(route) == false)
			return false;

		participantSet.Add(route);
		participants.Add(route);
		return true;
	}

	private static SearchNode FindPlan(
		GridService grid,
		ISet<int3> unavailableCells,
		FindRoute passingRoute,
		FindRoute priorityOwner,
		IReadOnlyList<FindRoute> participants,
		HashSet<FindRoute> participantSet,
		HashSet<int3> protectedCells,
		int3[] initialPositions,
		in int3 searchOrigin,
		int searchRadius,
		int maxMoves)
	{
		Queue<SearchNode> open = new();
		HashSet<StateKey> visited = new();
		SearchNode start = new() { Positions = initialPositions, Depth = 0 };
		open.Enqueue(start);
		visited.Add(new StateKey(initialPositions));

		while (open.Count > 0)
		{
			SearchNode current = open.Dequeue();
			if (AreAllParticipantsClear(current.Positions, protectedCells))
				return current;
			if (current.Depth >= maxMoves)
				continue;

			for (int participantIndex = participants.Count - 1; participantIndex >= 0; --participantIndex)
			{
				int3 from = current.Positions[participantIndex];
				for (int directionIndex = 0; directionIndex < CardinalDirections.Length; ++directionIndex)
				{
					int3 to = from + CardinalDirections[directionIndex];
					if (CanUseStep(
						grid,
						unavailableCells,
						passingRoute,
						priorityOwner,
						participants[participantIndex],
						participantSet,
						current.Positions,
						participantIndex,
						from,
						to,
						searchOrigin,
						searchRadius) == false)
					{
						continue;
					}

					int3[] nextPositions = (int3[])current.Positions.Clone();
					nextPositions[participantIndex] = to;
					StateKey key = new(nextPositions);
					if (visited.Add(key) == false)
						continue;
					if (visited.Count > MaxSearchStates)
						return null;

					open.Enqueue(new SearchNode
					{
						Positions = nextPositions,
						Parent = current,
						MovedIndex = participantIndex,
						FromCell = from,
						ToCell = to,
						Depth = current.Depth + 1,
					});
				}
			}
		}

		return null;
	}

	private static bool AreAllParticipantsClear(int3[] positions, HashSet<int3> protectedCells)
	{
		for (int i = 0; i < positions.Length; ++i)
		{
			if (protectedCells.Contains(positions[i]))
				return false;
		}

		return true;
	}

	private static bool CanUseStep(
		GridService grid,
		ISet<int3> unavailableCells,
		FindRoute passingRoute,
		FindRoute priorityOwner,
		FindRoute movingRoute,
		HashSet<FindRoute> participantSet,
		int3[] positions,
		int movingIndex,
		in int3 from,
		in int3 to,
		in int3 searchOrigin,
		int searchRadius)
	{
		int distanceFromOrigin = math.abs(to.x - searchOrigin.x) + math.abs(to.y - searchOrigin.y) + math.abs(to.z - searchOrigin.z);
		if (distanceFromOrigin > searchRadius)
			return false;
		if (grid.GetCell(to) == null || grid.IsBlocked(to) || grid.IsSameRegion(from, to) == false)
			return false;
		if (movingRoute.CanTraverseTrafficCell(to) == false)
			return false;
		if (unavailableCells != null && unavailableCells.Contains(to))
			return false;
		if (to.Equals(passingRoute.TrafficFromCell) || to.Equals(priorityOwner.TrafficFromCell))
			return false;

		for (int i = 0; i < positions.Length; ++i)
		{
			if (i != movingIndex && positions[i].Equals(to))
				return false;
		}

		GridCell cell = grid.GetCell(to);
		FindRoute occupyingRoute = cell.OccupancyWorker?.RouteFinder;
		if (occupyingRoute != null && participantSet.Contains(occupyingRoute) == false)
			return false;

		FindRoute reservedRoute = grid.GetReservedFindRoute(to);
		if (reservedRoute != null && participantSet.Contains(reservedRoute) == false)
			return false;

		return true;
	}
}
