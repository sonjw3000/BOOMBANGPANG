using System;
using System.Collections.Generic;

public enum CapsuleRelocateScope
{
	SameBuilding,
	LinkedBuilding,
	GlobalAllowed,
}

public readonly struct CapsuleRelocateSendRequest
{
	public readonly CapsuleDock SourceDock;
	public readonly CapsuleDockState RequiredSourceDockState;
	public readonly CapsuleLogisticsState RequiredCapsuleState;
	public readonly CapsuleDockState WantedTargetDockState;
	public readonly CapsuleRelocateScope Scope;
	public readonly uint SourceBuildingId;
	public readonly uint RequiredTargetBuildingId;
	public readonly CargoRouteKind RequiredRouteKind;
	public readonly bool RequireRuleMatchedTarget;
	public readonly bool EvaluateLaunchReadiness;
	public readonly Func<CapsuleRelocateMatch, bool> OnMatched;

	public CapsuleRelocateSendRequest(
		CapsuleDock sourceDock,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		CapsuleDockState wantedTargetDockState,
		CapsuleRelocateScope scope,
		uint sourceBuildingId,
		uint requiredTargetBuildingId = 0,
		Func<CapsuleRelocateMatch, bool> onMatched = null,
		CargoRouteKind requiredRouteKind = CargoRouteKind.Standard,
		bool requireRuleMatchedTarget = false,
		bool evaluateLaunchReadiness = false)
	{
		SourceDock = sourceDock;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		WantedTargetDockState = wantedTargetDockState;
		Scope = scope;
		SourceBuildingId = sourceBuildingId;
		RequiredTargetBuildingId = requiredTargetBuildingId;
		RequiredRouteKind = requiredRouteKind;
		RequireRuleMatchedTarget = requireRuleMatchedTarget;
		EvaluateLaunchReadiness = evaluateLaunchReadiness;
		OnMatched = onMatched;
	}
}

public readonly struct CapsuleRelocateDemand
{
	public readonly CapsuleDock TargetDock;
	public readonly CapsuleDockState RequiredTargetDockState;
	public readonly CapsuleDockState RequiredSourceDockState;
	public readonly CapsuleLogisticsState RequiredCapsuleState;
	public readonly CapsuleRelocateScope Scope;
	public readonly uint TargetBuildingId;
	public readonly uint RequiredSourceBuildingId;
	public readonly CargoRouteKind RequiredRouteKind;
	public readonly Func<CapsuleRelocateMatch, bool> OnMatched;

	public CapsuleRelocateDemand(
		CapsuleDock targetDock,
		CapsuleDockState requiredTargetDockState,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		CapsuleRelocateScope scope,
		uint targetBuildingId,
		uint requiredSourceBuildingId = 0,
		Func<CapsuleRelocateMatch, bool> onMatched = null,
		CargoRouteKind requiredRouteKind = CargoRouteKind.Standard)
	{
		TargetDock = targetDock;
		RequiredTargetDockState = requiredTargetDockState;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		Scope = scope;
		TargetBuildingId = targetBuildingId;
		RequiredSourceBuildingId = requiredSourceBuildingId;
		RequiredRouteKind = requiredRouteKind;
		OnMatched = onMatched;
	}
}

public readonly struct CapsuleRelocateMatch
{
	public readonly CapsuleDock SourceDock;
	public readonly CapsuleDock TargetDock;
	public readonly uint SourceBuildingId;
	public readonly uint TargetBuildingId;

	public CapsuleRelocateMatch(
		CapsuleDock sourceDock,
		CapsuleDock targetDock,
		uint sourceBuildingId,
		uint targetBuildingId)
	{
		SourceDock = sourceDock;
		TargetDock = targetDock;
		SourceBuildingId = sourceBuildingId;
		TargetBuildingId = targetBuildingId;
	}
}

public readonly struct CapsuleRelocateDemandSnapshot
{
	public int PendingSends { get; }
	public int PendingDemands { get; }
	public int SourceCount => PendingSends + PendingDemands;

	public CapsuleRelocateDemandSnapshot(int pendingSends, int pendingDemands)
	{
		PendingSends = pendingSends;
		PendingDemands = pendingDemands;
	}
}

public sealed class CapsuleRelocateCoordinator
{
	private readonly CapsuleDockService dockService;
	private readonly CapsuleBufferService bufferService;
	private readonly LinkedList<CapsuleRelocateSendRequest> pendingSends = new();
	private readonly LinkedList<CapsuleRelocateDemand> pendingDemands = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateSendRequest>> pendingSendNodeBySource = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateDemand>> pendingDemandNodeByTarget = new();
	private readonly HashSet<CapsuleDock> reservedDocks = new();
	private readonly HashSet<CapsuleDock> activeRelocationSources = new();
	private readonly HashSet<CapsuleDock> activeRelocationTargets = new();
	private readonly HashSet<CapsuleDock> potentialReturnSources = new();
	private readonly HashSet<CapsuleDock> playerClaimedDocks = new();
	private readonly HashSet<CapsuleDock> dirtyDocks = new();
	private readonly HashSet<uint> dirtyBuildingIds = new();
	private readonly HashSet<uint> processingDirtyBuildingIds = new();
	private readonly List<CapsuleBuffer> ruleTargetScratch = new();
	private readonly Func<uint, uint, bool> canUseLinkedBuilding;
	private readonly Action<CapsuleDock> evaluateDirtyDock;
	private readonly Action<uint> evaluateDirtyBuilding;
	private bool isRestoring;
	private bool isProcessingDirty;

	public event Action<CapsuleDock> OnPlayerClaimReleased;
	public event Action<uint, CapsuleBuffer, bool> OnRuleRoutingEvaluated;

	public int PendingSendCount => pendingSendNodeBySource.Count;
	public int PendingDemandCount => pendingDemandNodeByTarget.Count;
	public int DirtyDockCount => dirtyDocks.Count;
	public int DirtyBuildingCount => dirtyBuildingIds.Count;
	public bool HasDirty => dirtyDocks.Count > 0 || dirtyBuildingIds.Count > 0;

	public CapsuleRelocateDemandSnapshot GetDemandSnapshot()
	{
		return GetDemandSnapshot(0, filterByBuilding: false);
	}

	public CapsuleRelocateDemandSnapshot GetDemandSnapshot(uint buildingId)
	{
		return GetDemandSnapshot(buildingId, filterByBuilding: true);
	}

	private CapsuleRelocateDemandSnapshot GetDemandSnapshot(uint buildingId, bool filterByBuilding)
	{
		int sendCount = 0;
		foreach (CapsuleRelocateSendRequest request in pendingSends)
		{
			if ((filterByBuilding == false || request.SourceBuildingId == buildingId) &&
				IsSendSourceValid(request))
			{
				++sendCount;
			}
		}

		int demandCount = 0;
		foreach (CapsuleRelocateDemand demand in pendingDemands)
		{
			if ((filterByBuilding == false || demand.TargetBuildingId == buildingId) &&
				IsDemandTargetValid(demand))
			{
				++demandCount;
			}
		}

		return new CapsuleRelocateDemandSnapshot(sendCount, demandCount);
	}

	public CapsuleRelocateCoordinator(
		CapsuleDockService dockService,
		Func<uint, uint, bool> canUseLinkedBuilding = null,
		CapsuleBufferService bufferService = null,
		Action<CapsuleDock> evaluateDirtyDock = null,
		Action<uint> evaluateDirtyBuilding = null)
	{
		this.dockService = dockService;
		this.canUseLinkedBuilding = canUseLinkedBuilding;
		this.bufferService = bufferService;
		this.evaluateDirtyDock = evaluateDirtyDock;
		this.evaluateDirtyBuilding = evaluateDirtyBuilding;
	}

	public void MarkDirty(CapsuleDock dock)
	{
		if (dock != null)
			dirtyDocks.Add(dock);
	}

	public void MarkBuildingDirty(uint buildingId)
	{
		if (buildingId != 0)
			dirtyBuildingIds.Add(buildingId);
	}

	public void ProcessDirty()
	{
		if (isRestoring || isProcessingDirty || HasDirty == false)
			return;

		isProcessingDirty = true;
		try
		{
			const int maxPasses = 32;
			for (int pass = 0; pass < maxPasses && HasDirty; ++pass)
			{
				uint[] buildingIds = new uint[dirtyBuildingIds.Count];
				dirtyBuildingIds.CopyTo(buildingIds);
				dirtyBuildingIds.Clear();
				processingDirtyBuildingIds.Clear();
				for (int i = 0; i < buildingIds.Length; ++i)
					processingDirtyBuildingIds.Add(buildingIds[i]);

				CapsuleDock[] docks = new CapsuleDock[dirtyDocks.Count];
				dirtyDocks.CopyTo(docks);
				dirtyDocks.Clear();

				for (int i = 0; i < buildingIds.Length; ++i)
					evaluateDirtyBuilding?.Invoke(buildingIds[i]);

				for (int i = 0; i < docks.Length; ++i)
				{
					CapsuleDock dock = docks[i];
					if (dock == null)
						continue;
					if (dockService?.TryGetRegisteredBuildingId(dock, out uint buildingId) == true &&
						processingDirtyBuildingIds.Contains(buildingId))
					{
						continue;
					}

					evaluateDirtyDock?.Invoke(dock);
				}
			}
		}
		finally
		{
			processingDirtyBuildingIds.Clear();
			isProcessingDirty = false;
		}
	}

	public bool RequestSend(CapsuleRelocateSendRequest request)
	{
		if (IsSendSourceValid(request, checkReservation: false) == false)
			return false;
		if (isRestoring)
		{
			AddPendingSend(request);
			return false;
		}

		if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
		{
			if (TryAcceptSendMatch(request, targetDock, targetBuildingId))
				return true;
		}

		AddPendingSend(request);
		return false;
	}

	public bool RequestDemand(CapsuleRelocateDemand demand)
	{
		if (IsDemandTargetValid(demand, checkReservation: false) == false)
			return false;
		if (isRestoring)
		{
			AddPendingDemand(demand);
			return false;
		}

		if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
		{
			if (TryAcceptDemandMatch(demand, sourceDock, sourceBuildingId))
				return true;
		}

		AddPendingDemand(demand);
		return false;
	}

	public bool NotifyCapsuleDocked(CapsuleDock dock)
	{
		MarkDirty(dock);
		potentialReturnSources.Remove(dock);
		activeRelocationTargets.Remove(dock);
		ReleaseReservation(dock);
		if (isRestoring)
			return false;
		return TryMatchPendingDemand() || TryMatchPendingSend();
	}

	public bool NotifyCapsuleUndocked(CapsuleDock dock)
	{
		MarkDirty(dock);
		activeRelocationSources.Remove(dock);
		if (potentialReturnSources.Contains(dock) == false)
			ReleaseReservation(dock);
		if (isRestoring)
			return false;
		return TryMatchPendingSend() || TryMatchPendingDemand();
	}

	public bool NotifyDockStateChanged(CapsuleDock dock)
	{
		MarkDirty(dock);
		if (potentialReturnSources.Contains(dock) == false)
			ReleaseReservation(dock);
		if (isRestoring)
			return false;
		return TryMatchPendingSend() || TryMatchPendingDemand();
	}

	public void ReleaseReservation(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			ReleaseReservation(sourceDock);
		if (targetDock != null)
			ReleaseReservation(targetDock);
	}

	public void ReleaseReservation(CapsuleDock dock)
	{
		if (dock != null && potentialReturnSources.Contains(dock) == false)
			reservedDocks.Remove(dock);
	}

	public void NotifyRelocationEnded(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		MarkDirty(sourceDock);
		MarkDirty(targetDock);
		if (sourceDock != null)
		{
			potentialReturnSources.Remove(sourceDock);
			activeRelocationSources.Remove(sourceDock);
			reservedDocks.Remove(sourceDock);
		}

		if (targetDock != null)
		{
			activeRelocationTargets.Remove(targetDock);
			reservedDocks.Remove(targetDock);
		}

		if (isRestoring)
			return;

		TryMatchPendingSend();
		TryMatchPendingDemand();
	}

	public void NotifyRelocationTargetReleased(CapsuleDock targetDock)
	{
		MarkDirty(targetDock);
		if (targetDock != null)
		{
			activeRelocationTargets.Remove(targetDock);
			reservedDocks.Remove(targetDock);
		}

		TryMatchPendingSend();
		TryMatchPendingDemand();
	}

	public void ResetRuntimeState()
	{
		isRestoring = false;
		pendingSends.Clear();
		pendingDemands.Clear();
		pendingSendNodeBySource.Clear();
		pendingDemandNodeByTarget.Clear();
		reservedDocks.Clear();
		activeRelocationSources.Clear();
		activeRelocationTargets.Clear();
		potentialReturnSources.Clear();
		playerClaimedDocks.Clear();
		dirtyDocks.Clear();
		dirtyBuildingIds.Clear();
		processingDirtyBuildingIds.Clear();
		ruleTargetScratch.Clear();
		isProcessingDirty = false;
	}

	public void BeginRestore()
	{
		isRestoring = true;
	}

	public void EndRestore()
	{
		isRestoring = false;
		while (TryMatchPendingDemand() || TryMatchPendingSend())
		{
		}
	}

	public void NotifyTaskDependenciesChanged()
	{
		if (isRestoring)
			return;

		while (TryMatchPendingDemand() || TryMatchPendingSend())
		{
		}
	}

	public void NotifyRuleRoutingEvaluated(
		uint buildingId,
		CapsuleBuffer buffer,
		bool isRuleMatched)
	{
		if (buildingId == 0 || buffer == null)
			return;

		OnRuleRoutingEvaluated?.Invoke(buildingId, buffer, isRuleMatched);
	}

	public void CancelPendingRequests(CapsuleDock dock)
	{
		if (dock == null)
			return;

		RemoveSendRequest(dock);
		RemoveDemand(dock);
	}

	public void RemoveDock(CapsuleDock dock)
	{
		if (dock == null)
			return;

		activeRelocationSources.Remove(dock);
		activeRelocationTargets.Remove(dock);
		potentialReturnSources.Remove(dock);
		reservedDocks.Remove(dock);
		playerClaimedDocks.Remove(dock);
		dirtyDocks.Remove(dock);
		CancelPendingRequests(dock);
	}

	public bool IsReserved(CapsuleDock dock)
	{
		return dock != null && (reservedDocks.Contains(dock) || playerClaimedDocks.Contains(dock));
	}

	public bool IsPlayerClaimed(CapsuleDock dock)
	{
		return dock != null && playerClaimedDocks.Contains(dock);
	}

	public bool IsRelocationSourceActive(CapsuleDock dock)
	{
		return dock != null && activeRelocationSources.Contains(dock);
	}

	public bool IsRelocationTargetActive(CapsuleDock dock)
	{
		return dock != null && activeRelocationTargets.Contains(dock);
	}

	public bool TryReserveActiveTarget(CapsuleDock dock)
	{
		if (IsFacilityAvailable(dock) == false ||
			dock.CanPutBox() == false ||
			reservedDocks.Contains(dock) ||
			playerClaimedDocks.Contains(dock) ||
			activeRelocationTargets.Contains(dock))
		{
			return false;
		}

		reservedDocks.Add(dock);
		activeRelocationTargets.Add(dock);
		return true;
	}

	public bool TryHoldSourceForPotentialReturn(CapsuleDock sourceDock)
	{
		if (IsFacilityAvailable(sourceDock) == false ||
			activeRelocationSources.Contains(sourceDock) == false ||
			reservedDocks.Contains(sourceDock) == false)
		{
			return false;
		}

		potentialReturnSources.Add(sourceDock);
		return true;
	}

	public bool TryReplaceActiveTargetWithHeldSource(CapsuleDock currentTarget, CapsuleDock sourceDock)
	{
		if (IsFacilityAvailable(sourceDock) == false ||
			potentialReturnSources.Contains(sourceDock) == false ||
			sourceDock.CanPutBox() == false ||
			(currentTarget != null && activeRelocationTargets.Contains(currentTarget) == false))
		{
			return false;
		}

		CancelPendingRequests(sourceDock);
		if (currentTarget != null)
		{
			activeRelocationTargets.Remove(currentTarget);
			reservedDocks.Remove(currentTarget);
		}

		potentialReturnSources.Remove(sourceDock);
		reservedDocks.Add(sourceDock);
		activeRelocationTargets.Add(sourceDock);
		if (isRestoring == false)
		{
			TryMatchPendingSend();
			TryMatchPendingDemand();
		}

		return true;
	}

	public bool RestoreActiveRelocation(
		CapsuleDock sourceDock,
		CapsuleDock targetDock,
		bool payloadAlreadyPicked,
		bool holdSourceForPotentialReturn = false)
	{
		if (sourceDock == null || targetDock == null)
			return false;

		CancelPendingRequests(sourceDock);
		CancelPendingRequests(targetDock);
		if (payloadAlreadyPicked)
		{
			if (holdSourceForPotentialReturn && ReferenceEquals(sourceDock, targetDock) == false)
			{
				reservedDocks.Add(sourceDock);
				potentialReturnSources.Add(sourceDock);
			}
			reservedDocks.Add(targetDock);
			activeRelocationTargets.Add(targetDock);
		}
		else
		{
			Reserve(sourceDock, targetDock);
			MarkActive(sourceDock, targetDock);
		}
		return true;
	}

	public bool TryClaimForPlayer(CapsuleDock dock)
	{
		if (IsFacilityAvailable(dock) == false ||
			playerClaimedDocks.Contains(dock))
		{
			return false;
		}

		playerClaimedDocks.Add(dock);
		CancelPendingRequests(dock);
		return true;
	}

	public void ReleasePlayerClaim(CapsuleDock dock)
	{
		if (dock == null || playerClaimedDocks.Remove(dock) == false)
			return;

		MarkDirty(dock);
		TryMatchPendingSend();
		TryMatchPendingDemand();
		OnPlayerClaimReleased?.Invoke(dock);
	}

	private bool TryMatchPendingSend()
	{
		LinkedListNode<CapsuleRelocateSendRequest> node = pendingSends.First;
		while (node != null)
		{
			LinkedListNode<CapsuleRelocateSendRequest> next = node.Next;
			CapsuleRelocateSendRequest request = node.Value;
			if (IsSendSourceValid(request) == false)
			{
				RemovePendingSendNode(node);
				node = next;
				continue;
			}

			if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
			{
				if (TryAcceptSendMatch(request, targetDock, targetBuildingId))
				{
					RemovePendingSendNode(node);
					return true;
				}
			}

			node = next;
		}

		return false;
	}

	private bool TryMatchPendingDemand()
	{
		LinkedListNode<CapsuleRelocateDemand> node = pendingDemands.First;
		while (node != null)
		{
			LinkedListNode<CapsuleRelocateDemand> next = node.Next;
			CapsuleRelocateDemand demand = node.Value;
			if (IsDemandTargetValid(demand) == false)
			{
				RemovePendingDemandNode(node);
				node = next;
				continue;
			}

			if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
			{
				if (TryAcceptDemandMatch(demand, sourceDock, sourceBuildingId))
				{
					RemovePendingDemandNode(node);
					return true;
				}
			}

			node = next;
		}

		return false;
	}

	private bool TryFindReceiver(
		CapsuleRelocateSendRequest request,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		if (request.RequireRuleMatchedTarget)
			return TryFindRuleMatchedReceiver(request, out targetDock, out targetBuildingId);

		if (dockService == null)
			return false;

		uint queryBuildingId = request.RequiredTargetBuildingId != 0
			? request.RequiredTargetBuildingId
			: request.Scope == CapsuleRelocateScope.SameBuilding
				? request.SourceBuildingId
				: 0;

		return dockService.TryFindDock(
			queryBuildingId,
			request.WantedTargetDockState,
			false,
			out targetDock,
			out targetBuildingId,
			(candidate, candidateBuildingId) => CanMatch(request, candidate, candidateBuildingId));
	}

	private bool TryFindRuleMatchedReceiver(
		CapsuleRelocateSendRequest request,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		CargoCapsule capsule = request.SourceDock?.DockedCapsule;
		if (bufferService == null || capsule == null)
			return false;

		uint queryBuildingId = request.RequiredTargetBuildingId != 0
			? request.RequiredTargetBuildingId
			: request.Scope == CapsuleRelocateScope.SameBuilding
				? request.SourceBuildingId
				: 0;
		if (bufferService.TryQueryRuleMatchedDestinations(
				queryBuildingId,
				capsule,
				ruleTargetScratch,
				request.EvaluateLaunchReadiness) == false)
		{
			return false;
		}

		for (int i = 0; i < ruleTargetScratch.Count; ++i)
		{
			CapsuleBuffer candidate = ruleTargetScratch[i];
			if (bufferService.TryGetRegisteredBuildingId(candidate, out uint candidateBuildingId) == false ||
				CanMatch(request, candidate, candidateBuildingId) == false)
			{
				continue;
			}

			targetDock = candidate;
			targetBuildingId = candidateBuildingId;
			return true;
		}

		return false;
	}

	private bool TryFindSource(
		CapsuleRelocateDemand demand,
		out CapsuleDock sourceDock,
		out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;
		if (dockService == null)
			return false;

		uint queryBuildingId = demand.RequiredSourceBuildingId != 0
			? demand.RequiredSourceBuildingId
			: demand.Scope == CapsuleRelocateScope.SameBuilding
				? demand.TargetBuildingId
				: 0;

		return dockService.TryFindDock(
			queryBuildingId,
			demand.RequiredSourceDockState,
			true,
			out sourceDock,
			out sourceBuildingId,
			(candidate, candidateBuildingId) => CanMatch(candidate, candidateBuildingId, demand));
	}

	private bool CanMatch(CapsuleRelocateSendRequest request, CapsuleDock targetDock, uint targetBuildingId)
	{
		if (targetDock == null ||
			IsFacilityAvailable(request.SourceDock) == false ||
			IsFacilityAvailable(targetDock) == false ||
			HasTaskDependency(request.SourceDock) ||
			HasTaskDependency(targetDock) ||
			playerClaimedDocks.Contains(request.SourceDock) ||
			playerClaimedDocks.Contains(targetDock) ||
			reservedDocks.Contains(request.SourceDock) ||
			reservedDocks.Contains(targetDock) ||
			activeRelocationSources.Contains(request.SourceDock) ||
			activeRelocationTargets.Contains(targetDock) ||
			(request.RequireRuleMatchedTarget == false && targetDock.DockState != request.WantedTargetDockState) ||
			targetDock.CanAcceptCargoRoute(request.RequiredRouteKind) == false ||
			targetDock.CanPutBox() == false)
		{
			return false;
		}

		return CanUseBuilding(request.Scope, request.SourceBuildingId, targetBuildingId) &&
			(request.RequiredTargetBuildingId == 0 || request.RequiredTargetBuildingId == targetBuildingId);
	}

	private bool CanMatch(CapsuleDock sourceDock, uint sourceBuildingId, CapsuleRelocateDemand demand)
	{
		if (sourceDock == null ||
			IsFacilityAvailable(sourceDock) == false ||
			IsFacilityAvailable(demand.TargetDock) == false ||
			HasTaskDependency(sourceDock) ||
			HasTaskDependency(demand.TargetDock) ||
			playerClaimedDocks.Contains(sourceDock) ||
			playerClaimedDocks.Contains(demand.TargetDock) ||
			reservedDocks.Contains(sourceDock) ||
			reservedDocks.Contains(demand.TargetDock) ||
			activeRelocationSources.Contains(sourceDock) ||
			activeRelocationTargets.Contains(demand.TargetDock) ||
			sourceDock.DockState != demand.RequiredSourceDockState ||
			sourceDock.DockedCapsule?.LogisticsState != demand.RequiredCapsuleState ||
			sourceDock.DockedCapsule?.RouteKind != demand.RequiredRouteKind ||
			sourceDock.CanGetBox() == false ||
			demand.TargetDock.CanAcceptCargoRoute(demand.RequiredRouteKind) == false ||
			demand.TargetDock.CanPutBox() == false)
		{
			return false;
		}

		return CanUseBuilding(demand.Scope, sourceBuildingId, demand.TargetBuildingId) &&
			(demand.RequiredSourceBuildingId == 0 || demand.RequiredSourceBuildingId == sourceBuildingId);
	}

	private bool CanUseBuilding(CapsuleRelocateScope scope, uint sourceBuildingId, uint targetBuildingId)
	{
		return scope switch
		{
			CapsuleRelocateScope.SameBuilding => sourceBuildingId != 0 && sourceBuildingId == targetBuildingId,
			CapsuleRelocateScope.LinkedBuilding =>
				targetBuildingId != 0 &&
				sourceBuildingId != targetBuildingId &&
				canUseLinkedBuilding != null &&
				canUseLinkedBuilding(sourceBuildingId, targetBuildingId),
			CapsuleRelocateScope.GlobalAllowed => true,
			_ => false,
		};
	}

	private bool IsSendSourceValid(CapsuleRelocateSendRequest request, bool checkReservation = true)
	{
		return request.SourceDock != null &&
			IsFacilityAvailable(request.SourceDock) &&
			playerClaimedDocks.Contains(request.SourceDock) == false &&
			(checkReservation == false || reservedDocks.Contains(request.SourceDock) == false) &&
			activeRelocationSources.Contains(request.SourceDock) == false &&
			request.SourceDock.DockState == request.RequiredSourceDockState &&
			request.SourceDock.DockedCapsule?.LogisticsState == request.RequiredCapsuleState &&
			request.SourceDock.DockedCapsule?.RouteKind == request.RequiredRouteKind &&
			request.SourceDock.CanGetBox();
	}

	private bool IsDemandTargetValid(CapsuleRelocateDemand demand, bool checkReservation = true)
	{
		return demand.TargetDock != null &&
			IsFacilityAvailable(demand.TargetDock) &&
			playerClaimedDocks.Contains(demand.TargetDock) == false &&
			(checkReservation == false || reservedDocks.Contains(demand.TargetDock) == false) &&
			activeRelocationTargets.Contains(demand.TargetDock) == false &&
			demand.TargetDock.DockState == demand.RequiredTargetDockState &&
			demand.TargetDock.CanAcceptCargoRoute(demand.RequiredRouteKind) &&
			demand.TargetDock.CanPutBox();
	}

	private static bool IsFacilityAvailable(CapsuleDock dock)
	{
		return dock != null &&
			(GameContext.HasInstance == false ||
			 GameContext.Instance.FacilityMgr == null ||
			 GameContext.Instance.FacilityMgr.IsInvalidating(dock) == false);
	}

	private static bool HasTaskDependency(CapsuleDock dock)
	{
		return dock != null &&
			GameContext.HasInstance &&
			GameContext.Instance.TaskMgr?.HasManagedTaskFacilityDependency(dock) == true;
	}

	private void Reserve(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			reservedDocks.Add(sourceDock);
		if (targetDock != null)
			reservedDocks.Add(targetDock);
	}

	private void MarkActive(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			activeRelocationSources.Add(sourceDock);
		if (targetDock != null)
			activeRelocationTargets.Add(targetDock);
	}

	private bool TryAcceptSendMatch(
		CapsuleRelocateSendRequest request,
		CapsuleDock targetDock,
		uint targetBuildingId)
	{
		CapsuleRelocateMatch match = new(request.SourceDock, targetDock, request.SourceBuildingId, targetBuildingId);
		Reserve(match.SourceDock, match.TargetDock);
		if (request.OnMatched == null)
			return true;

		if (request.OnMatched(match))
		{
			MarkActive(match.SourceDock, match.TargetDock);
			return true;
		}

		ReleaseReservation(match.SourceDock, match.TargetDock);
		return false;
	}

	private bool TryAcceptDemandMatch(
		CapsuleRelocateDemand demand,
		CapsuleDock sourceDock,
		uint sourceBuildingId)
	{
		CapsuleRelocateMatch match = new(sourceDock, demand.TargetDock, sourceBuildingId, demand.TargetBuildingId);
		Reserve(match.SourceDock, match.TargetDock);
		if (demand.OnMatched == null)
			return true;

		if (demand.OnMatched(match))
		{
			MarkActive(match.SourceDock, match.TargetDock);
			return true;
		}

		ReleaseReservation(match.SourceDock, match.TargetDock);
		return false;
	}

	private void AddPendingSend(CapsuleRelocateSendRequest request)
	{
		RemoveSendRequest(request.SourceDock);
		LinkedListNode<CapsuleRelocateSendRequest> node = pendingSends.AddLast(request);
		pendingSendNodeBySource[request.SourceDock] = node;
	}

	private void AddPendingDemand(CapsuleRelocateDemand demand)
	{
		RemoveDemand(demand.TargetDock);
		LinkedListNode<CapsuleRelocateDemand> node = pendingDemands.AddLast(demand);
		pendingDemandNodeByTarget[demand.TargetDock] = node;
	}

	private void RemoveSendRequest(CapsuleDock sourceDock)
	{
		if (sourceDock == null ||
			pendingSendNodeBySource.TryGetValue(sourceDock, out LinkedListNode<CapsuleRelocateSendRequest> node) == false)
		{
			return;
		}

		RemovePendingSendNode(node);
	}

	private void RemoveDemand(CapsuleDock targetDock)
	{
		if (targetDock == null ||
			pendingDemandNodeByTarget.TryGetValue(targetDock, out LinkedListNode<CapsuleRelocateDemand> node) == false)
		{
			return;
		}

		RemovePendingDemandNode(node);
	}

	private void RemovePendingSendNode(LinkedListNode<CapsuleRelocateSendRequest> node)
	{
		if (node == null)
			return;

		pendingSendNodeBySource.Remove(node.Value.SourceDock);
		node.List?.Remove(node);
	}

	private void RemovePendingDemandNode(LinkedListNode<CapsuleRelocateDemand> node)
	{
		if (node == null)
			return;

		pendingDemandNodeByTarget.Remove(node.Value.TargetDock);
		node.List?.Remove(node);
	}
}
