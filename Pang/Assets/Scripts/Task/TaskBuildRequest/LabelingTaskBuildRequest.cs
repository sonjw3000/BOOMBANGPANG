public sealed class LabelingTaskBuildRequest : TaskBuildRequest<LabelingTask>
{
	private readonly StagingBuilding building;
	private readonly CapsuleBuffer targetBuffer;

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.Labeling;
	public override object RequestKey => new TaskBuildRequestKey(TaskType, targetBuffer);
	public override bool IsStillValid => building != null && building.CanRequestLabelingTask(targetBuffer);

	public LabelingTaskBuildRequest(StagingBuilding building, CapsuleBuffer targetBuffer)
		: base(building != null ? building.RuntimeBuildingId : 0)
	{
		this.building = building;
		this.targetBuffer = targetBuffer;
	}

	protected override bool TryBuildTask(out LabelingTask task)
	{
		if (IsStillValid == false)
		{
			task = null;
			return false;
		}

		task = new LabelingTask(building, targetBuffer);
		return true;
	}

	public override void OnTaskQueued(WorkerTask task)
	{
		building?.OnLabelingTaskQueued(targetBuffer);
	}
}
