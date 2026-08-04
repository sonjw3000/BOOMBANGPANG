using System.Collections.Generic;

public sealed class LaunchBuilding : Building
{
	private readonly LaunchSortPlanner launchSortPlanner;
	private bool isEvaluatingLaunchSortWork;

	private ItemTransferTaskScheduler Scheduler => GameContext.Instance.ItemTransferTaskScheduler;
	private FacilityRuleManager FacilityRuleManager => GameContext.Instance.FacilityRuleMgr;

	internal LaunchSortPlanner LaunchSortPlanner => launchSortPlanner;

	public LaunchBuilding(string displayName, List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Launch)
	{
		launchSortPlanner = new LaunchSortPlanner(this);
	}

	protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		OutboundWorkflowService outbound = GameContext.HasInstance
			? GameContext.Instance.OBWorkflowSvc
			: null;
		if (capsuleBuffer == null ||
			capsuleBuffer.DockState != CapsuleDockState.OBStandby ||
			capsuleBuffer.DockedCapsule == null ||
			outbound == null ||
			(capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OBStandby &&
			 capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OB) ||
			outbound.HasDispatchBlockingCargo(capsuleBuffer) ||
			outbound.HasCompleteDispatchManifest(capsuleBuffer) == false)
		{
			return false;
		}

		float workflowThreshold = GameContext.HasInstance && GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.CargoPortThresholdPercent
			: CapsuleThresholdPercent;
		float threshold = OverrideCapsuleThreshold ? CapsuleThresholdPercent : workflowThreshold;
		return capsuleBuffer.FilledPercent >= threshold;
	}

	internal void ReevaluateOutboundBuffer(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null ||
			capsuleBuffer.DockState != CapsuleDockState.OBStandby ||
			GameContext.HasInstance == false ||
			GameContext.Instance.OBWorkflowSvc == null)
		{
			return;
		}

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		outbound.RejectInvalidPackedCargo(capsuleBuffer);
		if (outbound.HasDispatchBlockingCargo(capsuleBuffer) ||
			outbound.HasCompleteDispatchManifest(capsuleBuffer) == false)
		{
			if (capsuleBuffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
				capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.OBStandby);
			return;
		}

		base.OnCapsuleBufferContentChanged(capsuleBuffer);
	}

	internal bool TryPrepareOutboundDispatch(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer?.DockedCapsule == null ||
			GameContext.HasInstance == false ||
			GameContext.Instance.OBWorkflowSvc == null)
		{
			return false;
		}

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		outbound.RejectInvalidPackedCargo(capsuleBuffer);
		if (outbound.HasDispatchBlockingCargo(capsuleBuffer) ||
			outbound.HasCompleteDispatchManifest(capsuleBuffer) == false)
		{
			if (capsuleBuffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
				capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.OBStandby);
			EvaluateLaunchSortWork();
			return false;
		}

		return IsBufferOutboundReady(capsuleBuffer);
	}

	protected override void OnIBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		base.OnIBDockDocked(dock, capsule);
		EvaluateLaunchSortWork();
	}

	protected override void OnOBStandbyDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		base.OnOBStandbyDockDocked(dock, capsule);
		EvaluateLaunchSortWork();
	}


	protected override void OnCapsuleDockStateChanged(CapsuleBuffer capsuleBuffer)
	{
		base.OnCapsuleDockStateChanged(capsuleBuffer);
		EvaluateLaunchSortWork();
	}


	protected override void OnRegistered()
	{
		Scheduler?.Register(
			RuntimeBuildingId,
			ItemTransferScheduleMode.LaunchSort,
			WorkerTask.TaskType.LaunchSort,
			TryBuildLaunchSortTask);

		SubscribeFacilityRuleEvents();
		EvaluateLaunchSortWork();
	}

	protected override void OnUnregistered()
	{
		UnsubscribeFacilityRuleEvents();
		Scheduler?.Unregister(RuntimeBuildingId, ItemTransferScheduleMode.LaunchSort);
	}

	internal void EvaluateLaunchSortWork()
	{
		if (RuntimeBuildingId == 0 || isEvaluatingLaunchSortWork)
			return;

		isEvaluatingLaunchSortWork = true;
		try
		{
			RejectInvalidOutboundStandbyCargo();

			if (Scheduler == null)
				return;

			if (launchSortPlanner.HasSortableWork())
				Scheduler.MarkDirty(RuntimeBuildingId, ItemTransferScheduleMode.LaunchSort);
			else
				Scheduler.ClearDirty(RuntimeBuildingId, ItemTransferScheduleMode.LaunchSort);
		}
		finally
		{
			isEvaluatingLaunchSortWork = false;
		}
	}

	private void RejectInvalidOutboundStandbyCargo()
	{
		OutboundWorkflowService outbound = GameContext.HasInstance
			? GameContext.Instance.OBWorkflowSvc
			: null;
		if (outbound == null)
			return;

		for (int i = 0; i < OccupiedCapsuleBuffers.Count; ++i)
		{
			CapsuleBuffer buffer = OccupiedCapsuleBuffers[i];
			if (buffer?.DockedCapsule == null ||
				buffer.DockState != CapsuleDockState.OBStandby ||
				(buffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OBStandby &&
				 buffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OB))
			{
				continue;
			}

			outbound.RejectInvalidPackedCargo(buffer);
			if ((outbound.HasDispatchBlockingCargo(buffer) ||
				 outbound.HasCompleteDispatchManifest(buffer) == false) &&
				buffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
			{
				buffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.OBStandby);
			}
		}
	}

	private ItemTransferScheduleResult TryBuildLaunchSortTask(
		ItemTransferScheduleRequest request,
		out WorkerTask task)
	{
		task = null;
		if (request.Worker == null || request.Worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		if (launchSortPlanner.HasSortableWork() == false)
			return ItemTransferScheduleResult.NoWork;

		if (launchSortPlanner.HasSortableWork(request.Worker) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		task = new ItemTransferTask(
			WorkerTask.TaskType.LaunchSort,
			new ItemTransferJob(
				launchSortPlanner,
				TransferObjectType.Item,
				TransferObjectType.Item,
				RuntimeBuildingId,
				request.Worker));
		return ItemTransferScheduleResult.Scheduled;
	}

	private void SubscribeFacilityRuleEvents()
	{
		if (FacilityRuleManager == null)
			return;

		FacilityRuleManager.OnPresetChanged += HandlePresetChanged;
		FacilityRuleManager.OnPresetDeleted += HandlePresetDeleted;
		FacilityRuleManager.OnFacilityRulePresetApplied += HandleFacilityRulePresetApplied;
		FacilityRuleManager.OnPresetsRebuilt += HandlePresetsRebuilt;
	}

	private void UnsubscribeFacilityRuleEvents()
	{
		if (FacilityRuleManager == null)
			return;

		FacilityRuleManager.OnPresetChanged -= HandlePresetChanged;
		FacilityRuleManager.OnPresetDeleted -= HandlePresetDeleted;
		FacilityRuleManager.OnFacilityRulePresetApplied -= HandleFacilityRulePresetApplied;
		FacilityRuleManager.OnPresetsRebuilt -= HandlePresetsRebuilt;
	}

	private void HandlePresetChanged(FacilityRulePreset preset) => EvaluateLaunchSortWork();
	private void HandlePresetDeleted(uint presetId) => EvaluateLaunchSortWork();
	private void HandlePresetsRebuilt() => EvaluateLaunchSortWork();

	private void HandleFacilityRulePresetApplied(IFacility facility, uint previousPresetId, uint presetId)
	{
		if (facility == null)
			return;

		for (int i = 0; i < OccupiedFacilities.Count; ++i)
		{
			if (ReferenceEquals(OccupiedFacilities[i], facility))
			{
				EvaluateLaunchSortWork();
				return;
			}
		}
	}
}
