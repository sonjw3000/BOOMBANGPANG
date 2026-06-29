
using System;
using System.Runtime.CompilerServices;

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

public readonly struct TaskBuildRequestKey : IEquatable<TaskBuildRequestKey>
{
	private readonly WorkerTask.TaskType taskType;
	private readonly object source;

	public TaskBuildRequestKey(WorkerTask.TaskType taskType, object source)
	{
		this.taskType = taskType;
		this.source = source;
	}

	public bool Equals(TaskBuildRequestKey other)
	{
		return taskType == other.taskType && ReferenceEquals(source, other.source);
	}

	public override bool Equals(object obj)
	{
		return obj is TaskBuildRequestKey other && Equals(other);
	}

	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 31 + (int)taskType;
		hash = hash * 31 + (source != null ? RuntimeHelpers.GetHashCode(source) : 0);
		return hash;
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
