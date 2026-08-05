using System;
using Assets.Scripts.AI.BT;
using Unity.Mathematics;
using UnityEngine;
using OverridePhase = PlayerOverridePhase;

public enum WorkerControlMode
{
	Automatic = 0,
	PlayerOverride = 1,
}

public enum PlayerOverridePhase
{
	None = 0,
	Moving = 1,
	AwaitingCommand = 2,
	ExecutingInteraction = 3,
	WaitingForAirlock = 4,
	UsingAirlock = 5,
}

internal interface IPlayerOverrideAction
{
	WorkActionType ActionType { get; }

	bool TryCommit(AIWorker worker, out string message);

	void Cancel();
}

public abstract partial class AIWorker
{
	private readonly BlackBoard playerOverrideBlackBoard = new();
	private WorkerControlMode controlMode = WorkerControlMode.Automatic;
	private PlayerOverridePhase playerOverridePhase = OverridePhase.None;
	private IPlayerOverrideAction playerOverrideAction;
	private float playerOverrideActionSecondsRemaining;
	private bool navigationRescueOverride;
	private WorkerTask navigationRescueTask;
	private BoxBase navigationRescueBox;
	private bool hasNavigationRescueGoal;
	private int3 navigationRescueGoal;

	internal event Action<AIWorker> PlayerOverrideStateChanged;

	public WorkerControlMode ControlMode => controlMode;
	public PlayerOverridePhase PlayerOverridePhase => playerOverridePhase;
	public bool IsPlayerOverride => controlMode == WorkerControlMode.PlayerOverride;
	public bool IsNavigationRescueOverride => IsPlayerOverride && navigationRescueOverride;
	public bool IsManualNavigation => IsPlayerOverride && this is RobotWorker;

	internal bool TryEnterPlayerOverride(bool preserveNavigationTask, out string message)
	{
		message = string.Empty;
		if (IsPlayerOverride)
			return true;

		if (IsOperational == false)
		{
			message = "The worker is not operational.";
			return false;
		}

		if (currentTask != null && preserveNavigationTask == false)
		{
			message = "The worker must leave its current task before player control can begin.";
			return false;
		}
		if (preserveNavigationTask && (currentTask == null || IsWaitingForNavigation == false))
		{
			message = "Only a navigation-waiting task can be preserved for rescue control.";
			return false;
		}

		CancelRecovery(false);
		navigationRescueOverride = preserveNavigationTask;
		navigationRescueTask = preserveNavigationTask ? currentTask : null;
		navigationRescueBox = preserveNavigationTask ? CarryingAbility?.CarryingBox : null;
		hasNavigationRescueGoal = preserveNavigationTask &&
			routeFinder != null &&
			routeFinder.TryGetCurrentGoalCell(out navigationRescueGoal);
		if (preserveNavigationTask)
			SuspendNavigationWaitForPlayerOverride();
		routeFinder?.CancelCurrentRoute();
		if (preserveNavigationTask == false)
			localBlackBoard.Clear();
		playerOverrideBlackBoard.Clear();
		controlMode = WorkerControlMode.PlayerOverride;
		SetPlayerOverridePhase(OverridePhase.AwaitingCommand);
		SetWorkerTarget(WorkerStatusTarget.None);
		SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
		enabled = true;

		if (GameContext.HasInstance)
			WorkerMgr.RemoveIdleWorker(this);

		BuildBehaviorTree();
		NotifyPlayerOverrideStateChanged();
		return true;
	}

	internal bool TryQueuePlayerOverrideAction(
		IPlayerOverrideAction action,
		float durationSeconds,
		out string message)
	{
		message = string.Empty;
		if (IsPlayerOverride == false || playerOverridePhase != OverridePhase.AwaitingCommand)
		{
			message = "The worker is not awaiting a player command.";
			return false;
		}

		if (IsOperational == false)
		{
			message = "The worker is not operational.";
			return false;
		}

		if (action == null)
		{
			message = "The requested interaction is invalid.";
			return false;
		}

		playerOverrideAction?.Cancel();
		playerOverrideAction = action;
		playerOverrideActionSecondsRemaining = float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)
			? 0.0f
			: Mathf.Max(0.0f, durationSeconds);
		SetPlayerOverridePhase(OverridePhase.ExecutingInteraction);
		SetWorkerTarget(WorkerStatusTarget.WorkTarget);
		SetWorkerAction(WorkerStatusAction.Working);
		enabled = true;
		NotifyPlayerOverrideStateChanged();
		return true;
	}

	internal bool TryQueuePlayerOverrideAction(IPlayerOverrideAction action, out string message)
	{
		float duration = 0.0f;
		if (action != null && GameContext.HasInstance && GameContext.Instance.WMSys?.WorkPolicyService != null)
			duration = GameContext.Instance.WMSys.WorkPolicyService.GetWorkTime(this, action.ActionType);

		return TryQueuePlayerOverrideAction(action, duration, out message);
	}

	internal bool TryExitPlayerOverride(out string message)
	{
		message = string.Empty;
		if (IsPlayerOverride == false)
			return true;

		CancelPlayerOverride();
		return true;
	}

	internal void RestorePlayerOverrideState(WorkerControlMode mode)
	{
		CancelPendingPlayerOverrideAction();
		CancelPendingPlayerOverrideMove();
		controlMode = mode;
		navigationRescueOverride = false;
		navigationRescueTask = null;
		navigationRescueBox = null;
		hasNavigationRescueGoal = false;
		playerOverridePhase = mode == WorkerControlMode.PlayerOverride
			? OverridePhase.AwaitingCommand
			: OverridePhase.None;

		if (mode == WorkerControlMode.PlayerOverride)
		{
			SetWorkerTarget(WorkerStatusTarget.None);
			SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
			enabled = true;
		}
	}

	internal void PreparePlayerOverrideForSave()
	{
		if (IsPlayerOverride == false || playerOverridePhase == OverridePhase.AwaitingCommand)
			return;

		CancelPendingPlayerOverrideAction();
		CancelPendingPlayerOverrideMove();
		SetPlayerOverridePhase(OverridePhase.AwaitingCommand);
		SetWorkerTarget(WorkerStatusTarget.None);
		SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
		enabled = true;
		NotifyPlayerOverrideStateChanged();
	}

	internal void CancelPlayerOverride(bool becomeIdle = true)
	{
		bool resumeNavigationTask = navigationRescueOverride &&
			currentTask != null &&
			currentTask == navigationRescueTask &&
			CarryingAbility?.CarryingBox == navigationRescueBox;
		bool resumeNavigationGoal = resumeNavigationTask && hasNavigationRescueGoal;
		int3 resumeGoal = navigationRescueGoal;

		CancelPendingPlayerOverrideAction();
		CancelPendingPlayerOverrideMove();
		if (resumeNavigationTask == false)
			localBlackBoard.Clear();
		controlMode = WorkerControlMode.Automatic;
		playerOverridePhase = OverridePhase.None;
		navigationRescueOverride = false;
		navigationRescueTask = null;
		navigationRescueBox = null;
		hasNavigationRescueGoal = false;
		SetWorkerTarget(WorkerStatusTarget.None);
		if (IsOperational)
			SetWorkerAction(WorkerStatusAction.Idle);

		BuildBehaviorTree();
		enabled = true;
		if (resumeNavigationGoal)
			routeFinder?.RestoreNavigationGoal(resumeGoal);
		if (GameContext.HasInstance)
		{
			if (this is RobotWorker robot &&
				GameContext.Instance.RobotNavigationSvc != null &&
				GameContext.Instance.RobotNavigationSvc.CanRunAutomatic(robot, out RobotNavigationWaitReason reason) == false)
			{
				BeginNavigationWait(reason);
			}
			else
			{
				EndNavigationWait();
				if (resumeNavigationGoal)
					routeFinder?.RequestFreshRouteToCurrentGoal();
				else if (becomeIdle && IsOperational && currentTask == null)
					WorkerMgr.AddIdleWorker(this);
				else
					WorkerMgr.RemoveIdleWorker(this);
			}
		}

		NotifyPlayerOverrideStateChanged();
	}

	private static IBaseNode.NodeState RunPlayerOverride(in BTContext ctx)
	{
		AIWorker worker = ctx.Worker;
		if (worker == null || worker.IsPlayerOverride == false)
			return IBaseNode.NodeState.Failure;

		if (worker.IsOperational == false)
			return IBaseNode.NodeState.Failure;

		if (worker.HasPendingBlockingIncident)
			return IBaseNode.NodeState.Failure;

		switch (worker.playerOverridePhase)
		{
			case OverridePhase.Moving:
			case OverridePhase.WaitingForAirlock:
			case OverridePhase.UsingAirlock:
				return RunPlayerOverrideMove(ctx, worker);

			case OverridePhase.ExecutingInteraction:
				return RunPlayerOverrideAction(ctx, worker);

			case OverridePhase.AwaitingCommand:
			default:
				worker.SetWorkerTarget(WorkerStatusTarget.None);
				worker.SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
				return IBaseNode.NodeState.Running;
		}
	}

	private static IBaseNode.NodeState RunPlayerOverrideAction(in BTContext ctx, AIWorker worker)
	{
		IPlayerOverrideAction action = worker.playerOverrideAction;
		if (action == null)
		{
			worker.SetPlayerOverridePhase(OverridePhase.AwaitingCommand);
			worker.SetWorkerTarget(WorkerStatusTarget.None);
			worker.SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
			worker.NotifyPlayerOverrideStateChanged();
			return IBaseNode.NodeState.Running;
		}

		worker.SetWorkerAction(WorkerStatusAction.Working);
		worker.playerOverrideActionSecondsRemaining -= Mathf.Max(0.0f, ctx.DeltaTime);
		if (worker.playerOverrideActionSecondsRemaining > 0.0f)
			return IBaseNode.NodeState.Running;

		worker.ClearPendingWorkHandling();
		bool committed = false;
		string commitMessage = string.Empty;
		try
		{
			committed = action.TryCommit(worker, out commitMessage);
		}
		catch (Exception exception)
		{
			Debug.LogException(exception, worker);
		}

		bool hasHandling = worker.TryConsumePendingWorkHandling(out HumanWorkHandlingResult handling);
		if (committed || hasHandling)
			ApplyCompletedWork(ctx, action.ActionType, in handling);
		if (committed == false && string.IsNullOrWhiteSpace(commitMessage) == false)
		{
			Debug.LogWarning($"[PlayerOverride] {commitMessage}", worker);
			if (GameContext.HasInstance)
				GameContext.Instance.HudEventManager?.Publish(HudEventType.Warning, commitMessage, worker);
		}

		action.Cancel();
		worker.playerOverrideAction = null;
		worker.playerOverrideActionSecondsRemaining = 0.0f;
		worker.SetPlayerOverridePhase(OverridePhase.AwaitingCommand);
		worker.SetWorkerTarget(WorkerStatusTarget.None);
		worker.SetWorkerAction(WorkerStatusAction.AwaitingPlayerCommand);
		worker.NotifyPlayerOverrideStateChanged();
		return IBaseNode.NodeState.Running;
	}

	private void CancelPendingPlayerOverrideAction()
	{
		playerOverrideAction?.Cancel();
		playerOverrideAction = null;
		playerOverrideActionSecondsRemaining = 0.0f;
		ClearPendingWorkHandling();
	}

	private void SetPlayerOverridePhase(PlayerOverridePhase phase)
	{
		playerOverridePhase = phase;
	}

	private void NotifyPlayerOverrideStateChanged()
	{
		PlayerOverrideStateChanged?.Invoke(this);
	}
}
