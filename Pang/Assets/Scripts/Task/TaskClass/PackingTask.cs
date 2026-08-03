using UnityEngine;
using System;
using static IBaseNode;
using static IBaseNode.NodeState;

public partial class PackingTask : WorkerTask
{
	private static PackingStationService PackingStationService => GameContext.Instance.OBWorkflowSvc.PackingStationService;
	private static OutboundWorkflowService OutboundWorkflowService => GameContext.Instance.OBWorkflowSvc;
	private static OrderManager OrderMgr => GameContext.Instance.OrderMgr;

	private readonly PackingStation targetStation;
	private bool isTaskEnd = false;

	public PackingStation TargetStation => targetStation;

	public PackingTask(PackingStation targetStation) : base(TaskType.Packing)
	{
		this.targetStation = targetStation;
		TrackDependencyBox(targetStation?.WaitingBox?.Box);
	}

	protected override void OnTaskAssigned()
	{
		if (targetStation == null)
			return;

		if (targetStation.CurrentPackingWorker == null)
			targetStation.CurrentPackingWorker = OccupyWorker;

		PackingStationService.OnPackingTaskAssigned(targetStation);
	}

	protected override void OnTaskReturned(AIWorker worker)
	{
		if (targetStation != null && targetStation.CurrentPackingWorker == worker)
			targetStation.CurrentPackingWorker = null;
	}

	protected override void OnTaskInvalidated()
	{
		if (targetStation != null && targetStation.CurrentPackingWorker == OccupyWorker)
			targetStation.CurrentPackingWorker = null;
	}

	protected override void OnTaskReassigned()
	{
		if (targetStation != null)
			targetStation.CurrentPackingWorker = OccupyWorker;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = targetStation != null ? targetStation.CurrentPackingWorker : null;
		return worker != null;
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.PackingStation, InteractionKind.Work, SetPackingStation));

		SelectorNode work = new();

		SequenceNode packEnd = new();
		packEnd.Add(new ActionNode(CheckPackedBoxEnd));
		packEnd.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PackEnd));
		packEnd.Add(new ActionNode(MarkTaskComplete));

		SequenceNode packItems = new();
		packItems.Add(new ActionNode(CheckBoxToPack));
		packItems.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PackItem, PackItems));

		SequenceNode prepareBox = new();
		prepareBox.Add(new ActionNode(CheckWaitingBoxMoveable));
		prepareBox.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PrepareBox));

		work.Add(packEnd);
		work.Add(packItems);
		work.Add(prepareBox);

		root.Add(work);
		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return CanDispatchToWorkerZones(worker, targetStation);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		return ReferenceEquals(targetStation, facility);
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[PackingTask] : {targetStation?.name}";
	}
#endif

	public override string GetStatusSummary()
	{
		if (isTaskEnd)
			return $"Station: {targetStation?.name ?? "None"}\nPacking complete.";

		if (targetStation?.CurrentPackingBox?.IsFullyPacked == true)
			return $"Station: {targetStation.name}\nMoving completed box.";

		WorkLine line = targetStation?.CurrentPackingBox?.Job?.CurrentLine;
		return $"Station: {targetStation?.name ?? "None"}\nCurrent line: {(line != null ? line.ItemID.ToString() : "None")}";
	}

	private static PackingTask GetTask(in BTContext ctx) => ctx.Worker.CurrentTask as PackingTask;

	private static PackingStation GetStation(in BTContext ctx) => GetTask(ctx)?.TargetStation;

	public static NodeState SetPackingStation(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null)
			return Failure;

		ctx.LocalBlackBoard.SetTargetBuilding(station);
		return Success;
	}

	public static NodeState CheckPackedBoxEnd(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station != null && station.CurrentPackingBox?.IsFullyPacked == true)
		{
			if (station.IsBoxMoveableToEnd)
				return Success;

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
			ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
			ctx.Worker.enabled = false;
			return Running;
		}

		return Failure;
	}

	public static NodeState PackEnd(in BTContext ctx)
	{
		var station = GetStation(ctx);
		BoxBase box = station?.CurrentPackingBox?.Box;
		if (station == null || station.EndWorkingBox() == false)
			return Failure;
		ctx.Worker.ReportBoxHandling(box);
		return Success;
	}

	public static NodeState MarkTaskComplete(in BTContext ctx)
	{
		var task = GetTask(ctx);
		if (task == null)
			return Failure;

		task.isTaskEnd = true;
		return Success;
	}

	public static NodeState CheckBoxToPack(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station != null && station.CurrentPackingBox?.IsFullyPacked == false)
			return Success;

		return Failure;
	}

	public static NodeState PackItems(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null)
			return Failure;

		BoxWithOrder box = station.CurrentPackingBox;
		WorkLine line = box?.Job.CurrentLine;
		if (box == null || line == null)
			return Failure;

		if (OutboundWorkflowService.TryGetPackableManifestLine(box.Box, line.RelatedOrderLine, line.ItemID, out PickingManifestLine manifestLine) == false)
		{
			Debug.LogWarning($"[PackingTask] No packable manifest line. box={box.Box?.BoxId}, item={line.ItemID}");
			return Failure;
		}

		int quantityToPack = Math.Min(manifestLine.PackableQuantity, box.Box.GetQuantity(line.ItemID));
		ItemStack packedStack = ItemStack.Rent(
			line.ItemID,
			status: ItemStatus.Packed,
			outboundStage: PackageOutboundStage.None);
		packedStack.AddItem(quantityToPack);

		if (quantityToPack <= 0 || station.CanAcceptStack(packedStack) == false)
		{
			packedStack.Recycle();
			Debug.Log("Station Stack Is Full");
			return quantityToPack <= 0 ? Failure : Running;
		}

		ItemTransferResult result = ItemTransferUtility.MoveItemAsStack(
			box.Box,
			station,
			packedStack,
			handlingWorker: ctx.Worker);
		if (result.Moved > 0)
			ctx.Worker.ReportItemHandling(result.ItemId, result.Moved, station);
		if (result.Kind != TransferResultKind.Complete)
		{
			if (packedStack.Quantity > 0)
				packedStack.Recycle();
			return Failure;
		}

		int manifestPacked = OutboundWorkflowService.ReportPackedFromManifest(box.Box, line.RelatedOrderLine, line.ItemID, result.Moved);
		if (manifestPacked != result.Moved)
		{
			Debug.LogWarning($"[PackingTask] Manifest packing progress mismatch. packed={result.Moved}, applied={manifestPacked}");
		}

		int packedQuantity = OrderMgr.ReportPackagingCompleted(line.RelatedOrderLine, manifestPacked);
		if (packedQuantity != result.Moved)
		{
			Debug.LogWarning($"[PackingTask] Order packing progress mismatch. packed={result.Moved}, manifest={manifestPacked}, applied={packedQuantity}");
		}

		box.Job.MoveToNextLine();
		return Success;
	}

	public static NodeState CheckWaitingBoxMoveable(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null)
			return Failure;

		if (station.IsBoxMoveableToPack)
			return Success;

		if (station.HasWaitingBox == false)
			return Failure;

		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);
		ctx.Worker.enabled = false;
		return Running;
	}

	public static NodeState PrepareBox(in BTContext ctx)
	{
		var station = GetStation(ctx);
		if (station == null || station.PrepareBox() == false)
			return Failure;
		ctx.Worker.ReportBoxHandling(station.CurrentPackingBox?.Box);
		return Success;
	}
}
