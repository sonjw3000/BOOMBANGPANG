public sealed class LabelingTaskBuildRequest : TaskBuildRequest<LabelingTask>
{
	private readonly uint buildingId;
	private readonly CapsuleBuffer targetBuffer;

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.Labeling;
	public override object RequestKey => new TaskBuildRequestKey(TaskType, targetBuffer);
	public override bool IsStillValid => GameContext.HasInstance &&
		GameContext.Instance.IBWorkflowSvc?.CanRequestLabelingTask(buildingId, targetBuffer) == true;
	public override bool DependsOnFacility(IFacility facility) => ReferenceEquals(targetBuffer, facility);

	public LabelingTaskBuildRequest(uint buildingId, CapsuleBuffer targetBuffer)
		: base(buildingId)
	{
		this.buildingId = buildingId;
		this.targetBuffer = targetBuffer;
	}

	protected override bool TryBuildTask(out LabelingTask task)
	{
		if (IsStillValid == false)
		{
			task = null;
			return false;
		}

		task = new LabelingTask(buildingId, targetBuffer);
		return true;
	}

	public override void OnTaskQueued(WorkerTask task)
	{
		if (task is not LabelingTask labelingTask ||
			GameContext.Instance.IBWorkflowSvc?.RegisterLabelingTask(labelingTask) == true)
		{
			return;
		}

		TaskMgr?.InvalidateTask(labelingTask, TaskInvalidationReason.DispatchInvalid);
	}
}
