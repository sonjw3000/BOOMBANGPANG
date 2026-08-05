using Assets.Scripts.AI.BT;

public abstract partial class AIWorker
{
	private RobotNavigationWaitReason navigationWaitReason;

	public RobotNavigationWaitReason NavigationWaitReason => navigationWaitReason;
	public bool IsWaitingForNavigation => navigationWaitReason != RobotNavigationWaitReason.None;

	public bool CanUseAutomaticNavigation(out RobotNavigationWaitReason reason)
	{
		reason = RobotNavigationWaitReason.None;
		if (this is not RobotWorker robot || IsPlayerOverride || GameContext.HasInstance == false)
			return true;

		RobotNavigationService service = GameContext.Instance.RobotNavigationSvc;
		return service == null || (currentTask == null
			? service.CanAcceptNewAutomaticTask(robot, out reason)
			: service.CanRunAutomatic(robot, out reason));
	}

	public void BeginNavigationWait(RobotNavigationWaitReason reason)
	{
		if (this is not RobotWorker || IsPlayerOverride)
			return;

		if (reason == RobotNavigationWaitReason.None)
			reason = RobotNavigationWaitReason.Coverage;

		bool alreadyWaitingForSameReason = navigationWaitReason == reason;
		navigationWaitReason = reason;
		if (this is RobotWorker robot && GameContext.HasInstance)
			GameContext.Instance.RobotNavigationSvc?.RegisterWaitingRobot(robot);
		if (alreadyWaitingForSameReason)
			return;

		routeFinder?.SuspendForNavigation();
		if (GameContext.HasInstance)
			WorkerMgr.RemoveIdleWorker(this);

		SetWorkerTarget(WorkerStatusTarget.NavigationHub);
		SetWorkerAction(GetNavigationWaitAction(reason));
		enabled = true;
	}

	public void EndNavigationWait()
	{
		if (navigationWaitReason == RobotNavigationWaitReason.None)
			return;

		navigationWaitReason = RobotNavigationWaitReason.None;
		if (this is RobotWorker robot && GameContext.HasInstance)
			GameContext.Instance.RobotNavigationSvc?.UnregisterWaitingRobot(robot);
		if (IsOperational == false)
			return;
		if (routeFinder != null && routeFinder.ResumeFromNavigation())
			return;

		if (currentTask == null)
		{
			SetWorkerTarget(WorkerStatusTarget.None);
			SetWorkerAction(WorkerStatusAction.Idle);
			if (GameContext.HasInstance && IsOperational && IsPlayerOverride == false)
				WorkerMgr.AddIdleWorker(this);
		}

		enabled = true;
	}

	internal void SuspendNavigationWaitForPlayerOverride()
	{
		if (navigationWaitReason == RobotNavigationWaitReason.None)
			return;

		navigationWaitReason = RobotNavigationWaitReason.None;
		if (this is RobotWorker robot && GameContext.HasInstance)
			GameContext.Instance.RobotNavigationSvc?.UnregisterWaitingRobot(robot);
	}

	private static IBaseNode.NodeState HoldNavigationWait(in BTContext ctx)
	{
		if (ctx.Worker == null || ctx.Worker.IsWaitingForNavigation == false)
			return IBaseNode.NodeState.Failure;

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.NavigationHub);
		ctx.Worker.SetWorkerAction(GetNavigationWaitAction(ctx.Worker.NavigationWaitReason));
		return IBaseNode.NodeState.Running;
	}

	private static WorkerStatusAction GetNavigationWaitAction(RobotNavigationWaitReason reason)
	{
		return reason == RobotNavigationWaitReason.OrchestrationCapacity
			? WorkerStatusAction.WaitingForOrchestrationCapacity
			: WorkerStatusAction.WaitingForNavigationCoverage;
	}
}
