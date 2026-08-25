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
	public bool TryRequestWorkerRoleAssignment(
		AIWorker worker,
		uint buildingId,
		WorkforceRole role)
	{
		if (TryGetWorkforceRoleDefinition(
				buildingId,
				role,
				out WorkforceRoleDefinition definition) == false)
		{
			return false;
		}

		return TryRequestWorkerAssignment(worker, buildingId, definition.TaskTypes);
	}

	public bool TryRequestWorkerUnassignment(AIWorker worker)
	{
		return TryRequestWorkerAssignment(
			worker,
			0,
			System.Array.Empty<WorkerTask.TaskType>());
	}

	public bool CanRequestWorkerUnassignment(AIWorker worker)
	{
		return CanRequestWorkerAssignment(
			worker,
			0,
			System.Array.Empty<WorkerTask.TaskType>());
	}

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
		return TryGetWorkforceRoleDefinition(buildingId, role, out _);
	}

	private static bool TryGetWorkforceRoleDefinition(
		uint buildingId,
		WorkforceRole role,
		out WorkforceRoleDefinition definition)
	{
		definition = null;
		return TryResolveBuildingScope(buildingId) &&
			WorkforceRoleCatalog.IsRoleSupported(buildingId, role) &&
			WorkforceRoleCatalog.TryGetDefinition(role, out definition);
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
