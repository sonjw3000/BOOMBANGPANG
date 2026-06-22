using static WorkerTask;

public partial class WorkerManager
{
	public void ResetRuntimeState()
	{
		workers.Clear();
		monthlyCost = 0;
		nextWorkerID = 0;
		trafficBlockedCount = 0;

		ResetWorkerStatusCounts();

		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			workersPerTaskType[type].Clear();
			idleWorkersQueue[type].Clear();
			idleWorkersSet[type].Clear();
		}
	}
}
