
public struct WorkerTask
{
	public enum TaskType
	{
		Picking,
		Packagine,
		Transfer,
		Undefined
	}

	public TaskType Type { get; private set; }
	public AIWorker OccupyWorker;
}
