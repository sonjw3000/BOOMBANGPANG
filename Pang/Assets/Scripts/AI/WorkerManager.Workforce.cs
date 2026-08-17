using System.Collections.Generic;

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

public readonly struct WorkforceRoleWorkerEntry
{
	public AIWorker Worker { get; }
	public WorkforceRoleAssignmentState AssignmentState { get; }

	internal WorkforceRoleWorkerEntry(
		AIWorker worker,
		WorkforceRoleAssignmentState assignmentState)
	{
		Worker = worker;
		AssignmentState = assignmentState;
	}
}

public partial class WorkerManager
{
	public void GetOperationalUnassignedWorkers(List<AIWorker> results)
	{
		if (results == null)
			return;

		results.Clear();
		for (int i = 0; i < workers.Count; ++i)
		{
			AIWorker worker = workers[i];
			if (worker != null &&
				worker.IsOperational &&
				(worker.AssignedTaskTypes == null || worker.AssignedTaskTypes.Count == 0))
			{
				results.Add(worker);
			}
		}
	}

	public bool TryGetWorkforceRoleWorkers(
		uint buildingId,
		WorkforceRole role,
		List<WorkforceRoleWorkerEntry> results)
	{
		if (results == null)
			return false;

		results.Clear();
		if (TryValidateWorkforceRoleScope(buildingId, role) == false)
			return false;

		for (int i = 0; i < workers.Count; ++i)
		{
			AIWorker worker = workers[i];
			if (TryGetCurrentWorkforceRoleAssignmentState(
					worker,
					buildingId,
					role,
					out WorkforceRoleAssignmentState assignmentState) == false)
			{
				continue;
			}

			results.Add(new WorkforceRoleWorkerEntry(worker, assignmentState));
		}

		return true;
	}

	public bool TryGetWorkforceRoleSummary(
		uint buildingId,
		WorkforceRole role,
		out WorkforceRoleSummary summary)
	{
		summary = new WorkforceRoleSummary(role, 0, 0);
		if (TryValidateWorkforceRoleScope(buildingId, role) == false)
			return false;

		int fullCount = 0;
		int partialCount = 0;
		for (int i = 0; i < workers.Count; ++i)
		{
			AIWorker worker = workers[i];
			if (TryGetCurrentWorkforceRoleAssignmentState(
					worker,
					buildingId,
					role,
					out WorkforceRoleAssignmentState assignmentState) == false)
			{
				continue;
			}

			switch (assignmentState)
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

	private static bool TryValidateWorkforceRoleScope(uint buildingId, WorkforceRole role)
	{
		return TryResolveBuildingType(buildingId, out BuildingType? buildingType) &&
			WorkforceRoleCatalog.IsRoleSupported(buildingType, role) &&
			WorkforceRoleCatalog.TryGetDefinition(role, out _);
	}

	private static bool TryGetCurrentWorkforceRoleAssignmentState(
		AIWorker worker,
		uint buildingId,
		WorkforceRole role,
		out WorkforceRoleAssignmentState assignmentState)
	{
		assignmentState = WorkforceRoleAssignmentState.None;
		if (worker == null ||
			worker.IsOperational == false ||
			worker.PrimaryBuildingId != buildingId)
		{
			return false;
		}

		assignmentState = WorkforceRoleCatalog.GetAssignmentState(role, worker.AssignedTaskTypes);
		return assignmentState != WorkforceRoleAssignmentState.None;
	}
}
