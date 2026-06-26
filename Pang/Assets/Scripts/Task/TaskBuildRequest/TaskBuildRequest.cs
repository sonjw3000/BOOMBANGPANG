
public abstract class TaskBuildRequest
{
	private readonly uint requestedBuildingID = 0;

	public abstract WorkerTask.TaskType TaskType { get; }
	public virtual object RequestKey => this;
	public abstract bool IsStillValid { get; }

	public uint RequestedBuildingID => requestedBuildingID;

	protected GameContext Ctx => GameContext.Instance;
	protected TaskManager TaskMgr => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
	protected GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	protected BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;

	public TaskBuildRequest(uint requestedBuildingID) => this.requestedBuildingID = requestedBuildingID;

	public abstract bool TryBuild(out WorkerTask task);

	public virtual void OnTaskQueued(WorkerTask task)
	{
	}
}

public abstract class TaskBuildRequest<TTask> : TaskBuildRequest where TTask : WorkerTask
{
	public TaskBuildRequest(uint requestedBuildingID) : base(requestedBuildingID) {}

	public sealed override bool TryBuild(out WorkerTask task)
	{
		if (TryBuildTask(out TTask typedTask))
		{
			task = typedTask;
			return true;
		}

		task = null;
		return false;
	}

	protected abstract bool TryBuildTask(out TTask task);
}

