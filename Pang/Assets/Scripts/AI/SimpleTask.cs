class WorkerTask
{
	public enum ETaskType
	{
		PICKING,
		PACKAGING,
		TRANSFER,
		UNDEFINED
	}

	public ETaskType Type { get; set; }
	public AIWorker OccupyWorker = null;
}
