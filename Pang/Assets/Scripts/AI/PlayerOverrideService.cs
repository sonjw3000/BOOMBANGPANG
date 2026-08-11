using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public enum PlayerTransferDirection
{
	PickFromTarget = 0,
	PutToTarget = 1,
}

public readonly struct PlayerItemStackKey
{
	public readonly uint ItemId;
	public readonly int FreshnessPercent;
	public readonly int DamagePercent;
	public readonly ItemStatus Status;
	public readonly ItemQuality Quality;
	public readonly PackageOutboundStage OutboundStage;

	public PlayerItemStackKey(
		uint itemId,
		int freshnessPercent,
		int damagePercent,
		ItemStatus status,
		ItemQuality quality,
		PackageOutboundStage outboundStage)
	{
		ItemId = itemId;
		FreshnessPercent = freshnessPercent;
		DamagePercent = damagePercent;
		Status = status;
		Quality = quality;
		OutboundStage = outboundStage;
	}

	public static PlayerItemStackKey From(ItemStack stack)
	{
		return stack == null
			? default
			: new PlayerItemStackKey(
				stack.ItemID,
				stack.FreshnessPercent,
				stack.DamagePercent,
				stack.Status,
				stack.Quality,
				stack.OutboundStage);
	}

	public bool Matches(ItemStack stack)
	{
		return stack != null &&
			stack.ItemID == ItemId &&
			stack.FreshnessPercent == FreshnessPercent &&
			stack.DamagePercent == DamagePercent &&
			stack.Status == Status &&
			stack.Quality == Quality &&
			stack.OutboundStage == OutboundStage;
	}
}

public readonly struct PlayerInteractionTarget
{
	private readonly Component component;

	public string DisplayName { get; }
	public InteractionKind AvailableKinds { get; }
	public bool CanHandleWholeBox => component is IBoxHandleable && component is not PackingStation;

	internal Component Component => component;
	internal CapsuleDock CapsuleDock => component as CapsuleDock;

	internal PlayerInteractionTarget(Component component, InteractionKind availableKinds)
	{
		this.component = component;
		AvailableKinds = availableKinds;
		DisplayName = component != null
			? $"{component.name} · {component.GetType().Name}"
			: "Unavailable interaction";
	}

	public IItemContainer ResolveContainer()
	{
		if (component == null)
			return null;

		if (component is CapsuleBuffer buffer)
			return buffer;

		if (component is IItemContainer directContainer)
			return directContainer;

		if (component is CapsuleDock dock)
			return dock.DockedCapsule;

		return null;
	}

	internal IBoxHandleable ResolveBoxHandle()
	{
		return component is not PackingStation ? component as IBoxHandleable : null;
	}
}

public sealed class PlayerOverrideService
{
	private const InteractionKind SupportedInteractionKinds = InteractionKind.Pick | InteractionKind.Put;
	private readonly HashSet<AIWorker> observedWorkers = new();

	public event Action<AIWorker> OnWorkerStateChanged;
	public event Action<AIWorker> OnInteractionWindowRequested;

	public void PrepareForSave(IReadOnlyList<AIWorker> workers)
	{
		if (workers == null)
			return;

		for (int i = 0; i < workers.Count; ++i)
			workers[i]?.PreparePlayerOverrideForSave();
	}

	public void ResetRuntimeState()
	{
		foreach (AIWorker worker in observedWorkers)
		{
			if (worker == null)
				continue;

			worker.PreparePlayerOverrideForSave();
			worker.PlayerOverrideStateChanged -= HandleWorkerStateChanged;
		}

		observedWorkers.Clear();
	}

	public bool TryTakeControl(AIWorker worker, out string message)
	{
		message = string.Empty;
		if (worker == null)
		{
			message = "Select a worker first.";
			return false;
		}

		if (worker.IsPlayerOverride)
		{
			Observe(worker);
			return true;
		}

		if (worker.IsOperational == false)
		{
			message = "The worker is not operational.";
			return false;
		}

		if (worker.HasPendingBlockingIncident)
		{
			message = "Resolve the worker's incident before taking control.";
			return false;
		}

		if (worker.IsAssignedToPackingStation)
		{
			message = "A packing-station worker cannot enter Player Override yet.";
			return false;
		}

		bool preserveNavigationTask = worker.CurrentTask != null && worker.IsWaitingForNavigation;
		if (worker.CurrentTask != null && preserveNavigationTask == false)
		{
			WorkerTask task = worker.CurrentTask;
			TaskManager taskManager = GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
			if (taskManager == null)
			{
				message = "The current task manager is unavailable.";
				return false;
			}

			worker.PrepareForPlayerControlPreemption();
			if (taskManager.InvalidateTask(task, TaskInvalidationReason.PlayerWorkerTakeover) == false)
			{
				worker.ReevaluateTask(task);
				message = "The current task could not be preempted for player control.";
				return false;
			}
		}
		else if (preserveNavigationTask == false)
		{
			worker.PrepareForPlayerControlPreemption();
		}

		Observe(worker);
		if (worker.TryEnterPlayerOverride(preserveNavigationTask, out message))
		{
			if (worker.IsManualNavigation && GameContext.HasInstance)
			{
				GameContext.Instance.HudEventManager?.Publish(
					HudEventType.Warning,
					"Manual Navigation ignores coverage limits. Changing navigation regions releases the current hub allocation.",
					worker);
			}
			return true;
		}

		StopObserving(worker);
		if (GameContext.HasInstance && worker.IsOperational && worker.CurrentTask == null)
			GameContext.Instance.WorkerMgr?.AddIdleWorker(worker);
		return false;
	}

	public bool TryRequestMove(AIWorker worker, in int3 destination, out string message)
	{
		message = string.Empty;
		if (worker == null || GameContext.HasInstance == false)
		{
			message = "The worker or game context is unavailable.";
			return false;
		}

		GridService gridService = GameContext.Instance.GridService;
		if (gridService == null || gridService.GetCell(destination) == null)
		{
			message = "The requested destination is outside the map.";
			return false;
		}

		if (worker.GridPosition.Equals(destination) == false && gridService.IsBlocked(destination))
		{
			message = "The requested destination is blocked.";
			return false;
		}

		bool enteredForMove = worker.IsPlayerOverride == false;
		if (TryTakeControl(worker, out message) == false)
			return false;

		Observe(worker);
		if (worker.TryRequestPlayerOverrideMove(destination, out message))
			return true;

		if (enteredForMove)
			worker.TryExitPlayerOverride(out _);
		return false;
	}

	public bool TryReleaseControl(AIWorker worker, out string message)
	{
		message = string.Empty;
		if (worker == null || worker.IsPlayerOverride == false)
		{
			message = "The worker is not under player control.";
			return false;
		}

		if (worker.PlayerOverridePhase != PlayerOverridePhase.AwaitingCommand)
		{
			message = "Wait until the current player command is complete.";
			return false;
		}

		if (worker.CarryingAbility?.CarryingBox != null && worker.IsNavigationRescueOverride == false)
		{
			message = "Put down the carried box or capsule before releasing control.";
			return false;
		}

		Observe(worker);
		return worker.TryExitPlayerOverride(out message);
	}

	public void RequestInteractionWindow(AIWorker worker)
	{
		if (worker == null || worker.IsPlayerOverride == false ||
			worker.PlayerOverridePhase != PlayerOverridePhase.AwaitingCommand ||
			worker.IsNavigationRescueOverride)
		{
			return;
		}

		Observe(worker);
		OnInteractionWindowRequested?.Invoke(worker);
	}

	public void GetInteractionTargets(AIWorker worker, List<PlayerInteractionTarget> results)
	{
		if (results == null)
			return;

		results.Clear();
		if (worker == null || worker.IsPlayerOverride == false || GameContext.HasInstance == false)
			return;

		IReadOnlyCollection<GameObject> objects =
			GameContext.Instance.GridService?.GetObjectsOnGrid(worker.GridPosition);
		if (objects == null)
			return;

		foreach (GameObject targetObject in objects)
		{
			if (targetObject == null ||
				targetObject.TryGetComponent(out IInteractionPoint interactionPoint) == false ||
				interactionPoint is not Component component ||
				component is PackingStation)
			{
				continue;
			}

			InteractionKind availableKinds = GetKindsAtPosition(interactionPoint, worker.GridPosition);
			if (availableKinds == InteractionKind.None)
				continue;

			PlayerInteractionTarget target = new(component, availableKinds);
			if (target.ResolveContainer() == null && target.CanHandleWholeBox == false)
				continue;

			results.Add(target);
		}

		results.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName));
	}

	public bool TryRequestItemTransfer(
		AIWorker worker,
		PlayerInteractionTarget target,
		PlayerTransferDirection direction,
		PlayerItemStackKey stackKey,
		int quantity,
		out string message)
	{
		message = string.Empty;
		if (TryValidateAwaitingWorker(worker, out message) == false ||
			TryValidateTarget(worker, target, direction, out message) == false)
		{
			return false;
		}

		if (worker.HasAbility(WorkerAbility.PickingStoring) == false)
		{
			message = "This worker cannot perform item picking or storing.";
			return false;
		}

		BoxBase carriedBox = worker.CarryingAbility?.CarryingBox;
		IItemContainer facilityContainer = target.ResolveContainer();
		if (carriedBox == null || facilityContainer == null)
		{
			message = "Item transfer requires a carried container and a facility container.";
			return false;
		}

		if (HasPickingManifest(carriedBox) || HasPickingManifest(ResolveOwningBox(target, facilityContainer)))
		{
			message = "Order cargo must be moved with its entire box or capsule.";
			return false;
		}

		if (quantity <= 0)
		{
			message = "Choose a positive item quantity.";
			return false;
		}

		IItemContainer source = direction == PlayerTransferDirection.PickFromTarget
			? facilityContainer
			: carriedBox;
		IItemContainer destination = direction == PlayerTransferDirection.PickFromTarget
			? carriedBox
			: facilityContainer;
		if (ReferenceEquals(source, destination))
		{
			message = "Source and destination are the same container.";
			return false;
		}

		int matchingQuantity = GetMatchingQuantity(source, stackKey);
		if (matchingQuantity < quantity)
		{
			message = $"Only {matchingQuantity} matching items are available.";
			return false;
		}

		if (ItemTransferUtility.GetMovableQuantity(
				source,
				destination,
				stackKey.ItemId,
				quantity,
				stackKey.Matches) < quantity)
		{
			message = "The selected item stack cannot fully fit in the destination.";
			return false;
		}

		IItemPickReservable pickReservation = source as IItemPickReservable;
		int reservedQuantity = 0;
		if (pickReservation != null)
		{
			if (pickReservation.GetPickableQuantity(stackKey.ItemId) < quantity)
			{
				message = "Some of this item quantity is already reserved.";
				return false;
			}

			reservedQuantity = pickReservation.ReservePicking(stackKey.ItemId, quantity);
			if (reservedQuantity != quantity)
			{
				if (reservedQuantity > 0)
					pickReservation.ReleaseReservedPick(stackKey.ItemId, reservedQuantity);
				message = "The source quantity could not be reserved.";
				return false;
			}
		}

		CapsuleDock reservedDock = null;
		if (TryReserveDock(target.CapsuleDock, out reservedDock) == false)
		{
			if (reservedQuantity > 0)
				pickReservation.ReleaseReservedPick(stackKey.ItemId, reservedQuantity);
			message = "The capsule dock is reserved by another logistics operation.";
			return false;
		}

		PlayerItemTransferAction action = new(
			target,
			direction,
			stackKey,
			quantity,
			carriedBox,
			facilityContainer,
			pickReservation,
			reservedQuantity,
			reservedDock);
		Observe(worker);
		if (worker.TryQueuePlayerOverrideAction(action, out message))
			return true;

		action.Cancel();
		return false;
	}

	public bool TryRequestWholeBoxTransfer(
		AIWorker worker,
		PlayerInteractionTarget target,
		PlayerTransferDirection direction,
		out string message)
	{
		message = string.Empty;
		if (TryValidateAwaitingWorker(worker, out message) == false ||
			TryValidateTarget(worker, target, direction, out message) == false)
		{
			return false;
		}

		IBoxHandleable targetHandle = target.ResolveBoxHandle();
		CarryBoxAbility carryingAbility = worker.CarryingAbility;
		if (targetHandle == null || carryingAbility == null)
		{
			message = "This interaction cannot move an entire box or capsule.";
			return false;
		}

		BoxBase carriedBox = carryingAbility.CarryingBox;
		WorkerAbility requiredAbility = GetWholeBoxAbility(target, carriedBox);
		if (worker.HasAbility(requiredAbility) == false)
		{
			message = requiredAbility == WorkerAbility.CargoHandling
				? "This worker cannot handle cargo capsules."
				: "This worker cannot carry boxes.";
			return false;
		}

		if (direction == PlayerTransferDirection.PickFromTarget)
		{
			if (carriedBox != null)
			{
				message = "The worker is already carrying a container.";
				return false;
			}

			if (targetHandle.CanGetBox() == false)
			{
				message = "The target has no available container.";
				return false;
			}
		}
		else
		{
			if (carriedBox == null)
			{
				message = "The worker is not carrying a container.";
				return false;
			}

			if (CanTargetAcceptBox(target, carriedBox) == false)
			{
				message = "The target cannot accept this container.";
				return false;
			}
		}

		CapsuleDock reservedDock = null;
		if (TryReserveDock(target.CapsuleDock, out reservedDock) == false)
		{
			message = "The capsule dock is reserved by another logistics operation.";
			return false;
		}

		PlayerWholeBoxTransferAction action = new(target, direction, carriedBox, reservedDock);
		Observe(worker);
		if (worker.TryQueuePlayerOverrideAction(action, out message))
			return true;

		action.Cancel();
		return false;
	}

	private void Observe(AIWorker worker)
	{
		if (worker == null || observedWorkers.Add(worker) == false)
			return;

		worker.PlayerOverrideStateChanged += HandleWorkerStateChanged;
	}

	private void StopObserving(AIWorker worker)
	{
		if (worker == null || observedWorkers.Remove(worker) == false)
			return;

		worker.PlayerOverrideStateChanged -= HandleWorkerStateChanged;
	}

	private void HandleWorkerStateChanged(AIWorker worker)
	{
		OnWorkerStateChanged?.Invoke(worker);
		if (worker == null || worker.IsPlayerOverride == false)
			StopObserving(worker);
	}

	private static bool TryValidateAwaitingWorker(AIWorker worker, out string message)
	{
		message = string.Empty;
		if (worker == null || worker.IsPlayerOverride == false ||
			worker.PlayerOverridePhase != PlayerOverridePhase.AwaitingCommand)
		{
			message = "The worker is not awaiting a player command.";
			return false;
		}

		if (worker.IsOperational == false)
		{
			message = "The worker is not operational.";
			return false;
		}

		if (worker.IsNavigationRescueOverride)
		{
			message = "Navigation rescue control supports movement only.";
			return false;
		}

		return true;
	}

	private static bool TryValidateTarget(
		AIWorker worker,
		PlayerInteractionTarget target,
		PlayerTransferDirection direction,
		out string message)
	{
		message = string.Empty;
		Component component = target.Component;
		if (component == null || component is not IInteractionPoint interactionPoint ||
			component is PackingStation)
		{
			message = "The interaction target is no longer available.";
			return false;
		}

		InteractionKind requiredKind = direction == PlayerTransferDirection.PickFromTarget
			? InteractionKind.Pick
			: InteractionKind.Put;
		if ((target.AvailableKinds & requiredKind) == 0 ||
			(GetKindsAtPosition(interactionPoint, worker.GridPosition) & requiredKind) == 0)
		{
			message = "The worker is not at the required interaction point.";
			return false;
		}

		if (component is IFacility facility && GameContext.HasInstance &&
			GameContext.Instance.FacilityMgr != null &&
			GameContext.Instance.FacilityMgr.IsInvalidating(facility))
		{
			message = "The interaction target is being removed.";
			return false;
		}

		return true;
	}

	private static InteractionKind GetKindsAtPosition(IInteractionPoint interactionPoint, in int3 position)
	{
		InteractionKind result = InteractionKind.None;
		IReadOnlyList<InteractionPoint> points = interactionPoint?.InteractionPoints;
		if (points == null)
			return result;

		for (int i = 0; i < points.Count; ++i)
		{
			InteractionPoint point = points[i];
			if (point != null && point.Point.Equals(position))
				result |= point.InteractionKind & SupportedInteractionKinds;
		}

		return result;
	}

	private static int GetMatchingQuantity(IItemContainer container, in PlayerItemStackKey key)
	{
		if (container?.Stacks == null)
			return 0;

		int quantity = 0;
		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (key.Matches(stack))
				quantity += Mathf.Max(0, stack.Quantity);
		}

		return quantity;
	}

	private static BoxBase ResolveOwningBox(PlayerInteractionTarget target, IItemContainer container)
	{
		if (container is BoxBase directBox)
			return directBox;
		if (target.Component is CapsuleBuffer buffer)
			return buffer.DockedCapsule;
		if (target.Component is CapsuleDock dock)
			return dock.DockedCapsule;
		return null;
	}

	private static bool HasPickingManifest(BoxBase box)
	{
		return box != null &&
			GameContext.HasInstance &&
			GameContext.Instance.OBWorkflowSvc != null &&
			GameContext.Instance.OBWorkflowSvc.TryGetPickingManifest(box, out PickingManifest manifest) &&
			manifest != null &&
			manifest.IsEmpty == false;
	}

	private static bool CanTargetAcceptBox(PlayerInteractionTarget target, BoxBase box)
	{
		if (target.Component is BoxPool pool)
			return pool.CanStoreBox(box);
		if (target.Component is CapsuleDock dock)
			return box is CargoCapsule && dock.CanPutBox();
		return target.ResolveBoxHandle()?.CanPutBox() == true;
	}

	private static WorkerAbility GetWholeBoxAbility(PlayerInteractionTarget target, BoxBase carriedBox)
	{
		return target.Component is CapsuleDock || carriedBox is CargoCapsule
			? WorkerAbility.CargoHandling
			: WorkerAbility.CarryBox;
	}

	private static bool TryReserveDock(CapsuleDock dock, out CapsuleDock reservedDock)
	{
		reservedDock = null;
		if (dock == null)
			return true;

		if (GameContext.HasInstance == false)
		{
			return false;
		}

		GameContext context = GameContext.Instance;
		CapsuleRelocateCoordinator coordinator = context.CapsuleRelocateCoordinator;
		if (coordinator == null || coordinator.TryClaimForPlayer(dock) == false)
			return false;

		if (context.TaskMgr == null || context.TaskMgr.TryPreemptCapsuleDockForPlayer(dock) == false)
		{
			coordinator.ReleasePlayerClaim(dock);
			return false;
		}

		reservedDock = dock;
		return true;
	}

	private static void ReleaseDock(CapsuleDock dock)
	{
		if (dock != null && GameContext.HasInstance)
			GameContext.Instance.CapsuleRelocateCoordinator?.ReleasePlayerClaim(dock);
	}

	private sealed class PlayerItemTransferAction : IPlayerOverrideAction
	{
		private readonly PlayerInteractionTarget target;
		private readonly PlayerTransferDirection direction;
		private readonly PlayerItemStackKey stackKey;
		private readonly int quantity;
		private readonly BoxBase carriedBox;
		private readonly IItemContainer facilityContainer;
		private readonly IItemPickReservable pickReservation;
		private readonly CapsuleDock reservedDock;
		private int reservedQuantity;
		private bool cancelled;

		public WorkActionType ActionType => direction == PlayerTransferDirection.PickFromTarget
			? WorkActionType.PickItem
			: WorkActionType.PutItem;

		public PlayerItemTransferAction(
			PlayerInteractionTarget target,
			PlayerTransferDirection direction,
			PlayerItemStackKey stackKey,
			int quantity,
			BoxBase carriedBox,
			IItemContainer facilityContainer,
			IItemPickReservable pickReservation,
			int reservedQuantity,
			CapsuleDock reservedDock)
		{
			this.target = target;
			this.direction = direction;
			this.stackKey = stackKey;
			this.quantity = quantity;
			this.carriedBox = carriedBox;
			this.facilityContainer = facilityContainer;
			this.pickReservation = pickReservation;
			this.reservedQuantity = reservedQuantity;
			this.reservedDock = reservedDock;
		}

		public bool TryCommit(AIWorker worker, out string message)
		{
			message = string.Empty;
			if (cancelled || worker == null ||
				worker.HasAbility(WorkerAbility.PickingStoring) == false ||
				worker.CarryingAbility?.CarryingBox != carriedBox ||
				TryValidateTarget(worker, target, direction, out message) == false)
			{
				if (string.IsNullOrWhiteSpace(message))
					message = "The carried container changed before the transfer completed.";
				return false;
			}

			IItemContainer currentFacilityContainer = target.ResolveContainer();
			if (ReferenceEquals(currentFacilityContainer, facilityContainer) == false)
			{
				message = "The facility container changed before the transfer completed.";
				return false;
			}

			if (HasPickingManifest(carriedBox) ||
				HasPickingManifest(ResolveOwningBox(target, currentFacilityContainer)))
			{
				message = "Order cargo must be moved with its entire box or capsule.";
				return false;
			}

			IItemContainer source = direction == PlayerTransferDirection.PickFromTarget
				? facilityContainer
				: carriedBox;
			IItemContainer destination = direction == PlayerTransferDirection.PickFromTarget
				? carriedBox
				: facilityContainer;
			if (GetMatchingQuantity(source, stackKey) < quantity ||
				ItemTransferUtility.GetMovableQuantity(
					source,
					destination,
					stackKey.ItemId,
					quantity,
					stackKey.Matches) < quantity)
			{
				message = "The requested quantity is no longer transferable.";
				return false;
			}

			ItemTransferResult result = ItemTransferUtility.MoveItem(new ItemTransferPayload(
				source,
				destination,
				stackKey.ItemId,
				quantity,
				consumeSourcePickReservation: pickReservation != null,
				stackPredicate: stackKey.Matches,
				handlingWorker: worker));
			reservedQuantity = Mathf.Max(0, reservedQuantity - result.Moved);
			if (result.Moved > 0)
				worker.ReportItemHandling(result.ItemId, result.Moved, destination);

			if (result.Kind != TransferResultKind.Complete)
			{
				message = $"Only {result.Moved} of {quantity} items could be moved.";
				return false;
			}

			return true;
		}

		public void Cancel()
		{
			if (cancelled)
				return;

			cancelled = true;
			if (pickReservation != null && reservedQuantity > 0)
				pickReservation.ReleaseReservedPick(stackKey.ItemId, reservedQuantity);
			reservedQuantity = 0;
			ReleaseDock(reservedDock);
		}
	}

	private sealed class PlayerWholeBoxTransferAction : IPlayerOverrideAction
	{
		private readonly PlayerInteractionTarget target;
		private readonly PlayerTransferDirection direction;
		private readonly BoxBase expectedCarriedBox;
		private readonly CapsuleDock reservedDock;
		private bool cancelled;

		public WorkActionType ActionType => direction == PlayerTransferDirection.PickFromTarget
			? WorkActionType.PickBox
			: WorkActionType.PutBox;

		public PlayerWholeBoxTransferAction(
			PlayerInteractionTarget target,
			PlayerTransferDirection direction,
			BoxBase expectedCarriedBox,
			CapsuleDock reservedDock)
		{
			this.target = target;
			this.direction = direction;
			this.expectedCarriedBox = expectedCarriedBox;
			this.reservedDock = reservedDock;
		}

		public bool TryCommit(AIWorker worker, out string message)
		{
			message = string.Empty;
			if (cancelled || worker == null ||
				TryValidateTarget(worker, target, direction, out message) == false)
			{
				return false;
			}

			CarryBoxAbility carryingAbility = worker.CarryingAbility;
			IBoxHandleable targetHandle = target.ResolveBoxHandle();
			if (carryingAbility == null || targetHandle == null)
			{
				message = "The box interaction is no longer available.";
				return false;
			}

			if (worker.HasAbility(GetWholeBoxAbility(target, carryingAbility.CarryingBox)) == false)
			{
				message = "The worker no longer has the required container-handling ability.";
				return false;
			}

			if (direction == PlayerTransferDirection.PickFromTarget)
				return TryPick(worker, carryingAbility, targetHandle, out message);

			return TryPut(worker, carryingAbility, targetHandle, out message);
		}

		public void Cancel()
		{
			if (cancelled)
				return;

			cancelled = true;
			ReleaseDock(reservedDock);
		}

		private static bool TryPick(
			AIWorker worker,
			CarryBoxAbility carryingAbility,
			IBoxHandleable targetHandle,
			out string message)
		{
			message = string.Empty;
			if (carryingAbility.CarryingBox != null || targetHandle.CanGetBox() == false ||
				targetHandle.GetBox(out BoxBase box) == false || box == null)
			{
				message = "The target container is no longer available.";
				return false;
			}

			if (carryingAbility.PutBox(box) == false)
			{
				if (targetHandle.PutBox(box) == false)
					Debug.LogError($"[PlayerOverride] Failed to return rejected container {box.Type} #{box.BoxId}.");
				message = "The worker could not carry the target container.";
				return false;
			}

			worker.ReportBoxHandling(box);
			return true;
		}

		private bool TryPut(
			AIWorker worker,
			CarryBoxAbility carryingAbility,
			IBoxHandleable targetHandle,
			out string message)
		{
			message = string.Empty;
			if (carryingAbility.CarryingBox != expectedCarriedBox || expectedCarriedBox == null ||
				CanTargetAcceptBox(target, expectedCarriedBox) == false ||
				carryingAbility.GetBox(out BoxBase box) == false || box != expectedCarriedBox)
			{
				message = "The carried container or target changed before the transfer completed.";
				return false;
			}

			if (targetHandle.PutBox(box) == false)
			{
				if (carryingAbility.PutBox(box) == false)
					Debug.LogError($"[PlayerOverride] Failed to retain rejected container {box.Type} #{box.BoxId}.");
				message = "The target rejected the carried container.";
				return false;
			}

			worker.ReportBoxHandling(box);
			return true;
		}
	}
}
