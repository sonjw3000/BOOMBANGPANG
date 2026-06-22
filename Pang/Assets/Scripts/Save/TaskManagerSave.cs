using System;
using static WorkerTask;

public partial class TaskManager
{
	public void ResetRuntimeState()
	{
		foreach (TaskType type in Enum.GetValues(typeof(TaskType)))
		{
			taskQueue[type].Clear();
			taskOnProgress[type].Clear();
		}
	}
}
