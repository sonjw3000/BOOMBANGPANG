using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TrafficCoordinator : MonoBehaviour
{
	[SerializeField] private float waitRetrySeconds = 5f;

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
	private readonly HashSet<GridCell> subscribedWaitCells = new();
	private readonly List<FindRoute> waitScanScratch = new();

	public bool IsWaitingForTraffic(FindRoute route)
	{
		return route != null && waitingRoutes.ContainsKey(route);
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

	public void NotifyAvoidTargetCleared(FindRoute route, FindRoute avoidTarget)
	{
		if (route == null)
			return;

		clearingForRoutes.Remove(route);
	}

	private void Update()
	{
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

		FindRoute routeAOwner = GetEffectivePriorityRoute(routeA);
		FindRoute routeBOwner = GetEffectivePriorityRoute(routeB);
		bool routeAHasPriority = WorkPolicy.IsTargetHigherPriority(routeAOwner.Worker, routeBOwner.Worker);
		FindRoute high = routeAHasPriority ? routeA : routeB;
		FindRoute low = routeAHasPriority ? routeB : routeA;
		FindRoute highOwner = routeAHasPriority ? routeAOwner : routeBOwner;

		if (low.TryGetFutureToCell(out var lowFutureCell))
		{
			UnregisterWait(low);
			RegisterClearingRoute(low, highOwner);
			low.RequestSubPath(lowFutureCell, high);
		}
		else if (low.TryGetTrafficToCell(out var lowDesiredCell))
		{
			RegisterWait(low, lowDesiredCell);
		}

		if (high.TryGetTrafficToCell(out var highDesiredCell))
		{
			RegisterWait(high, highDesiredCell);
		}
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

	private bool IsStaticOrIdleBlocker(FindRoute blocker)
	{
		if (blocker == null)
			return false;

		if (blocker.TryGetTrafficToCell(out _) == false)
			return true;

		if (blocker.enabled == false && IsWaitingForTraffic(blocker) == false)
			return true;

		return blocker.CurrentMovementState == FindRoute.MovementState.Idle ||
			blocker.CurrentMovementState == FindRoute.MovementState.Arrived ||
			blocker.CurrentMovementState == FindRoute.MovementState.Failed;
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
		queuedRoutes.Clear();
		trafficResolveQueue.Clear();
	}
}
