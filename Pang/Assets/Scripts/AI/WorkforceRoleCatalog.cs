using System;
using System.Collections.Generic;

public enum WorkforceRole
{
	Undefined = 0,
	CapsuleHandling,
	Labeling,
	Storing,
	Picking,
	Packing,
	PackingLogistics,
	LaunchSorting,
	Unloading,
	Loading,
	CargoTransfer,
	WasteCollection,
}

public enum WorkforceRoleAssignmentState
{
	None,
	Partial,
	Full,
}

public sealed class WorkforceRoleDefinition
{
	private readonly IReadOnlyList<WorkerTask.TaskType> taskTypes;

	public WorkforceRole Role { get; }
	public string DisplayName { get; }
	public IReadOnlyList<WorkerTask.TaskType> TaskTypes => taskTypes;

	internal WorkforceRoleDefinition(
		WorkforceRole role,
		string displayName,
		params WorkerTask.TaskType[] taskTypes)
	{
		if (role == WorkforceRole.Undefined)
			throw new ArgumentException("Undefined cannot be registered as a workforce role.", nameof(role));
		if (string.IsNullOrWhiteSpace(displayName))
			throw new ArgumentException("A workforce role requires a display name.", nameof(displayName));
		if (taskTypes == null || taskTypes.Length == 0)
			throw new ArgumentException("A workforce role requires at least one task type.", nameof(taskTypes));

		Role = role;
		DisplayName = displayName;
		List<WorkerTask.TaskType> uniqueTaskTypes = new(taskTypes.Length);
		for (int i = 0; i < taskTypes.Length; ++i)
		{
			WorkerTask.TaskType taskType = taskTypes[i];
			if (taskType == WorkerTask.TaskType.Undefined ||
				taskType == WorkerTask.TaskType.HandleMistake)
			{
				throw new ArgumentException($"{taskType} cannot be assigned through workforce roles.", nameof(taskTypes));
			}

			if (uniqueTaskTypes.Contains(taskType) == false)
				uniqueTaskTypes.Add(taskType);
		}

		this.taskTypes = uniqueTaskTypes.AsReadOnly();
	}
}

public static class WorkforceRoleCatalog
{
	private static readonly IReadOnlyList<WorkforceRole> noRoles = CreateRoleList();
	private static readonly IReadOnlyList<WorkforceRole> genericRoles = CreateRoleList(
		WorkforceRole.CapsuleHandling);
	private static readonly IReadOnlyList<WorkforceRole> stagingRoles = CreateRoleList(
		WorkforceRole.Labeling,
		WorkforceRole.CapsuleHandling);
	private static readonly IReadOnlyList<WorkforceRole> storageRoles = CreateRoleList(
		WorkforceRole.Storing,
		WorkforceRole.Picking,
		WorkforceRole.CapsuleHandling);
	private static readonly IReadOnlyList<WorkforceRole> packingRoles = CreateRoleList(
		WorkforceRole.Packing,
		WorkforceRole.PackingLogistics,
		WorkforceRole.CapsuleHandling);
	private static readonly IReadOnlyList<WorkforceRole> launchRoles = CreateRoleList(
		WorkforceRole.LaunchSorting,
		WorkforceRole.CapsuleHandling);
	private static readonly IReadOnlyList<WorkforceRole> publicRoles = CreateRoleList(
		WorkforceRole.Unloading,
		WorkforceRole.Loading,
		WorkforceRole.CargoTransfer,
		WorkforceRole.WasteCollection);

	private static readonly Dictionary<WorkforceRole, WorkforceRoleDefinition> definitions = new()
	{
		[WorkforceRole.CapsuleHandling] = new(
			WorkforceRole.CapsuleHandling,
			"Capsule Handling",
			WorkerTask.TaskType.IB,
			WorkerTask.TaskType.CapsuleClear,
			WorkerTask.TaskType.CapsuleSupply,
			WorkerTask.TaskType.OB),
		[WorkforceRole.Labeling] = new(
			WorkforceRole.Labeling,
			"Labeling",
			WorkerTask.TaskType.Labeling),
		[WorkforceRole.Storing] = new(
			WorkforceRole.Storing,
			"Storing",
			WorkerTask.TaskType.Storing),
		[WorkforceRole.Picking] = new(
			WorkforceRole.Picking,
			"Picking",
			WorkerTask.TaskType.Picking),
		[WorkforceRole.Packing] = new(
			WorkforceRole.Packing,
			"Packing",
			WorkerTask.TaskType.Packing),
		[WorkforceRole.PackingLogistics] = new(
			WorkforceRole.PackingLogistics,
			"Packing Logistics",
			WorkerTask.TaskType.PackingInput,
			WorkerTask.TaskType.PackingOutput),
		[WorkforceRole.LaunchSorting] = new(
			WorkforceRole.LaunchSorting,
			"Launch Sorting",
			WorkerTask.TaskType.LaunchSort),
		[WorkforceRole.Unloading] = new(
			WorkforceRole.Unloading,
			"Unloading",
			WorkerTask.TaskType.Unloading),
		[WorkforceRole.Loading] = new(
			WorkforceRole.Loading,
			"Loading",
			WorkerTask.TaskType.Loading),
		[WorkforceRole.CargoTransfer] = new(
			WorkforceRole.CargoTransfer,
			"Cargo Transfer",
			WorkerTask.TaskType.CargoTransfer),
		[WorkforceRole.WasteCollection] = new(
			WorkforceRole.WasteCollection,
			"Waste Collection",
			WorkerTask.TaskType.WasteCollection),
	};

	public static IReadOnlyList<WorkforceRole> PublicRoles => publicRoles;

	public static bool TryGetDefinition(WorkforceRole role, out WorkforceRoleDefinition definition)
	{
		return definitions.TryGetValue(role, out definition);
	}

	public static IReadOnlyList<WorkforceRole> GetRoles(BuildingType? buildingType)
	{
		if (buildingType.HasValue == false)
			return publicRoles;

		return buildingType.Value switch
		{
			BuildingType.Generic => genericRoles,
			BuildingType.Staging => stagingRoles,
			BuildingType.Storage => storageRoles,
			BuildingType.Packing => packingRoles,
			BuildingType.Launch => launchRoles,
			_ => noRoles,
		};
	}

	public static bool IsRoleSupported(BuildingType? buildingType, WorkforceRole role)
	{
		IReadOnlyList<WorkforceRole> roles = GetRoles(buildingType);
		for (int i = 0; i < roles.Count; ++i)
		{
			if (roles[i] == role)
				return true;
		}

		return false;
	}

	public static WorkforceRoleAssignmentState GetAssignmentState(
		WorkforceRole role,
		IReadOnlyList<WorkerTask.TaskType> assignedTaskTypes)
	{
		if (TryGetDefinition(role, out WorkforceRoleDefinition definition) == false ||
			assignedTaskTypes == null ||
			assignedTaskTypes.Count == 0)
		{
			return WorkforceRoleAssignmentState.None;
		}

		int matchedCount = 0;
		for (int i = 0; i < definition.TaskTypes.Count; ++i)
		{
			if (ContainsTaskType(assignedTaskTypes, definition.TaskTypes[i]))
				++matchedCount;
		}

		if (matchedCount == 0)
			return WorkforceRoleAssignmentState.None;

		return matchedCount == definition.TaskTypes.Count
			? WorkforceRoleAssignmentState.Full
			: WorkforceRoleAssignmentState.Partial;
	}

	public static bool TryCopyTaskTypes(
		WorkforceRole role,
		List<WorkerTask.TaskType> results)
	{
		if (results == null)
			return false;

		results.Clear();
		if (TryGetDefinition(role, out WorkforceRoleDefinition definition) == false)
			return false;

		for (int i = 0; i < definition.TaskTypes.Count; ++i)
			results.Add(definition.TaskTypes[i]);

		return true;
	}

	private static bool ContainsTaskType(
		IReadOnlyList<WorkerTask.TaskType> taskTypes,
		WorkerTask.TaskType target)
	{
		for (int i = 0; i < taskTypes.Count; ++i)
		{
			if (taskTypes[i] == target)
				return true;
		}

		return false;
	}

	private static IReadOnlyList<WorkforceRole> CreateRoleList(params WorkforceRole[] roles)
	{
		return Array.AsReadOnly(roles ?? Array.Empty<WorkforceRole>());
	}
}
