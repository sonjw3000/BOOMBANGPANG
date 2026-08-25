using System.Collections.Generic;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public enum ItemTransferPhase
{
	Collect,
	Place,
}

public enum TransferObjectType
{
	Item,
	Box,
}

public sealed class ItemTransferJob
{
	public readonly IItemTransferPlanner Planner;
	public readonly TransferObjectType CollectType;
	public readonly TransferObjectType PlaceType;
	public readonly uint BuildingId;
	public readonly AIWorker PreferredWorker;

	public ItemTransferJob(
		IItemTransferPlanner planner,
		TransferObjectType collectType,
		TransferObjectType placeType,
		uint buildingId = 0,
		AIWorker preferredWorker = null)
	{
		Planner = planner;
		CollectType = collectType;
		PlaceType = placeType;
		BuildingId = buildingId;
		PreferredWorker = preferredWorker;
	}
}

public sealed class ItemTransferCollectedLine
{
	public readonly WorkLine CollectLine;
	public int PlacedQuantity { get; private set; }
	public int RemainingQuantity => Mathf.Max(0, CollectLine.Quantity - PlacedQuantity);
	public bool IsPlaceComplete => RemainingQuantity <= 0;

	public ItemTransferCollectedLine(WorkLine collectLine)
	{
		CollectLine = collectLine;
	}

	public void ReportPlaced(int quantity)
	{
		PlacedQuantity = Mathf.Clamp(PlacedQuantity + quantity, 0, CollectLine.Quantity);
	}
}

public interface IItemTransferPlanner
{
	WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line);
	WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result);

	WorkPlanResult TryGetPlaceLine(AIWorker worker, uint buildingId, WorkLine collectedLine, int remainingQuantity, out WorkLine line);
	WorkPlanResult OnPlaceCompleted(AIWorker worker, WorkLine collectedLine, WorkLine placeLine, ItemTransferResult result);
}

public interface IItemTransferTaskInvalidationHandler
{
	void OnTaskInvalidated(ItemTransferTask task);
}

public interface IItemTransferCollectGate
{
	WorkPlanResult EvaluateBeforeCollect(AIWorker worker, WorkLine line, out bool allowTransfer);
}

public interface IItemTransferTaskCompletionHandler
{
	void OnTaskCompleted(ItemTransferTask task);
}

public sealed class ItemTransferTask : WorkerTask
{
	private readonly ItemTransferJob job;
	private readonly List<ItemTransferCollectedLine> collectedLines = new();
	private readonly List<CapsuleBuffer> retainedPickingOutputBuffers = new();

	private ItemTransferPhase phase = ItemTransferPhase.Collect;
	private WorkLine currentLine;
	private int placingLineIndex;
	private bool isTaskEnd;
	private bool isReevaluatingFacility;

	public uint BuildingId => job != null ? job.BuildingId : 0;
	public ItemTransferPhase Phase => phase;
	public WorkLine CurrentLine => currentLine;
	public IReadOnlyList<ItemTransferCollectedLine> CollectedLines => collectedLines;
	internal IReadOnlyList<CapsuleBuffer> RetainedPickingOutputBuffers => retainedPickingOutputBuffers;
	internal bool IsReevaluatingFacility => isReevaluatingFacility;

	public ItemTransferTask(TaskType taskType, ItemTransferJob job) : base(taskType)
	{
		this.job = job;

		if (IsSupportedTaskType(taskType) == false)
			Debug.LogWarning($"[ItemTransferTask] {taskType} is not a standard item-transfer task type.");
	}

	protected override void OnTaskAssigned()
	{
		if (WorkerCarryBox == null)
			Debug.LogError($"[ItemTransferTask] No carry box ability but assigned to {Type}.");

		if (Type == TaskType.WasteCollection && job?.Planner is WasteCollectionPlanner wastePlanner)
			wastePlanner.OnTaskAssigned(this, OccupyWorker);
	}

	protected override void OnTaskInvalidated()
	{
		if (job?.Planner is IItemTransferTaskInvalidationHandler handler)
			handler.OnTaskInvalidated(this);

		ReleaseRetainedPickingOutputsForRouting();
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new();

		root.Add(BuildPlaceNode(TransferObjectType.Box, WorkActionType.PutBox));
		root.Add(BuildPlaceNode(TransferObjectType.Item, WorkActionType.PutItem));
		root.Add(BuildCollectNode(TransferObjectType.Box, WorkActionType.PickBox));
		root.Add(BuildCollectNode(TransferObjectType.Item, WorkActionType.PickItem));

		return root;
	}

	private static SequenceNode BuildCollectNode(TransferObjectType transferType, WorkActionType workActionType)
	{
		SequenceNode collect = new();
		collect.Add(new ActionNode(CheckPhaseCollect));
		collect.Add(new ActionNode((in BTContext ctx) => CheckTransferType(ctx, ItemTransferPhase.Collect, transferType)));

		if (transferType == TransferObjectType.Item)
			collect.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		else
			collect.Add(AIWorker.ReturnBox());

		collect.Add(new ActionNode(SetCollectingPosition));
		collect.Add(AIWorker.MoveToTarget(WorkerStatusTarget.WorkTarget, InteractionKind.Pick));
		collect.Add(AIWorker.BuildWorkTimeInteract(workActionType, Collect));
		return collect;
	}

	private static SequenceNode BuildPlaceNode(TransferObjectType transferType, WorkActionType workActionType)
	{
		SequenceNode place = new();
		place.Add(new ActionNode(CheckPhasePlace));
		place.Add(new ActionNode(SetPlacingPosition));
		place.Add(new ActionNode((in BTContext ctx) => CheckTransferType(ctx, ItemTransferPhase.Place, transferType)));
		place.Add(AIWorker.MoveToTarget(WorkerStatusTarget.WorkTarget, InteractionKind.Put));
		place.Add(AIWorker.BuildWorkTimeInteract(workActionType, Place));
		return place;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return worker != null &&
			(BuildingId == 0 || worker.PrimaryBuildingId == BuildingId) &&
			CanDispatchToWorkerZones(worker, currentLine?.Target);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		return ReferencesFacility(currentLine, facility) || RetainsPickingOutput(facility);
	}

	internal override FacilityTaskInvalidationAction HandleFacilityInvalidating(
		IFacility facility,
		in FacilityInvalidationContext context)
	{
		bool referencesCurrentLine = ReferencesFacility(currentLine, facility);
		bool retainsPickingOutput = RetainsPickingOutput(facility);
		if (referencesCurrentLine == false && retainsPickingOutput == false)
			return FacilityTaskInvalidationAction.None;

		if (retainsPickingOutput && facility is CapsuleBuffer invalidatingBuffer)
		{
			retainedPickingOutputBuffers.Remove(invalidatingBuffer);
			MarkPickingOutputDirty(invalidatingBuffer);
		}

		if (referencesCurrentLine == false)
			return FacilityTaskInvalidationAction.None;

		if (phase == ItemTransferPhase.Collect)
			return FacilityTaskInvalidationAction.Invalidate;

		if (job?.Planner is IItemTransferTaskInvalidationHandler handler)
		{
			isReevaluatingFacility = true;
			try
			{
				handler.OnTaskInvalidated(this);
			}
			finally
			{
				isReevaluatingFacility = false;
			}
		}

		currentLine = null;
		return FacilityTaskInvalidationAction.Reevaluate;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		if (Type == TaskType.WasteCollection && job?.Planner is WasteCollectionPlanner wastePlanner)
			return wastePlanner.TryGetPreferredWorker(this, out worker);

		worker = job?.PreferredWorker;
		return worker != null;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[ItemTransferTask:{Type}] {job?.CollectType} -> {job?.PlaceType}, Phase={phase}";
	}
#endif

	public override string GetStatusSummary()
	{
		if (isTaskEnd)
			return $"{Type} item transfer complete.";

		string targetName = currentLine?.TargetName ?? "Pending";
		return $"Item Transfer ({Type})\n{job?.CollectType} -> {job?.PlaceType}\nPhase: {phase}\nTarget: {targetName}";
	}

	public static NodeState CheckPhaseCollect(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		return task != null && task.phase == ItemTransferPhase.Collect ? Success : Failure;
	}

	public static NodeState CheckPhasePlace(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		return task != null && task.phase == ItemTransferPhase.Place ? Success : Failure;
	}

	private static NodeState CheckTransferType(in BTContext ctx, ItemTransferPhase phase, TransferObjectType transferType)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job == null)
			return Failure;

		TransferObjectType currentType = phase == ItemTransferPhase.Collect
			? task.job.CollectType
			: task.job.PlaceType;

		return currentType == transferType ? Success : Failure;
	}

	public static NodeState SetCollectingPosition(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null)
			return Failure;

		if (task.currentLine == null)
		{
			WorkPlanResult result = task.job.Planner.TryGetCollectLine(ctx.Worker, task.BuildingId, out WorkLine nextLine);
			if (result == WorkPlanResult.Issued)
			{
				if (task.IsValidLine(nextLine, WorkLineAction.Pick, task.job.CollectType) == false)
				{
					Debug.LogError($"[ItemTransferTask] Invalid collect line for {task.Type}.");
					return Failure;
				}

				task.currentLine = nextLine;
			}
			else
			{
				return task.ApplyPlanResult(ctx, result);
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.currentLine.Target);
		return Success;
	}

	public static NodeState Collect(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null || task.currentLine == null)
			return Failure;

		WorkLine line = task.currentLine;
		if (task.job.Planner is IItemTransferCollectGate collectGate)
		{
			WorkPlanResult gateResult = collectGate.EvaluateBeforeCollect(ctx.Worker, line, out bool allowTransfer);
			if (allowTransfer == false)
			{
				task.currentLine = null;
				return task.ApplyPlanResult(ctx, gateResult);
			}
		}

		ItemTransferResult result = task.job.CollectType == TransferObjectType.Box
			? MoveCollectBox(ctx.Worker, line)
			: MoveCollectItem(ctx.Worker, line);
		if (task.job.CollectType == TransferObjectType.Item && result.Moved > 0)
			ctx.Worker.ReportItemHandling(result.ItemId, result.Moved, ctx.Worker.CarryingAbility?.CarryingBox);

		if (result.Moved > 0)
			task.collectedLines.Add(new ItemTransferCollectedLine(CopyLineWithQuantity(line, result.Moved)));

		line.CompleteQuantity += result.Moved;
		task.currentLine = null;

		WorkPlanResult next = task.job.Planner.OnCollectCompleted(ctx.Worker, line, result);
		return task.ApplyPlanResult(ctx, next);
	}

	public static NodeState SetPlacingPosition(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null)
			return Failure;

		if (task.currentLine == null)
		{
			if (task.TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine) == false)
				return task.ApplyPlanResult(ctx, WorkPlanResult.Completed);

			int remainingQuantity = task.GetRemainingPlaceQuantity(collectedLine);
			WorkPlanResult result = task.job.Planner.TryGetPlaceLine(
				ctx.Worker,
				task.BuildingId,
				collectedLine.CollectLine,
				remainingQuantity,
				out WorkLine nextLine);

			if (result == WorkPlanResult.Issued)
			{
				if (task.IsValidLine(nextLine, WorkLineAction.Put, task.job.PlaceType) == false ||
					(task.job.CollectType == TransferObjectType.Item &&
						task.job.PlaceType == TransferObjectType.Item &&
						nextLine.Quantity > collectedLine.RemainingQuantity))
				{
					Debug.LogError($"[ItemTransferTask] Invalid place line for {task.Type}.");
					return Failure;
				}

				task.currentLine = nextLine;
				task.RetainPickingOutput(nextLine);
			}
			else
			{
				return task.ApplyPlanResult(ctx, result);
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.currentLine.Target);
		return Success;
	}

	public static NodeState Place(in BTContext ctx)
	{
		ItemTransferTask task = ctx.Worker.CurrentTask as ItemTransferTask;
		if (task?.job?.Planner == null || task.currentLine == null)
			return Failure;

		if (task.TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine) == false)
			return task.ApplyPlanResult(ctx, WorkPlanResult.Completed);

		WorkLine placeLine = task.currentLine;
		ItemTransferResult result = task.job.PlaceType == TransferObjectType.Box
			? MovePlaceBox(ctx.Worker, placeLine)
			: MovePlaceItem(ctx.Worker, placeLine);
		if (task.job.PlaceType == TransferObjectType.Item && result.Moved > 0)
			ctx.Worker.ReportItemHandling(result.ItemId, result.Moved, placeLine.Container);

		placeLine.CompleteQuantity += result.Moved;
		task.ReportPlaceProgress(ctx.Worker, collectedLine, result);

		task.currentLine = null;
		if (collectedLine.IsPlaceComplete)
			task.placingLineIndex += 1;

		WorkPlanResult next = task.job.Planner.OnPlaceCompleted(ctx.Worker, collectedLine.CollectLine, placeLine, result);
		return task.ApplyPlanResult(ctx, next);
	}

	private NodeState ApplyPlanResult(in BTContext ctx, WorkPlanResult result)
	{
		switch (result)
		{
			case WorkPlanResult.Issued:
				return Success;

			case WorkPlanResult.SwitchPhase:
				SwitchPhase();
				return Failure;

			case WorkPlanResult.Completed:
				if (phase == ItemTransferPhase.Collect && HasPendingPlaceLines())
				{
					phase = ItemTransferPhase.Place;
					currentLine = null;
					return Failure;
				}

				isTaskEnd = true;
				currentLine = null;
				return Failure;

			case WorkPlanResult.Waiting:
			default:
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				ctx.Worker.SetWorkerTarget(WorkerStatusTarget.WorkTarget);
				return AIWorker.KeepTaskWaiting(ctx);
		}
	}

	private int GetRemainingPlaceQuantity(ItemTransferCollectedLine collectedLine)
	{
		if (job == null || collectedLine == null)
			return 0;

		if (job.CollectType == TransferObjectType.Item && job.PlaceType == TransferObjectType.Item)
			return collectedLine.RemainingQuantity;

		return int.MaxValue;
	}

	private static ItemTransferResult MoveCollectItem(AIWorker worker, WorkLine line)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (line == null || line.Container == null || box == null || line.Quantity <= 0)
			return new(new ItemTransferPayload(line?.Container, box, line != null ? line.ItemID : 0, 0), 0);

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		if (remainingQuantity <= 0)
			return new(new ItemTransferPayload(line.Container, box, line.ItemID, remainingQuantity), 0);

		return ItemTransferUtility.MoveItem(new(
			line.Container,
			box,
			line.ItemID,
			remainingQuantity,
			consumeSourcePickReservation: line.ConsumeSourcePickReservation && line.Container is IItemPickReservable,
			stackPredicate: stack =>
				(line.RequiredStatus.HasValue == false || stack.HasStatus(line.RequiredStatus.Value)) &&
				(line.RequiredQuality.HasValue == false || stack.HasQuality(line.RequiredQuality.Value)) &&
				(line.ExcludedQuality.HasValue == false || stack.HasQuality(line.ExcludedQuality.Value) == false)));
	}

	private static ItemTransferResult MovePlaceItem(AIWorker worker, WorkLine line)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		if (line == null || line.Container == null || box == null || line.Quantity <= 0)
			return new(new ItemTransferPayload(box, line?.Container, line != null ? line.ItemID : 0, 0), 0);

		int remainingQuantity = line.Quantity - line.CompleteQuantity;
		if (remainingQuantity <= 0)
			return new(new ItemTransferPayload(box, line.Container, line.ItemID, remainingQuantity), 0);

		return ItemTransferUtility.MoveItem(new(
			box,
			line.Container,
			line.ItemID,
			remainingQuantity,
			stackPredicate: stack =>
				(line.RequiredStatus.HasValue == false || stack.HasStatus(line.RequiredStatus.Value)) &&
				(line.RequiredQuality.HasValue == false || stack.HasQuality(line.RequiredQuality.Value)) &&
				(line.ExcludedQuality.HasValue == false || stack.HasQuality(line.ExcludedQuality.Value) == false),
			handlingWorker: worker));
	}

	private static ItemTransferResult MoveCollectBox(AIWorker worker, WorkLine line)
	{
		ItemTransferPayload payload = CreateBoxPayload(line);
		if (worker?.CarryingAbility == null || line?.Target is not BoxInteraction boxInteraction)
			return new(payload, 0);

		if (worker.CarryingAbility.CarryingBox != null)
			return new(payload, 0);

		if (boxInteraction.GetBox(out BoxBase box) == false || worker.CarryingAbility.PutBox(box) == false)
		{
			if (box != null)
				boxInteraction.PutBox(box);

			return new(payload, 0);
		}

		worker.ReportBoxHandling(box);
		return new(payload, 1);
	}

	private static ItemTransferResult MovePlaceBox(AIWorker worker, WorkLine line)
	{
		ItemTransferPayload payload = CreateBoxPayload(line);
		if (worker?.CarryingAbility == null || line?.Target is not BoxInteraction boxInteraction)
			return new(payload, 0);

		if (worker.CarryingAbility.GetBox(out BoxBase box) == false)
			return new(payload, 0);

		if (boxInteraction.PutBox(box) == false)
		{
			worker.CarryingAbility.PutBox(box);
			return new(payload, 0);
		}

		worker.ReportBoxHandling(box);
		return new(payload, 1);
	}

	private static ItemTransferPayload CreateBoxPayload(WorkLine line)
	{
		return new(null, null, line != null ? line.ItemID : 0, line != null ? line.Quantity : 1);
	}

	private void SwitchPhase()
	{
		currentLine = null;
		if (phase == ItemTransferPhase.Collect)
		{
			phase = ItemTransferPhase.Place;
			return;
		}

		phase = ItemTransferPhase.Collect;
		ClearPlacedLines();
	}

	private void ReportPlaceProgress(AIWorker worker, ItemTransferCollectedLine collectedLine, ItemTransferResult result)
	{
		if (collectedLine == null || result.Moved <= 0 || job == null)
			return;

		if (job.PlaceType == TransferObjectType.Box)
		{
			ReportAllCollectedLinesPlaced();
			return;
		}

		if (job.CollectType == TransferObjectType.Box)
		{
			if (IsWorkerCarryBoxEmpty(worker))
				collectedLine.ReportPlaced(collectedLine.RemainingQuantity);

			return;
		}

		collectedLine.ReportPlaced(result.Moved);
	}

	private void ReportAllCollectedLinesPlaced()
	{
		for (int i = 0; i < collectedLines.Count; ++i)
			collectedLines[i].ReportPlaced(collectedLines[i].RemainingQuantity);
	}

	private static bool IsWorkerCarryBoxEmpty(AIWorker worker)
	{
		BoxBase box = worker?.CarryingAbility?.CarryingBox;
		return box == null || box.Stacks.Count <= 0;
	}

	private bool TryGetCurrentCollectedLine(out ItemTransferCollectedLine collectedLine)
	{
		while (placingLineIndex < collectedLines.Count && collectedLines[placingLineIndex].IsPlaceComplete)
			placingLineIndex += 1;

		collectedLine = placingLineIndex < collectedLines.Count ? collectedLines[placingLineIndex] : null;
		return collectedLine != null;
	}

	private bool HasPendingPlaceLines()
	{
		for (int i = 0; i < collectedLines.Count; ++i)
		{
			if (collectedLines[i].IsPlaceComplete == false)
				return true;
		}

		return false;
	}

	private void ClearPlacedLines()
	{
		for (int i = collectedLines.Count - 1; i >= 0; --i)
		{
			if (collectedLines[i].IsPlaceComplete)
				collectedLines.RemoveAt(i);
		}

		placingLineIndex = 0;
	}

	private static WorkLine CopyLineWithQuantity(WorkLine source, int quantity)
	{
		WorkLine line = new(
			source.Action,
			source.Container,
			source.Target,
			source.ItemID,
			quantity,
			source.RelatedOrderLine,
			source.RequiredStatus,
			source.RequiredQuality,
			source.ConsumeSourcePickReservation,
			source.ExcludedQuality);
		line.CompleteQuantity = quantity;
		return line;
	}

	private bool IsValidLine(WorkLine line, WorkLineAction action, TransferObjectType transferType)
	{
		if (line == null || line.Action != action || line.Target == null || line.Quantity <= 0)
			return false;

		return transferType switch
		{
			TransferObjectType.Item => line.Container != null,
			TransferObjectType.Box => line.Target is BoxInteraction,
			_ => false,
		};
	}

	private static bool IsSupportedTaskType(TaskType taskType)
	{
		return taskType is TaskType.Picking or TaskType.Storing or TaskType.PackingInput or TaskType.PackingOutput or TaskType.LaunchSort or TaskType.WasteCollection;
	}

	internal void NotifyPlannerCompleted()
	{
		if (job?.Planner is IItemTransferTaskCompletionHandler handler)
			handler.OnTaskCompleted(this);

		ReleaseRetainedPickingOutputsForRouting();
	}

	internal bool RetainsPickingOutput(IFacility facility)
	{
		if (Type != TaskType.Picking || facility is not CapsuleBuffer buffer)
			return false;

		return retainedPickingOutputBuffers.Contains(buffer);
	}

	internal ItemTransferTaskSaveData CaptureState()
	{
		AIWorker preferredWorker = job?.PreferredWorker ?? OccupyWorker;
		return new ItemTransferTaskSaveData
		{
			BuildingId = BuildingId,
			PreferredWorkerId = preferredWorker != null ? preferredWorker.WorkerID : 0,
			Phase = phase,
		};
	}

	internal bool RestoreCollectedPackingPayload(BoxBase payloadBox)
	{
		return RestoreCollectedPackingPayloadForPhase(payloadBox, ItemTransferPhase.Place);
	}

	internal bool RestoreCollectedPackingPayloadForPhase(
		BoxBase payloadBox,
		ItemTransferPhase restoredPhase)
	{
		if (payloadBox == null ||
			(Type != TaskType.PackingInput && Type != TaskType.PackingOutput))
		{
			return false;
		}

		List<WorkLine> restoredLines = new();
		for (int i = 0; i < payloadBox.Stacks.Count; ++i)
		{
			ItemStack candidate = payloadBox.Stacks[i];
			if (candidate == null ||
				candidate.Quantity <= 0 ||
				candidate.HasQuality(ItemQuality.Waste) ||
				(Type == TaskType.PackingInput && candidate.HasStatus(ItemStatus.Labeled) == false) ||
				(Type == TaskType.PackingOutput && candidate.HasStatus(ItemStatus.Packed) == false))
			{
				continue;
			}

			restoredLines.Add(new WorkLine(
				WorkLineAction.Pick,
				payloadBox,
				payloadBox,
				candidate.ItemID,
				Type == TaskType.PackingInput ? candidate.Quantity : 1,
				requiredStatus: Type == TaskType.PackingInput ? ItemStatus.Labeled : ItemStatus.Packed,
				consumeSourcePickReservation: false,
				excludedQuality: ItemQuality.Waste));

			// PackingOutput collected one physical box. Its planner scans that box for
			// every packed stack while placing, so one synthetic collected line owns it.
			if (Type == TaskType.PackingOutput)
				break;
		}

		if (restoredLines.Count <= 0)
			return false;

		RestoreCollectedLines(restoredLines, restoredPhase);
		return true;
	}

	internal bool RestoreCollectedLaunchSortPayload(BoxBase payloadBox)
	{
		if (payloadBox == null ||
			payloadBox.Type != BoxType.Personal ||
			GameContext.HasInstance == false ||
			GameContext.Instance.OBWorkflowSvc == null)
			return false;

		Dictionary<uint, int> normalPackedQuantityByItemId = new();
		List<WorkLine> wasteLines = new();
		for (int i = 0; i < payloadBox.Stacks.Count; ++i)
		{
			ItemStack stack = payloadBox.Stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (stack.HasQuality(ItemQuality.Waste))
			{
				wasteLines.Add(new WorkLine(
					WorkLineAction.Pick,
					payloadBox,
					payloadBox,
					stack.ItemID,
					stack.Quantity,
					requiredStatus: stack.Status,
					requiredQuality: ItemQuality.Waste,
					consumeSourcePickReservation: false));
				continue;
			}

			if (stack.Status != ItemStatus.Packed)
				return false;

			normalPackedQuantityByItemId[stack.ItemID] =
				normalPackedQuantityByItemId.GetValueOrDefault(stack.ItemID) + stack.Quantity;
		}

		List<WorkLine> restoredLines = new(wasteLines);
		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		if (outbound.TryGetPickingManifest(payloadBox, out PickingManifest manifest))
		{
			for (int i = 0; i < manifest.Lines.Count; ++i)
			{
				PickingManifestLine manifestLine = manifest.Lines[i];
				if (manifestLine?.OrderLine == null || manifestLine.PackedQuantity <= 0)
					return false;

				int available = normalPackedQuantityByItemId.GetValueOrDefault(manifestLine.ItemId);
				if (available < manifestLine.PackedQuantity)
					return false;

				normalPackedQuantityByItemId[manifestLine.ItemId] = available - manifestLine.PackedQuantity;
				restoredLines.Add(new WorkLine(
					WorkLineAction.Pick,
					payloadBox,
					payloadBox,
					manifestLine.ItemId,
					manifestLine.PackedQuantity,
					manifestLine.OrderLine,
					ItemStatus.Packed,
					consumeSourcePickReservation: false));
			}
		}

		foreach (var entry in normalPackedQuantityByItemId)
		{
			if (entry.Value > 0)
				return false;
		}

		if (restoredLines.Count <= 0)
			return false;

		RestoreCollectedLines(restoredLines, ItemTransferPhase.Place);
		return true;
	}

	private void RestoreCollectedLines(
		IReadOnlyList<WorkLine> restoredLines,
		ItemTransferPhase restoredPhase)
	{
		collectedLines.Clear();
		for (int i = 0; i < restoredLines.Count; ++i)
			collectedLines.Add(new ItemTransferCollectedLine(restoredLines[i]));

		phase = restoredPhase;
		currentLine = null;
		placingLineIndex = 0;
		isTaskEnd = false;
	}

	internal bool RestoreCollectedWastePayload(BoxBase payloadBox)
	{
		if (payloadBox == null)
			return false;

		collectedLines.Clear();
		for (int i = 0; i < payloadBox.Stacks.Count; ++i)
		{
			ItemStack stack = payloadBox.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.HasQuality(ItemQuality.Waste) == false)
				continue;

			WorkLine restoredLine = new(
				WorkLineAction.Pick,
				payloadBox,
				payloadBox,
				stack.ItemID,
				stack.Quantity,
				requiredStatus: stack.Status,
				requiredQuality: ItemQuality.Waste,
				consumeSourcePickReservation: false);
			collectedLines.Add(new ItemTransferCollectedLine(restoredLine));
		}

		if (collectedLines.Count <= 0)
			return false;

		phase = ItemTransferPhase.Place;
		currentLine = null;
		placingLineIndex = 0;
		isTaskEnd = false;
		return true;
	}

	private static bool ReferencesFacility(WorkLine line, IFacility facility)
	{
		return facility != null && line != null &&
			(ReferenceEquals(line.Target, facility) || ReferenceEquals(line.Container, facility));
	}

	private void RetainPickingOutput(WorkLine line)
	{
		if (Type != TaskType.Picking ||
			line?.Target is not CapsuleBuffer buffer ||
			retainedPickingOutputBuffers.Contains(buffer))
		{
			return;
		}

		retainedPickingOutputBuffers.Add(buffer);
	}

	private void ReleaseRetainedPickingOutputsForRouting()
	{
		if (retainedPickingOutputBuffers.Count <= 0)
			return;

		List<CapsuleBuffer> releasedBuffers = new(retainedPickingOutputBuffers);
		retainedPickingOutputBuffers.Clear();
		for (int i = 0; i < releasedBuffers.Count; ++i)
			MarkPickingOutputDirty(releasedBuffers[i]);
	}

	private static void MarkPickingOutputDirty(CapsuleBuffer buffer)
	{
		if (buffer == null || GameContext.HasInstance == false)
			return;

		CapsuleRelocateCoordinator coordinator = GameContext.Instance.ExistingCapsuleRelocateCoordinator;
		coordinator?.CancelPendingRequests(buffer);
		coordinator?.MarkDirty(buffer);
	}
}
