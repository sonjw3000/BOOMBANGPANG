public readonly struct WorkforceRoleSummary
{
	public WorkforceRole Role { get; }
	public int FullCount { get; }
	public int PartialCount { get; }
	public int OperationalCount => FullCount + PartialCount;

	internal WorkforceRoleSummary(
		WorkforceRole role,
		int fullCount,
		int partialCount)
	{
		Role = role;
		FullCount = fullCount;
		PartialCount = partialCount;
	}
}

public partial class WorkerManager
{
	public bool TryGetWorkforceRoleSummary(
		uint buildingId,
		WorkforceRole role,
		out WorkforceRoleSummary summary)
	{
		summary = new WorkforceRoleSummary(role, 0, 0);
		if (TryResolveBuildingType(buildingId, out BuildingType? buildingType) == false ||
			WorkforceRoleCatalog.IsRoleSupported(buildingType, role) == false ||
			WorkforceRoleCatalog.TryGetDefinition(role, out _) == false)
		{
			return false;
		}

		int fullCount = 0;
		int partialCount = 0;
		for (int i = 0; i < workers.Count; ++i)
		{
			AIWorker worker = workers[i];
			if (worker == null ||
				worker.IsOperational == false ||
				worker.PrimaryBuildingId != buildingId)
			{
				continue;
			}

			switch (WorkforceRoleCatalog.GetAssignmentState(role, worker.AssignedTaskTypes))
			{
				case WorkforceRoleAssignmentState.Full:
					++fullCount;
					break;

				case WorkforceRoleAssignmentState.Partial:
					++partialCount;
					break;
			}
		}

		summary = new WorkforceRoleSummary(role, fullCount, partialCount);
		return true;
	}
}
