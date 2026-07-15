using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TrafficCoordinator : MonoBehaviour
{
	[SerializeField] private float waitRetrySeconds = 5f;

	private sealed class YieldHold
	{
		public FindRoute PriorityRoute;
		public FindRoute YieldingRoute;
		public int3 YieldCell;
		public int3 OriginalCell;
		public int3 OriginalGoal;
		public float StartedAt;
		public bool ArrivedAtYieldCell;
		public bool PriorityEnteredOriginalCell;
		public bool ClearOnly;
	}

	private struct TrafficWaitEntry
	{
		public int3 DesiredCell;
		public float StartedAt;

		public TrafficWaitEntry(in int3 desiredCell, float startedAt)
		{
			DesiredCell = desiredCell;
			StartedAt = startedAt;
		}
	}

	private GameContext Ctx => GameContext.Instance;
	private GridService GridService => Ctx.GridService;
	private WorkPolicyService WorkPolicy => Ctx.WMSys.WorkPolicyService;

	private readonly Queue<FindRoute> trafficResolveQueue = new();
	private readonly HashSet<FindRoute> queuedRoutes = new();
	private readonly Dictionary<FindRoute, TrafficWaitEntry> waitingRoutes = new();
	private readonly Dictionary<FindRoute, FindRoute> clearingForRoutes = new();
	private readonly Dictionary<FindRoute, YieldHold> yieldHolds = new();
	private readonly HashSet<int3> reservedYieldCells = new();
	private readonly HashSet<GridCell> subscribedWaitCells = new();
	private readonly List<FindRoute> waitScanScratch = new();
	private readonly List<YieldHold> yieldHoldScratch = new();
	private static readonly int3[] CardinalDirections =
	{
		new(1, 0, 0),
		new(-1, 0, 0),
		new(0, 0, 1),
		new(0, 0, -1),
	};

	public bool IsWaitingForTraffic(FindRoute route)
	{
		return route != null && waitingRoutes.ContainsKey(route);
	}

	public bool IsYieldHeld(FindRoute route)
	{
		return route != null && yieldHolds.ContainsKey(route);
	}

	public bool TryGetWaitingDesiredCell(FindRoute route, out int3 desiredCell)
	{
		if (route != null && waitingRoutes.TryGetValue(route, out var entry))
		{
			desiredCell = entry.DesiredCell;
			return true;
		}

		desiredCell = default;
		return false;
	}

	public void RegisterBlocked(FindRoute route)
	{
		if (route == null)
			return;

		if (route.TryGetTrafficToCell(out var desiredCell))
		{
			RegisterWait(route, desiredCell);
		}
		else
		{
			route.SuspendForTraffic();
		}

		EnqueueResolve(route);
	}

	public void CancelRoute(FindRoute route)
	{
		if (route == null)
			return;

		UnregisterWait(route);
		queuedRoutes.Remove(route);
		clearingForRoutes.Remove(route);

		waitScanScratch.Clear();
		foreach (var pair in clearingForRoutes)
		{
			if (pair.Value == route)
				waitScanScratch.Add(pair.Key);
		}
		for (int i = 0; i < waitScanScratch.Count; ++i)
			clearingForRoutes.Remove(waitScanScratch[i]);

		yieldHoldScratch.Clear();
		foreach (var pair in yieldHolds)
		{
			YieldHold hold = pair.Value;
			if (hold != null && (hold.YieldingRoute == route || hold.PriorityRoute == route))
				yieldHoldScratch.Add(hold);
		}

		for (int i = 0; i < yieldHoldScratch.Count; ++i)
		{
			YieldHold hold = yieldHoldScratch[i];
			FindRoute yieldingRoute = hold.YieldingRoute;
			ClearYieldHold(hold);
			if (yieldingRoute != null && yieldingRoute != route)
				RequestFreshRouteOrResume(yieldingRoute);
		}
	}

	public void NotifyAvoidTargetCleared(FindRoute route, FindRoute avoidTarget)
	{
		if (route == null)
			return;

		clearingForRoutes.Remove(route);
	}

	public void NotifyYieldArrived(FindRoute route)
	{
		if (route == null || yieldHolds.TryGetValue(route, out var hold) == false)
			return;

		hold.ArrivedAtYieldCell = true;
		route.SuspendForTraffic();
	}

	public void NotifyYieldMoveFailed(FindRoute route)
	{
		if (route == null)
			return;

		if (yieldHolds.TryGetValue(route, out var hold))
		{
			Debug.LogWarning($"[TrafficCoordinator] Yield move failed. yielding={route.Worker.Name}, priority={hold.PriorityRoute?.Worker.Name}, yieldCell={hold.YieldCell}");
			ClearYieldHold(hold);
			if (hold.ClearOnly)
			{
				route.CompleteIdleYieldMove();
				return;
			}
		}

		if (route.RequestFreshRouteToCurrentGoal() == false)
		{
			route.ResumeFromTraffic();
		}
	}

	private void Update()
	{
		ProcessYieldHolds();
		EnqueueTimedOutWaits();

		while (trafficResolveQueue.Count > 0)
		{
			FindRoute route = trafficResolveQueue.Dequeue();
			queuedRoutes.Remove(route);
			ResolveRequest(route);
		}
	}

	private void EnqueueResolve(FindRoute route)
	{
		if (route == null || queuedRoutes.Add(route) == false)
			return;

		trafficResolveQueue.Enqueue(route);
	}

	private void ResolveRequest(FindRoute requestedRoute)
	{
		if (requestedRoute == null)
			return;

		if (requestedRoute.TryGetTrafficToCell(out var desiredCell) == false)
		{
			UnregisterWait(requestedRoute);
			return;
		}

		FindRoute blockedBy = GridService.GetReservedFindRoute(desiredCell);

		if (blockedBy == null)
		{
			UnregisterWait(requestedRoute);

			if (GridService.IsBlocked(desiredCell))
			{
				if (requestedRoute.RequestFreshRouteToCurrentGoal() == false)
				{
					requestedRoute.ResumeFromTraffic();
				}

				return;
			}

			requestedRoute.ResumeFromTraffic();
			return;
		}

		if (blockedBy == requestedRoute)
		{
			UnregisterWait(requestedRoute);
			requestedRoute.ResumeFromTraffic();
			return;
		}

		if (IsWaitingForTraffic(blockedBy))
		{
			ResolveBlockedByWaitingRoute(requestedRoute, blockedBy, desiredCell);
			return;
		}

		if (IsStaticOrIdleBlocker(blockedBy))
		{
			ResolveStaticBlocker(requestedRoute, blockedBy, desiredCell);
			return;
		}

		RegisterWait(requestedRoute, desiredCell);
	}

	private void ResolveStaticBlocker(FindRoute requestedRoute, FindRoute blockedBy, in int3 desiredCell)
	{
		if (IsBlockerIdle(blockedBy))
		{
			if (IsDestinationBlockedBy(requestedRoute, blockedBy))
			{
				if (TryYieldIdleBlocker(blockedBy, requestedRoute))
				{
					RegisterWait(requestedRoute, desiredCell);
					return;
				}

				RegisterWait(requestedRoute, desiredCell);
				return;
			}

			if (requestedRoute.TryGetFutureToCell(out var idleFutureCell))
			{
				UnregisterWait(requestedRoute);
				requestedRoute.RequestSubPath(idleFutureCell, blockedBy);
				return;
			}

			RegisterWait(requestedRoute, desiredCell);
			return;
		}

		if (requestedRoute.TryGetFutureToCell(out var futureCell))
		{
			UnregisterWait(requestedRoute);
			requestedRoute.RequestSubPath(futureCell, blockedBy);
			return;
		}

		RegisterWait(requestedRoute, desiredCell);
	}

	private void ResolveBlockedByWaitingRoute(FindRoute requestedRoute, FindRoute blockedBy, in int3 desiredCell)
	{
		if (TryGetWaitingDesiredCell(blockedBy, out var blockerDesiredCell) &&
			blockerDesiredCell.Equals(requestedRoute.TrafficFromCell))
		{
			ResolveHeadOnDeadlock(requestedRoute, blockedBy);
			return;
		}

		RegisterWait(requestedRoute, desiredCell);
	}

	private void ResolveHeadOnDeadlock(FindRoute routeA, FindRoute routeB)
	{
		Debug.Log($"[TrafficCoordinator] Head-on traffic block detected. A={routeA.Worker.Name}, B={routeB.Worker.Name}");

		bool routeAHasPriority = IsHigherTrafficPriority(routeA, routeB);
		FindRoute high = routeAHasPriority ? routeA : routeB;
		FindRoute low = routeAHasPriority ? routeB : routeA;
		FindRoute highOwner = GetEffectivePriorityRoute(high);

		if (low.TryGetFutureToCell(out var lowFutureCell))
		{
			UnregisterWait(low);
			RegisterClearingRoute(low, highOwner);
			low.RequestSubPath(lowFutureCell, high);
		}
		else if (TryStartOneTileYield(low, high, highOwner))
		{
		}
		else if (TryStartOneTileYield(high, low, GetEffectivePriorityRoute(low)))
		{
			if (low.TryGetTrafficToCell(out var lowDesiredCell))
			{
				RegisterWait(low, lowDesiredCell);
			}
		}
		else if (low.TryGetTrafficToCell(out var lowDesiredCell))
		{
			Debug.LogWarning($"[TrafficCoordinator] No yield space for head-on conflict. A={routeA.Worker.Name}, B={routeB.Worker.Name}");
			RegisterWait(low, lowDesiredCell);
		}

		if (high.TryGetTrafficToCell(out var remainingHighDesiredCell) && IsYieldHeld(high) == false)
		{
			RegisterWait(high, remainingHighDesiredCell);
		}
	}

	private bool IsHigherTrafficPriority(FindRoute routeA, FindRoute routeB)
	{
		bool routeAYieldHeld = IsYieldHeld(routeA);
		bool routeBYieldHeld = IsYieldHeld(routeB);

		if (routeAYieldHeld != routeBYieldHeld)
			return routeAYieldHeld;

		FindRoute routeAOwner = GetEffectivePriorityRoute(routeA);
		FindRoute routeBOwner = GetEffectivePriorityRoute(routeB);
		return WorkPolicy.IsTargetHigherPriority(routeAOwner.Worker, routeBOwner.Worker);
	}

	private FindRoute GetEffectivePriorityRoute(FindRoute route)
	{
		FindRoute current = route;
		int guard = 0;

		while (current != null && clearingForRoutes.TryGetValue(current, out var owner) && owner != null)
		{
			if (++guard > 16)
			{
				Debug.LogWarning($"[TrafficCoordinator] Clearing owner chain exceeded guard. Route={route?.name}");
				break;
			}

			current = owner;
		}

		return current != null ? current : route;
	}

	private void RegisterClearingRoute(FindRoute route, FindRoute priorityOwner)
	{
		if (route == null || priorityOwner == null || route == priorityOwner)
			return;

		clearingForRoutes[route] = priorityOwner;
	}

	private bool TryStartOneTileYield(FindRoute yieldingRoute, FindRoute priorityRoute, FindRoute priorityOwner)
	{
		if (yieldingRoute == null || priorityRoute == null || yieldHolds.ContainsKey(yieldingRoute))
			return false;

		if (yieldingRoute.TryGetCurrentGoalCell(out var originalGoal) == false)
			return false;

		if (TryFindOneTileYieldCell(yieldingRoute, priorityRoute, out var yieldCell) == false)
			return false;

		var hold = new YieldHold
		{
			PriorityRoute = priorityRoute,
			YieldingRoute = yieldingRoute,
			YieldCell = yieldCell,
			OriginalCell = yieldingRoute.TrafficFromCell,
			OriginalGoal = originalGoal,
			StartedAt = Time.time,
			ArrivedAtYieldCell = false,
			PriorityEnteredOriginalCell = false,
		};

		yieldHolds[yieldingRoute] = hold;
		reservedYieldCells.Add(yieldCell);
		RegisterClearingRoute(yieldingRoute, priorityOwner != null ? priorityOwner : priorityRoute);
		UnregisterWait(yieldingRoute);

		if (yieldingRoute.RequestYieldMove(yieldCell) == false)
		{
			ClearYieldHold(hold);
			return false;
		}

		Debug.Log($"[TrafficCoordinator] Yield started. yielding={yieldingRoute.Worker.Name}, priority={priorityRoute.Worker.Name}, yieldCell={yieldCell}");
		return true;
	}

	private bool TryYieldIdleBlocker(FindRoute blocker, FindRoute requestedRoute)
	{
		if (blocker == null || requestedRoute == null || yieldHolds.ContainsKey(blocker))
			return false;

		int3 origin = blocker.TrafficFromCell;
		for (int i = 0; i < CardinalDirections.Length; ++i)
		{
			int3 yieldCell = origin + CardinalDirections[i];
			if (CanUseAsIdleYieldCell(yieldCell, blocker, requestedRoute) == false)
				continue;

			var hold = new YieldHold
			{
				PriorityRoute = requestedRoute,
				YieldingRoute = blocker,
				YieldCell = yieldCell,
				OriginalCell = origin,
				OriginalGoal = yieldCell,
				StartedAt = Time.time,
				ArrivedAtYieldCell = false,
				PriorityEnteredOriginalCell = false,
				ClearOnly = true,
			};

			yieldHolds[blocker] = hold;
			reservedYieldCells.Add(yieldCell);
			RegisterClearingRoute(blocker, requestedRoute);
			UnregisterWait(blocker);

			if (blocker.RequestIdleYieldMove(yieldCell))
			{
				Debug.Log($"[TrafficCoordinator] Idle blocker yield started. yielding={blocker.Worker.Name}, priority={requestedRoute.Worker.Name}, yieldCell={yieldCell}");
				return true;
			}

			ClearYieldHold(hold);
		}

		return false;
	}

	private bool CanUseAsIdleYieldCell(in int3 candidate, FindRoute blocker, FindRoute requestedRoute)
	{
		GridCell cell = GridService.GetCell(candidate);
		if (cell == null)
			return false;

		if (GridService.IsBlocked(candidate))
			return false;

		if (GridService.GetReservedFindRoute(candidate) != null)
			return false;

		if (reservedYieldCells.Contains(candidate))
			return false;

		if (candidate.Equals(blocker.TrafficFromCell))
			return false;

		if (candidate.Equals(requestedRoute.TrafficFromCell))
			return false;

		if (requestedRoute.TryGetTrafficToCell(out var requestedToCell) && candidate.Equals(requestedToCell))
			return false;

		return true;
	}

	private bool TryFindOneTileYieldCell(FindRoute yieldingRoute, FindRoute priorityRoute, out int3 yieldCell)
	{
		yieldCell = default;

		int3 directionAway = yieldingRoute.TrafficFromCell - priorityRoute.TrafficFromCell;
		int manhattan = math.abs(directionAway.x) + math.abs(directionAway.y) + math.abs(directionAway.z);
		if (manhattan != 1)
			return false;

		int3 candidate = yieldingRoute.TrafficFromCell + directionAway;
		GridCell cell = GridService.GetCell(candidate);
		if (cell == null)
			return false;

		if (GridService.IsBlocked(candidate))
			return false;

		if (GridService.GetReservedFindRoute(candidate) != null)
			return false;

		if (reservedYieldCells.Contains(candidate))
			return false;

		if (candidate.Equals(priorityRoute.TrafficFromCell))
			return false;

		if (priorityRoute.TryGetTrafficToCell(out var priorityToCell) && candidate.Equals(priorityToCell))
			return false;

		yieldCell = candidate;
		return true;
	}

	private void ProcessYieldHolds()
	{
		if (yieldHolds.Count == 0)
			return;

		yieldHoldScratch.Clear();
		foreach (var pair in yieldHolds)
		{
			yieldHoldScratch.Add(pair.Value);
		}

		for (int i = 0; i < yieldHoldScratch.Count; ++i)
		{
			ProcessYieldHold(yieldHoldScratch[i]);
		}
	}

	private void ProcessYieldHold(YieldHold hold)
	{
		if (hold == null || hold.YieldingRoute == null || hold.PriorityRoute == null)
			return;

		if (TryGetInvalidYieldHoldReason(hold, out string invalidReason))
		{
			RecoverInvalidYieldHold(hold, invalidReason);
			return;
		}

		if (hold.ArrivedAtYieldCell == false)
			return;

		if (hold.ClearOnly)
		{
			ReleaseYieldHold(hold);
			return;
		}

		FindRoute originalCellReservedBy = GridService.GetReservedFindRoute(hold.OriginalCell);
		if (originalCellReservedBy == hold.PriorityRoute)
		{
			hold.PriorityEnteredOriginalCell = true;
			return;
		}

		if (hold.PriorityEnteredOriginalCell && originalCellReservedBy == null)
		{
			ReleaseYieldHold(hold);
		}
	}

	private bool TryGetInvalidYieldHoldReason(YieldHold hold, out string reason)
	{
		if (hold.PriorityRoute.TryGetTrafficToCell(out var priorityToCell) && hold.YieldCell.Equals(priorityToCell))
		{
			reason = "yield cell is priority target cell";
			return true;
		}

		reason = null;
		return false;
	}

	private void RecoverInvalidYieldHold(YieldHold hold, string reason)
	{
		FindRoute yieldingRoute = hold.YieldingRoute;
		FindRoute priorityRoute = hold.PriorityRoute;
		Debug.LogWarning(
			$"[TrafficCoordinator] Invalid yield hold cleared. reason={reason}, " +
			$"yielding={yieldingRoute.Worker.Name}, priority={priorityRoute.Worker.Name}, yieldCell={hold.YieldCell}");

		ClearYieldHold(hold);
		if (hold.ClearOnly)
		{
			yieldingRoute.CompleteIdleYieldMove();
			RequestFreshRouteOrResume(priorityRoute);
			return;
		}

		RequestFreshRouteOrResume(yieldingRoute);
		RequestFreshRouteOrResume(priorityRoute);
	}

	private void ReleaseYieldHold(YieldHold hold)
	{
		if (hold == null)
			return;

		FindRoute yieldingRoute = hold.YieldingRoute;
		Debug.Log($"[TrafficCoordinator] Yield released. yielding={yieldingRoute.Worker.Name}, originalGoal={hold.OriginalGoal}");
		ClearYieldHold(hold);

		if (hold.ClearOnly)
		{
			yieldingRoute.CompleteIdleYieldMove();
			return;
		}

		RequestFreshRouteOrResume(yieldingRoute);
	}

	private void RequestFreshRouteOrResume(FindRoute route)
	{
		if (route == null)
			return;

		if (route.RequestFreshRouteToCurrentGoal() == false)
		{
			route.ResumeFromTraffic();
		}
	}

	private void ClearYieldHold(YieldHold hold)
	{
		if (hold == null)
			return;

		if (hold.YieldingRoute != null)
		{
			yieldHolds.Remove(hold.YieldingRoute);
			clearingForRoutes.Remove(hold.YieldingRoute);
		}

		reservedYieldCells.Remove(hold.YieldCell);
	}

	private bool IsStaticOrIdleBlocker(FindRoute blocker)
	{
		if (blocker == null)
			return false;

		if (IsYieldHeld(blocker))
			return false;

		if (blocker.TryGetTrafficToCell(out _) == false)
			return true;

		if (blocker.enabled == false && IsWaitingForTraffic(blocker) == false && IsYieldHeld(blocker) == false)
			return true;

		return blocker.CurrentMovementState == FindRoute.MovementState.Idle ||
			blocker.CurrentMovementState == FindRoute.MovementState.Arrived ||
			blocker.CurrentMovementState == FindRoute.MovementState.Failed;
	}

	private bool IsBlockerIdle(FindRoute blocker)
	{
		AIWorker worker = blocker?.Worker;
		if (worker == null)
			return false;

		if (worker.CurrentTask == null)
			return true;

		WorkerStatusAction action = worker.EffectiveStatusAction;
		return action == WorkerStatusAction.Idle ||
			action == WorkerStatusAction.WaitingForItems ||
			action == WorkerStatusAction.WaitingForTargetBuilding;
	}

	private bool IsDestinationBlockedBy(FindRoute requestedRoute, FindRoute blockedBy)
	{
		if (requestedRoute == null || blockedBy == null)
			return false;

		if (requestedRoute.TryGetCurrentGoalCell(out var destination) == false)
			return false;

		return destination.Equals(blockedBy.TrafficFromCell);
	}

	private void RegisterWait(FindRoute route, in int3 desiredCell)
	{
		if (route == null)
			return;

		route.SuspendForTraffic();
		waitingRoutes[route] = new TrafficWaitEntry(desiredCell, Time.time);

		GridCell cell = GridService.GetCell(desiredCell);
		if (cell != null && subscribedWaitCells.Add(cell))
		{
			cell.OnGridUnReserved += OnWaitCellUnreserved;
		}
	}

	private void UnregisterWait(FindRoute route)
	{
		if (route == null || waitingRoutes.Remove(route) == false)
			return;

		route.ClearTrafficBlockState();
		CleanupUnusedWaitCellSubscriptions();
	}

	private void OnWaitCellUnreserved(GridCell cell)
	{
		waitScanScratch.Clear();

		foreach (var pair in waitingRoutes)
		{
			if (GridService.GetCell(pair.Value.DesiredCell) == cell)
			{
				waitScanScratch.Add(pair.Key);
			}
		}

		for (int i = 0; i < waitScanScratch.Count; ++i)
		{
			EnqueueResolve(waitScanScratch[i]);
		}
	}

	private void EnqueueTimedOutWaits()
	{
		if (waitingRoutes.Count == 0)
			return;

		waitScanScratch.Clear();
		float now = Time.time;

		foreach (var pair in waitingRoutes)
		{
			if (now - pair.Value.StartedAt >= waitRetrySeconds)
			{
				waitScanScratch.Add(pair.Key);
			}
		}

		for (int i = 0; i < waitScanScratch.Count; ++i)
		{
			EnqueueResolve(waitScanScratch[i]);
		}
	}

	private void CleanupUnusedWaitCellSubscriptions()
	{
		waitScanScratch.Clear();

		foreach (GridCell cell in subscribedWaitCells)
		{
			bool stillUsed = false;
			foreach (var pair in waitingRoutes)
			{
				if (GridService.GetCell(pair.Value.DesiredCell) == cell)
				{
					stillUsed = true;
					break;
				}
			}

			if (stillUsed == false)
			{
				cell.OnGridUnReserved -= OnWaitCellUnreserved;
				waitScanScratch.Add(null);
			}
		}

		if (waitScanScratch.Count == 0)
			return;

		subscribedWaitCells.RemoveWhere(cell =>
		{
			foreach (var pair in waitingRoutes)
			{
				if (GridService.GetCell(pair.Value.DesiredCell) == cell)
					return false;
			}

			return true;
		});
	}

	private void OnDestroy()
	{
		foreach (GridCell cell in subscribedWaitCells)
		{
			if (cell != null)
			{
				cell.OnGridUnReserved -= OnWaitCellUnreserved;
			}
		}

		subscribedWaitCells.Clear();
		waitingRoutes.Clear();
		clearingForRoutes.Clear();
		yieldHolds.Clear();
		reservedYieldCells.Clear();
		queuedRoutes.Clear();
		trafficResolveQueue.Clear();
	}
}
