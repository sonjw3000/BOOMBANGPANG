using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TrafficCoordinator : MonoBehaviour
{
	[SerializeField] private float waitRetrySeconds = 5f;
	[SerializeField, Range(1, 4)] private int maxClearingParticipants = 4;
	[SerializeField, Min(2)] private int clearingSearchRadius = 6;
	[SerializeField, Min(2)] private int maxClearingMoves = 10;
	[SerializeField, Min(3)] private int protectedClearingCellCount = 12;
	[SerializeField, Min(1f)] private float clearingPlanTimeoutSeconds = 30f;

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

	private sealed class ClearingPlan
	{
		public TrafficClearingPlanDefinition Definition;
		public int MoveIndex;
		public FindRoute MovingRoute;
		public bool WaitingForPassage;
		public float StartedAt;
		public int PassingPathVersion;
		public readonly Dictionary<FindRoute, int> ParticipantPathVersions = new();
		public readonly HashSet<FindRoute> IdleParticipants = new();
		public string AbortAfterStepReason;
		public FindRoute CancelledRoute;
	}

	private GameContext Ctx => GameContext.Instance;
	private GridService GridService => Ctx.GridService;
	private WorkPolicyService WorkPolicy => Ctx.WMSys.WorkPolicyService;
	private ResearchService Research => Ctx.ResearchService;
	private RobotHumanCollisionService RobotHumanCollision => Ctx.RobotHumanCollisionSvc;

	private readonly Queue<FindRoute> trafficResolveQueue = new();
	private readonly HashSet<FindRoute> queuedRoutes = new();
	private readonly Dictionary<FindRoute, TrafficWaitEntry> waitingRoutes = new();
	private readonly Dictionary<FindRoute, FindRoute> clearingForRoutes = new();
	private readonly Dictionary<FindRoute, YieldHold> yieldHolds = new();
	private readonly HashSet<int3> reservedYieldCells = new();
	private readonly HashSet<ClearingPlan> clearingPlans = new();
	private readonly Dictionary<FindRoute, ClearingPlan> clearingPlansByRoute = new();
	private readonly Dictionary<int3, ClearingPlan> clearingPlansByCell = new();
	private readonly HashSet<GridCell> subscribedWaitCells = new();
	private readonly List<FindRoute> waitScanScratch = new();
	private readonly List<YieldHold> yieldHoldScratch = new();
	private readonly List<ClearingPlan> clearingPlanScratch = new();
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
		return route != null && (yieldHolds.ContainsKey(route) ||
			(clearingPlansByRoute.TryGetValue(route, out ClearingPlan plan) && plan.Definition.Participants.Contains(route)));
	}

	// Logical plan ownership supplements, but never replaces, GridService's per-step reservation.
	public bool CanReserveClearingCell(FindRoute route, in int3 cell)
	{
		if (route != null && clearingPlansByRoute.TryGetValue(route, out ClearingPlan ownPlan))
		{
			if (ownPlan.Definition.Participants.Contains(route))
			{
				return ownPlan.MovingRoute == route && ownPlan.MoveIndex < ownPlan.Definition.Moves.Count &&
					ownPlan.Definition.Moves[ownPlan.MoveIndex].ToCell.Equals(cell);
			}
			if (ownPlan.Definition.PassingRoute == route && ownPlan.WaitingForPassage == false)
				return false;
		}

		return clearingPlansByCell.TryGetValue(cell, out ClearingPlan plan) == false ||
			(plan.WaitingForPassage && plan.Definition.PassingRoute == route);
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

		if (clearingPlansByRoute.TryGetValue(route, out ClearingPlan clearingPlan) &&
			clearingPlan.MovingRoute == route)
		{
			AbortClearingPlan(clearingPlan, $"move blocked at {route.TrafficFromCell}");
			return;
		}

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

		bool cancelledIdleParticipant = false;
		if (clearingPlansByRoute.TryGetValue(route, out ClearingPlan clearingPlan))
		{
			cancelledIdleParticipant = clearingPlan.IdleParticipants.Contains(route);
			AbortClearingPlan(clearingPlan, $"route cancelled: {route.Worker?.Name}", route);
			route.ClearTrafficBlockState();
			if (cancelledIdleParticipant)
				CompleteIdleYield(route);
		}

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
			if (hold.ClearOnly)
				CompleteIdleYield(yieldingRoute);
			else if (yieldingRoute != null && yieldingRoute != route)
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
		if (route != null &&
			clearingPlansByRoute.TryGetValue(route, out ClearingPlan clearingPlan) &&
			clearingPlan.MovingRoute == route)
		{
			NotifyClearingMoveArrived(clearingPlan, route);
			return;
		}

		if (route == null || yieldHolds.TryGetValue(route, out var hold) == false)
			return;

		hold.ArrivedAtYieldCell = true;
		route.SuspendForTraffic();
	}

	public void NotifyYieldMoveFailed(FindRoute route)
	{
		if (route == null)
			return;

		if (clearingPlansByRoute.TryGetValue(route, out ClearingPlan clearingPlan) &&
			clearingPlan.MovingRoute == route)
		{
			AbortClearingPlan(clearingPlan, $"move failed: {route.Worker?.Name}");
			return;
		}

		if (yieldHolds.TryGetValue(route, out var hold))
		{
			Debug.LogWarning($"[TrafficCoordinator] Yield move failed. yielding={route.Worker.Name}, priority={hold.PriorityRoute?.Worker.Name}, yieldCell={hold.YieldCell}");
			ClearYieldHold(hold);
			if (hold.ClearOnly)
			{
				CompleteIdleYield(route);
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
		ProcessClearingPlans();
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
		if (TryHoldForActiveClearingPlan(requestedRoute))
			return;

		if (requestedRoute.TryGetTrafficToCell(out var desiredCell) == false)
		{
			UnregisterWait(requestedRoute);
			return;
		}

		if (CanReserveClearingCell(requestedRoute, desiredCell) == false)
		{
			RegisterWait(requestedRoute, desiredCell);
			return;
		}

		FindRoute blockedBy = GridService.GetBlockingFindRoute(desiredCell);

		if (blockedBy == null)
		{
			if (GridService.IsBlocked(desiredCell))
			{
				if (CanReplanAroundStaticBlocker(requestedRoute) == false)
				{
					RegisterWait(requestedRoute, desiredCell);
					return;
				}

				UnregisterWait(requestedRoute);
				if (requestedRoute.RequestFreshRouteToCurrentGoal() == false)
				{
					requestedRoute.ResumeFromTraffic();
				}

				return;
			}

			UnregisterWait(requestedRoute);
			requestedRoute.ResumeFromTraffic();
			return;
		}

		if (blockedBy == requestedRoute)
		{
			UnregisterWait(requestedRoute);
			requestedRoute.ResumeFromTraffic();
			return;
		}

		if (TryResolveRobotHumanCollision(requestedRoute, blockedBy, desiredCell))
			return;

		if (CanResolveTraffic(requestedRoute) == false)
		{
			RegisterWait(requestedRoute, desiredCell);
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

	private bool TryResolveRobotHumanCollision(
		FindRoute requestedRoute,
		FindRoute blockedBy,
		in int3 desiredCell)
	{
		if (requestedRoute?.Worker is not RobotWorker robot ||
			blockedBy?.Worker is not HumanWorker human)
		{
			return false;
		}

		if (human.IsOperational == false)
		{
			RegisterWait(requestedRoute, desiredCell, WorkerStatusAction.BlockedByCasualty);
			return true;
		}

		if (Research?.IsResearched(ResearchIds.HumanRecognition) == true)
			return false;

		RobotHumanCollisionResult result = RobotHumanCollision.TryResolve(robot, human, desiredCell);
		switch (result)
		{
			case RobotHumanCollisionResult.HumanRelocated:
				UnregisterWait(requestedRoute);
				if (robot.IsOperational)
					requestedRoute.ResumeFromTraffic();
				return true;

			case RobotHumanCollisionResult.BlockedByCasualty:
				if (robot.IsOperational)
					RegisterWait(requestedRoute, desiredCell, WorkerStatusAction.BlockedByCasualty);
				return true;

			case RobotHumanCollisionResult.DuplicateIgnored:
				if (robot.IsOperational)
					RegisterWait(requestedRoute, desiredCell);
				return true;

			default:
				return false;
		}
	}

	private void ResolveStaticBlocker(FindRoute requestedRoute, FindRoute blockedBy, in int3 desiredCell)
	{
		if (IsBlockerIdle(blockedBy))
		{
			if (CanYield(requestedRoute) &&
				CanYield(blockedBy) &&
				TryYieldIdleBlocker(blockedBy, requestedRoute))
			{
				RegisterWait(requestedRoute, desiredCell);
				return;
			}

			if (TryStartIdleClearingPlan(requestedRoute, blockedBy, requestedRoute))
				return;

			if (IsDestinationBlockedBy(requestedRoute, blockedBy) == false &&
				CanRequestDetour(requestedRoute) &&
				requestedRoute.TryGetFutureToCell(out var idleFutureCell))
			{
				UnregisterWait(requestedRoute);
				requestedRoute.RequestSubPath(idleFutureCell, blockedBy);
				return;
			}

			RegisterWait(requestedRoute, desiredCell);
			return;
		}

		if (CanRequestDetour(requestedRoute) &&
			requestedRoute.TryGetFutureToCell(out var futureCell))
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
			if (CanResolvePriority(requestedRoute))
				ResolveHeadOnDeadlock(requestedRoute, blockedBy);
			else
				RegisterWait(requestedRoute, desiredCell);
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

		if (CanRequestDetour(low) && low.TryGetFutureToCell(out var lowFutureCell))
		{
			UnregisterWait(low);
			RegisterClearingRoute(low, highOwner);
			low.RequestSubPath(lowFutureCell, high);
		}
		else if (TryStartOneTileYield(low, high, highOwner))
		{
		}
		else if (TryStartClearingPlan(high, low, highOwner))
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

	private bool TryHoldForActiveClearingPlan(FindRoute route)
	{
		if (route == null || clearingPlansByRoute.TryGetValue(route, out ClearingPlan plan) == false)
			return false;

		TrafficClearingPlanDefinition definition = plan.Definition;
		if (definition.PassingRoute == route)
		{
			if (plan.WaitingForPassage)
				return false;

			if (route.TryGetTrafficToCell(out int3 desiredCell))
				RegisterWait(route, desiredCell);
			else
				route.SuspendForTraffic();
			return true;
		}

		if (definition.Participants.Contains(route))
		{
			// An old queue entry must not suspend the step currently executing.
			if (plan.MovingRoute != route)
				route.SuspendForTraffic();
			return true;
		}

		return false;
	}

	private bool TryStartClearingPlan(FindRoute passingRoute, FindRoute firstBlocker, FindRoute priorityOwner)
	{
		return TryStartClearingPlanInternal(passingRoute, firstBlocker, priorityOwner, false);
	}

	private bool TryStartIdleClearingPlan(FindRoute passingRoute, FindRoute firstBlocker, FindRoute priorityOwner)
	{
		return TryStartClearingPlanInternal(passingRoute, firstBlocker, priorityOwner, true);
	}

	private bool TryStartClearingPlanInternal(
		FindRoute passingRoute,
		FindRoute firstBlocker,
		FindRoute priorityOwner,
		bool allowIdleParticipants)
	{
		if (passingRoute == null || firstBlocker == null || priorityOwner == null)
			return false;
		if (clearingPlansByRoute.ContainsKey(passingRoute) || clearingPlansByRoute.ContainsKey(priorityOwner))
			return false;
		if (passingRoute.CanPassTrafficClearing == false || IsYieldHeld(passingRoute) ||
			priorityOwner.Worker == null || priorityOwner.Worker.IsOperational == false)
			return false;

		System.Func<FindRoute, bool> canUseParticipant = allowIdleParticipants
			? CanUseAsIdleClearingMember
			: CanUseAsClearingMember;

		if (TrafficClearingPlanner.TryBuild(
			GridService,
			reservedYieldCells,
			passingRoute,
			firstBlocker,
			priorityOwner,
			canUseParticipant,
			Mathf.Clamp(maxClearingParticipants, 1, 4),
			Mathf.Max(2, clearingSearchRadius),
			Mathf.Max(2, maxClearingMoves),
			Mathf.Max(3, protectedClearingCellCount),
			out TrafficClearingPlanDefinition definition) == false)
		{
			return false;
		}

		for (int i = 0; i < definition.Participants.Count; ++i)
		{
			if (canUseParticipant(definition.Participants[i]) == false)
				return false;
		}
		foreach (int3 cell in definition.ReservedCells)
		{
			if (reservedYieldCells.Contains(cell))
				return false;
		}

		ClearingPlan plan = new()
		{
			Definition = definition,
			MoveIndex = 0,
			StartedAt = Time.time,
			PassingPathVersion = passingRoute.PathRequestVersion,
		};
		for (int i = 0; i < definition.Participants.Count; ++i)
		{
			FindRoute participant = definition.Participants[i];
			if (participant.CanStartIdleTrafficClearing)
				plan.IdleParticipants.Add(participant);
		}
		clearingPlans.Add(plan);
		clearingPlansByRoute[passingRoute] = plan;
		if (priorityOwner != passingRoute)
			clearingPlansByRoute[priorityOwner] = plan;

		foreach (int3 cell in definition.ReservedCells)
		{
			reservedYieldCells.Add(cell);
			clearingPlansByCell.Add(cell, plan);
		}

		for (int i = 0; i < definition.Participants.Count; ++i)
		{
			FindRoute participant = definition.Participants[i];
			clearingPlansByRoute[participant] = plan;
			plan.ParticipantPathVersions[participant] = participant.PathRequestVersion;
			UnregisterWait(participant);
			participant.SuspendForTraffic();
			participant.Worker.enabled = false;
			if (plan.IdleParticipants.Contains(participant))
				SetIdleWorkerDispatchAvailability(participant, false);
			RegisterClearingRoute(participant, priorityOwner);
		}
		if (passingRoute.TryGetTrafficToCell(out int3 desiredCell))
			RegisterWait(passingRoute, desiredCell);

		Debug.Log(
			$"[TrafficCoordinator] Clearing plan started. passing={FormatRoute(passingRoute)}, " +
			$"priority={FormatRoute(priorityOwner)}, participants={definition.Participants.Count}, " +
			$"moves={definition.Moves.Count}, releaseCell={definition.ReleaseCell}");
		StartNextClearingMove(plan);
		return true;
	}

	private bool CanUseAsClearingMember(FindRoute route)
	{
		return route != null && route.CanStartTrafficClearing && IsWaitingForTraffic(route) &&
			CanYield(route) &&
			yieldHolds.ContainsKey(route) == false &&
			clearingPlansByRoute.ContainsKey(route) == false &&
			clearingForRoutes.ContainsKey(route) == false;
	}

	private bool CanUseAsIdleClearingMember(FindRoute route)
	{
		if (route == null || CanYield(route) == false ||
			yieldHolds.ContainsKey(route) || clearingPlansByRoute.ContainsKey(route) ||
			clearingForRoutes.ContainsKey(route))
		{
			return false;
		}

		return (route.CanStartTrafficClearing && IsWaitingForTraffic(route)) ||
			route.CanStartIdleTrafficClearing;
	}

	private void StartNextClearingMove(ClearingPlan plan)
	{
		if (plan == null || clearingPlans.Contains(plan) == false)
			return;

		TrafficClearingPlanDefinition definition = plan.Definition;
		if (plan.MoveIndex >= definition.Moves.Count)
		{
			plan.MovingRoute = null;
			plan.WaitingForPassage = true;
			EnqueueResolve(definition.PassingRoute);
			Debug.Log(
				$"[TrafficCoordinator] Clearing plan opened passage. passing={FormatRoute(definition.PassingRoute)}, " +
				$"releaseCell={definition.ReleaseCell}");
			return;
		}

		TrafficClearingMove move = definition.Moves[plan.MoveIndex];
		FindRoute route = move.Route;
		if (route == null || route.TrafficFromCell.Equals(move.FromCell) == false)
		{
			AbortClearingPlan(plan, $"move source changed: {FormatRoute(route)}");
			return;
		}

		FindRoute blocker = GridService.GetBlockingFindRoute(move.ToCell);
		if (GridService.IsBlocked(move.ToCell) || (blocker != null && blocker != route))
		{
			AbortClearingPlan(plan, $"planned cell is no longer free: {move.ToCell}");
			return;
		}

		plan.MovingRoute = route;
		route.ClearTrafficBlockState();
		if (route.RequestClearingStep(move.ToCell) == false)
		{
			plan.MovingRoute = null;
			AbortClearingPlan(plan, $"move request rejected: {FormatRoute(route)} -> {move.ToCell}");
			return;
		}
		plan.ParticipantPathVersions[route] = route.PathRequestVersion;

		Debug.Log(
			$"[TrafficCoordinator] Clearing move started. route={FormatRoute(route)}, " +
			$"from={move.FromCell}, to={move.ToCell}, step={plan.MoveIndex + 1}/{definition.Moves.Count}");
	}

	private void NotifyClearingMoveArrived(ClearingPlan plan, FindRoute route)
	{
		if (plan == null || clearingPlans.Contains(plan) == false || plan.MoveIndex >= plan.Definition.Moves.Count)
			return;

		TrafficClearingMove move = plan.Definition.Moves[plan.MoveIndex];
		if (move.Route != route || route.TrafficFromCell.Equals(move.ToCell) == false)
		{
			AbortClearingPlan(plan, $"unexpected arrival: {FormatRoute(route)} at {route.TrafficFromCell}");
			return;
		}

		Debug.Log(
			$"[TrafficCoordinator] Clearing move arrived. route={FormatRoute(route)}, " +
			$"cell={move.ToCell}, step={plan.MoveIndex + 1}/{plan.Definition.Moves.Count}");
		plan.MovingRoute = null;
		route.SuspendForTraffic();
		if (plan.AbortAfterStepReason != null)
		{
			AbortClearingPlan(plan, plan.AbortAfterStepReason, plan.CancelledRoute);
			return;
		}
		++plan.MoveIndex;
		StartNextClearingMove(plan);
	}

	private void ProcessClearingPlans()
	{
		if (clearingPlans.Count == 0)
			return;

		clearingPlanScratch.Clear();
		foreach (ClearingPlan plan in clearingPlans)
			clearingPlanScratch.Add(plan);

		for (int i = 0; i < clearingPlanScratch.Count; ++i)
		{
			ClearingPlan plan = clearingPlanScratch[i];
			if (IsClearingPlanValid(plan) == false)
			{
				AbortClearingPlan(plan, "route or worker became invalid");
				continue;
			}
			if (Time.time - plan.StartedAt >= clearingPlanTimeoutSeconds)
			{
				AbortClearingPlan(plan, "timeout");
				continue;
			}

			if (plan.WaitingForPassage &&
				plan.Definition.PassingRoute.TrafficFromCell.Equals(plan.Definition.ReleaseCell))
			{
				CompleteClearingPlan(plan);
			}
		}
	}

	private static bool IsClearingPlanValid(ClearingPlan plan)
	{
		if (plan?.Definition?.PassingRoute?.Worker == null ||
			plan.Definition.PriorityOwner?.Worker == null ||
			plan.Definition.PriorityOwner.Worker.IsOperational == false ||
			plan.Definition.PassingRoute.Worker.IsOperational == false ||
			plan.Definition.PassingRoute.PathRequestVersion != plan.PassingPathVersion)
		{
			return false;
		}

		for (int i = 0; i < plan.Definition.Participants.Count; ++i)
		{
			FindRoute participant = plan.Definition.Participants[i];
			if (participant?.Worker == null || participant.Worker.IsOperational == false ||
				participant.PathRequestVersion != plan.ParticipantPathVersions[participant])
				return false;
		}

		return true;
	}

	private void CompleteClearingPlan(ClearingPlan plan)
	{
		if (plan == null || clearingPlans.Contains(plan) == false)
			return;

		TrafficClearingPlanDefinition definition = plan.Definition;
		Debug.Log(
			$"[TrafficCoordinator] Clearing plan completed. passing={FormatRoute(definition.PassingRoute)}, " +
			$"releaseCell={definition.ReleaseCell}");
		ClearClearingPlan(plan);

		for (int i = definition.Participants.Count - 1; i >= 0; --i)
		{
			FindRoute participant = definition.Participants[i];
			ResumeClearingParticipant(participant, plan.IdleParticipants.Contains(participant));
		}
	}

	private void AbortClearingPlan(ClearingPlan plan, string reason, FindRoute cancelledRoute = null)
	{
		if (plan == null || clearingPlans.Contains(plan) == false)
			return;
		// Do not release a cell from underneath a worker that is physically between tiles.
		if (plan.MovingRoute != null && plan.MovingRoute != cancelledRoute &&
			plan.MovingRoute.IsTrafficStepReserved && plan.MovingRoute.Worker.IsOperational)
		{
			plan.AbortAfterStepReason = reason;
			if (cancelledRoute != null)
				plan.CancelledRoute = cancelledRoute;
			return;
		}

		TrafficClearingPlanDefinition definition = plan.Definition;
		Debug.LogWarning(
			$"[TrafficCoordinator] Clearing plan aborted. reason={reason}, " +
			$"passing={FormatRoute(definition.PassingRoute)}, priority={FormatRoute(definition.PriorityOwner)}");
		ClearClearingPlan(plan);

		for (int i = definition.Participants.Count - 1; i >= 0; --i)
		{
			FindRoute participant = definition.Participants[i];
			if (participant != null && participant != cancelledRoute)
				ResumeClearingParticipant(participant, plan.IdleParticipants.Contains(participant));
		}

		if (definition.PassingRoute != null && definition.PassingRoute != cancelledRoute)
			EnqueueResolve(definition.PassingRoute);
	}

	private void ClearClearingPlan(ClearingPlan plan)
	{
		if (plan == null || clearingPlans.Remove(plan) == false)
			return;

		TrafficClearingPlanDefinition definition = plan.Definition;
		foreach (int3 cell in definition.ReservedCells)
		{
			reservedYieldCells.Remove(cell);
			clearingPlansByCell.Remove(cell);
		}

		RemoveClearingPlanRoute(definition.PassingRoute, plan);
		RemoveClearingPlanRoute(definition.PriorityOwner, plan);
		for (int i = 0; i < definition.Participants.Count; ++i)
		{
			FindRoute participant = definition.Participants[i];
			RemoveClearingPlanRoute(participant, plan);
			if (ReferenceEquals(participant, null) == false &&
				clearingForRoutes.TryGetValue(participant, out FindRoute owner) &&
				owner == definition.PriorityOwner)
			{
				clearingForRoutes.Remove(participant);
			}
		}

		plan.MovingRoute = null;

		// Logical reservations do not emit GridCell unreserve events.
		foreach (var wait in waitingRoutes)
		{
			if (definition.ReservedCells.Contains(wait.Value.DesiredCell))
				EnqueueResolve(wait.Key);
		}
	}

	private void ResumeClearingParticipant(FindRoute route, bool wasIdle)
	{
		if (route?.Worker == null || route.Worker.IsOperational == false || route.Worker.IsWaitingForNavigation)
			return;
		UnregisterWait(route);
		route.ClearTrafficBlockState();
		if (wasIdle)
		{
			CompleteIdleYield(route);
			return;
		}
		RequestFreshRouteOrResume(route);
	}

	private void RemoveClearingPlanRoute(FindRoute route, ClearingPlan plan)
	{
		if (ReferenceEquals(route, null) == false &&
			clearingPlansByRoute.TryGetValue(route, out ClearingPlan current) &&
			current == plan)
		{
			clearingPlansByRoute.Remove(route);
		}
	}

	private static string FormatRoute(FindRoute route)
	{
		return route?.Worker != null
			? $"{route.Worker.Name}#{route.Worker.WorkerID}"
			: "None";
	}

	private bool TryStartOneTileYield(FindRoute yieldingRoute, FindRoute priorityRoute, FindRoute priorityOwner)
	{
		if (yieldingRoute == null ||
			priorityRoute == null ||
			CanYield(yieldingRoute) == false ||
			IsYieldHeld(yieldingRoute))
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
		if (blocker == null || requestedRoute == null || IsYieldHeld(blocker))
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
				SetIdleWorkerDispatchAvailability(blocker, false);
				Debug.Log($"[TrafficCoordinator] Idle blocker yield started. yielding={blocker.Worker.Name}, priority={requestedRoute.Worker.Name}, yieldCell={yieldCell}");
				return true;
			}

			ClearYieldHold(hold);
		}

		return false;
	}

	private bool CanResolveTraffic(FindRoute route) => HasTrafficControlCapability(route);

	private bool CanReplanAroundStaticBlocker(FindRoute route) => HasTrafficControlCapability(route);

	private bool CanRequestDetour(FindRoute route) => HasTrafficControlCapability(route);

	private bool CanYield(FindRoute route) => HasTrafficControlCapability(route);

	private bool CanResolvePriority(FindRoute route) => HasTrafficControlCapability(route);

	private bool HasTrafficControlCapability(FindRoute route)
	{
		if (route?.Worker is not RobotWorker robot)
			return true;

		return robot.IsPlayerOverride || Research?.IsResearched(ResearchIds.TrafficControl) == true;
	}

	private bool CanUseAsIdleYieldCell(in int3 candidate, FindRoute blocker, FindRoute requestedRoute)
	{
		GridCell cell = GridService.GetCell(candidate);
		if (cell == null)
			return false;

		if (GridService.IsBlocked(candidate))
			return false;

		if (cell.OccupancyWorker != null || GridService.GetReservedFindRoute(candidate) != null)
			return false;

		if (reservedYieldCells.Contains(candidate))
			return false;

		if (candidate.Equals(blocker.TrafficFromCell))
			return false;

		if (candidate.Equals(requestedRoute.TrafficFromCell))
			return false;

		if (requestedRoute.TryGetTrafficToCell(out var requestedToCell) && candidate.Equals(requestedToCell))
			return false;

		if (requestedRoute.TryGetFutureToCell(out var requestedFutureCell) && candidate.Equals(requestedFutureCell))
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

		if (cell.OccupancyWorker != null || GridService.GetReservedFindRoute(candidate) != null)
			return false;

		if (reservedYieldCells.Contains(candidate))
			return false;

		if (candidate.Equals(priorityRoute.TrafficFromCell))
			return false;

		if (priorityRoute.TryGetTrafficToCell(out var priorityToCell) && candidate.Equals(priorityToCell))
			return false;

		if (priorityRoute.TryGetFutureToCell(out var priorityFutureCell) && candidate.Equals(priorityFutureCell))
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
			CompleteIdleYield(yieldingRoute);
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
			CompleteIdleYield(yieldingRoute);
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

	private void CompleteIdleYield(FindRoute route)
	{
		if (route?.Worker == null)
			return;

		route.CompleteIdleYieldMove();
		SetIdleWorkerDispatchAvailability(route, true);
	}

	private static void SetIdleWorkerDispatchAvailability(FindRoute route, bool available)
	{
		if (route?.Worker == null || GameContext.HasInstance == false || GameContext.Instance.WorkerMgr == null)
			return;

		if (available)
			GameContext.Instance.WorkerMgr.AddIdleWorker(route.Worker);
		else
			GameContext.Instance.WorkerMgr.RemoveIdleWorker(route.Worker);
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
		if (worker.IsPlayerOverride)
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

	private void RegisterWait(
		FindRoute route,
		in int3 desiredCell,
		WorkerStatusAction blockAction = WorkerStatusAction.TrafficBlock)
	{
		if (route == null)
			return;

		route.SuspendForTraffic(blockAction);
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
		clearingPlans.Clear();
		clearingPlansByRoute.Clear();
		clearingPlansByCell.Clear();
		reservedYieldCells.Clear();
		queuedRoutes.Clear();
		trafficResolveQueue.Clear();
	}
}
