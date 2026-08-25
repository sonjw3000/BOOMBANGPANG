using System.Collections.Generic;

public static class WorkerTaskAssignmentPolicy
{
	public static bool CanAssign(AIWorker worker, WorkerTask.TaskType taskType)
	{
		return CanAssign(worker, ResolvePrimaryBuildingType(worker), taskType);
	}

	public static bool CanAssign(AIWorker worker, BuildingType? buildingType, WorkerTask.TaskType taskType)
	{
		return worker != null &&
			worker.HasAbility(WorkerTaskTypeRequirement.GetRequiredAbilities(taskType)) &&
			IsTaskTypeAllowedForBuilding(buildingType, taskType);
	}

	public static void GetAssignableTaskTypes(AIWorker worker, List<WorkerTask.TaskType> results)
	{
		GetAssignableTaskTypes(worker, ResolvePrimaryBuildingType(worker), results);
	}

	public static void GetAssignableTaskTypes(
		AIWorker worker,
		BuildingType? buildingType,
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

			if (CanAssign(worker, buildingType, taskType))
				results.Add(taskType);
		}
	}

	public static bool IsTaskTypeAllowedForBuilding(BuildingType? buildingType, WorkerTask.TaskType taskType)
	{
		switch (taskType)
		{
			case WorkerTask.TaskType.Undefined:
				return true;

			case WorkerTask.TaskType.IB:
			case WorkerTask.TaskType.CapsuleClear:
			case WorkerTask.TaskType.CapsuleSupply:
			case WorkerTask.TaskType.OB:
				return buildingType.HasValue;

			case WorkerTask.TaskType.CargoTransfer:
			case WorkerTask.TaskType.WasteCollection:
				return buildingType.HasValue == false;

			case WorkerTask.TaskType.Unloading:
			case WorkerTask.TaskType.Loading:
				return buildingType.HasValue == false;

			case WorkerTask.TaskType.Labeling:
			case WorkerTask.TaskType.Storing:
			case WorkerTask.TaskType.Picking:
			case WorkerTask.TaskType.Packing:
			case WorkerTask.TaskType.PackingInput:
			case WorkerTask.TaskType.PackingOutput:
			case WorkerTask.TaskType.LaunchSort:
				return buildingType.HasValue;

			default:
				return false;
		}
	}

	private static BuildingType? ResolvePrimaryBuildingType(AIWorker worker)
	{
		if (worker == null || worker.PrimaryBuildingId == 0 || GameContext.HasInstance == false)
			return null;

		return GameContext.Instance.BuildingMgr != null &&
			GameContext.Instance.BuildingMgr.TryGetBuilding(worker.PrimaryBuildingId, out Building building) &&
			building != null
			? building.Type
			: null;
	}
}
