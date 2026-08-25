using System.Collections.Generic;

public static class WorkerTaskAssignmentPolicy
{
	public static bool CanAssign(AIWorker worker, WorkerTask.TaskType taskType)
	{
		return CanAssign(worker, ResolvePrimaryBuildingId(worker), taskType);
	}

	public static bool CanAssign(AIWorker worker, uint buildingId, WorkerTask.TaskType taskType)
	{
		return worker != null &&
			worker.HasAbility(WorkerTaskTypeRequirement.GetRequiredAbilities(taskType)) &&
			IsTaskTypeAllowedForBuilding(buildingId, taskType);
	}

	public static void GetAssignableTaskTypes(AIWorker worker, List<WorkerTask.TaskType> results)
	{
		GetAssignableTaskTypes(worker, ResolvePrimaryBuildingId(worker), results);
	}

	public static void GetAssignableTaskTypes(
		AIWorker worker,
		uint buildingId,
		List<WorkerTask.TaskType> results)
	{
		if (results == null)
			return;

		results.Clear();
		if (worker == null)
			return;

		foreach (WorkerTask.TaskType taskType in System.Enum.GetValues(typeof(WorkerTask.TaskType)))
		{
			if (taskType == WorkerTask.TaskType.HandleMistake)
				continue;

			if (CanAssign(worker, buildingId, taskType))
				results.Add(taskType);
		}
	}

	public static bool IsTaskTypeAllowedForBuilding(uint buildingId, WorkerTask.TaskType taskType)
	{
		switch (taskType)
		{
			case WorkerTask.TaskType.Undefined:
				return true;

			case WorkerTask.TaskType.IB:
			case WorkerTask.TaskType.CapsuleClear:
			case WorkerTask.TaskType.CapsuleSupply:
			case WorkerTask.TaskType.OB:
				return buildingId != 0;

			case WorkerTask.TaskType.CargoTransfer:
			case WorkerTask.TaskType.WasteCollection:
				return buildingId == 0;

			case WorkerTask.TaskType.Unloading:
			case WorkerTask.TaskType.Loading:
				return buildingId == 0;

			case WorkerTask.TaskType.Labeling:
			case WorkerTask.TaskType.Storing:
			case WorkerTask.TaskType.Picking:
			case WorkerTask.TaskType.Packing:
			case WorkerTask.TaskType.PackingInput:
			case WorkerTask.TaskType.PackingOutput:
			case WorkerTask.TaskType.LaunchSort:
				return buildingId != 0;

			default:
				return false;
		}
	}

	private static uint ResolvePrimaryBuildingId(AIWorker worker)
	{
		if (worker == null || worker.PrimaryBuildingId == 0 || GameContext.HasInstance == false)
			return 0;

		return GameContext.Instance.BuildingMgr != null &&
			GameContext.Instance.BuildingMgr.TryGetBuilding(worker.PrimaryBuildingId, out Building building) &&
			building != null
			? worker.PrimaryBuildingId
			: 0;
	}
}
