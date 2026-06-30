using System.Collections.Generic;

public static class WorkerTaskAssignmentPolicy
{
	public static bool CanAssign(AIWorker worker, WorkerTask.TaskType taskType)
	{
		if (worker == null)
			return false;

		if (worker.HasAbility(WorkerTaskTypeRequirement.GetRequiredAbilities(taskType)) == false)
			return false;

		return IsTaskTypeAllowedForBuilding(ResolvePrimaryBuildingType(worker), taskType);
	}

	public static void GetAssignableTaskTypes(AIWorker worker, List<WorkerTask.TaskType> results)
	{
		if (results == null)
			return;

		results.Clear();
		if (worker == null)
			return;

		foreach (WorkerTask.TaskType taskType in System.Enum.GetValues(typeof(WorkerTask.TaskType)))
		{
			if (taskType > WorkerTask.TaskType.Undefined)
				continue;

			if (CanAssign(worker, taskType))
				results.Add(taskType);
		}
	}

	public static bool IsTaskTypeAllowedForBuilding(BuildingType? buildingType, WorkerTask.TaskType taskType)
	{
		switch (taskType)
		{
			case WorkerTask.TaskType.Undefined:
			case WorkerTask.TaskType.Water:
				return true;

			case WorkerTask.TaskType.IB:
			case WorkerTask.TaskType.CapsuleClear:
			case WorkerTask.TaskType.CapsuleSupply:
			case WorkerTask.TaskType.OB:
				return buildingType.HasValue;

			case WorkerTask.TaskType.CargoTransfer:
				return buildingType.HasValue == false;

			case WorkerTask.TaskType.Unloading:
			case WorkerTask.TaskType.Loading:
				return buildingType.HasValue == false;

			case WorkerTask.TaskType.Labeling:
				return buildingType == BuildingType.Staging;

			case WorkerTask.TaskType.Storing:
			case WorkerTask.TaskType.Picking:
				return buildingType == BuildingType.Storage;

			case WorkerTask.TaskType.Packing:
				return buildingType == BuildingType.Packing;

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
