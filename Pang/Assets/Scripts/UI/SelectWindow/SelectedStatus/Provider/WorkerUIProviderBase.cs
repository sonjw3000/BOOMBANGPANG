using System;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

public interface IWorkerUIProvider
{
	AIWorker Target { get; }
	string Name { get; }
	string Subtitle { get; }
	string WorkerTypeLabel { get; }
	string ResourceDisplay { get; }
	string MoveSpeedDisplay { get; }
	string WorkSpeedDisplay { get; }
	string MainTaskTypeDisplay { get; }
	string AbilityDisplay { get; }
	string MonthlyCostDisplay { get; }
	string PositionDisplay { get; }
	string DestinationDisplay { get; }
	string ActionDisplay { get; }
	string TargetDisplay { get; }
	string CurrentTaskButtonLabel { get; }
	string CurrentTaskSummary { get; }
	bool HasAssignedTask { get; }
	bool HasCarriedBox { get; }
	string CarriedBoxFillDisplay { get; }
	ItemContainerDisplayInfo GetCarriedBoxDisplay();
}

public abstract class WorkerUIProviderBase<TWorker> : UIProvider<TWorker>, IWorkerUIProvider
	where TWorker : AIWorker
{
	protected abstract string ResourceLabel { get; }
	protected abstract float ResourceValue { get; }

	public override string Name => currentTarget != null ? currentTarget.Name : "Unknown Worker";
	public override string Subtitle => currentTarget != null ? GetWorkerTypeLabel(currentTarget) : "Unknown Worker";
	public override Sprite Icon => null;
	AIWorker IWorkerUIProvider.Target => currentTarget;

	public string WorkerTypeLabel => currentTarget != null ? GetWorkerTypeLabel(currentTarget) : "Unknown";
	public string ResourceDisplay => $"{ResourceValue:0.0}%";
	public string MoveSpeedDisplay => currentTarget != null ? $"x{currentTarget.GetMoveSpeedMultiplier():0.00}" : "x0.00";
	public string WorkSpeedDisplay => currentTarget != null ? $"x{currentTarget.GetWorkSpeedMultiplier():0.00}" : "x0.00";
	public string MainTaskTypeDisplay => currentTarget != null ? BuildTaskTypeDisplay(currentTarget) : "None";
	public string AbilityDisplay => currentTarget != null ? BuildAbilityDisplay(currentTarget.Ability) : "None";
	public string MonthlyCostDisplay => currentTarget != null ? currentTarget.MonthlyCost.ToString() : "0";
	public string PositionDisplay => currentTarget != null ? currentTarget.GridPosition.ToString() : "(0,0,0)";
	public string ActionDisplay => currentTarget != null ? currentTarget.WorkerState.Action.ToString() : "None";
	public string TargetDisplay => currentTarget != null ? currentTarget.WorkerState.Target.ToString() : "None";
	public string CurrentTaskButtonLabel => currentTarget?.CurrentTask != null ? currentTarget.CurrentTask.GetType().Name : "None";
	public string CurrentTaskSummary => currentTarget?.CurrentTask != null ? currentTarget.CurrentTask.GetStatusSummary() : "No assigned task.";
	public bool HasAssignedTask => currentTarget?.CurrentTask != null;
	public bool HasCarriedBox => currentTarget?.CarryingAbility?.CarryingBox != null;
	public float CarriedBoxFillPercent
	{
		get
		{
			BoxBase box = currentTarget?.CarryingAbility?.CarryingBox;
			if (box == null || box.MaxSize <= 0.0f)
				return 0.0f;

			return (box.TotalSize / box.MaxSize) * 100.0f;
		}
	}
	public string CarriedBoxFillDisplay => $"{CarriedBoxFillPercent:0.0}%";

	public string DestinationDisplay
	{
		get
		{
			if (currentTarget == null || currentTarget.TryGetCurrentDestination(out var name, out var position) == false)
				return "None";

			return $"{name} ({position.x}, {position.y}, {position.z})";
		}
	}

	public ItemContainerDisplayInfo GetCarriedBoxDisplay() => new()
	{
		ContainerName = currentTarget?.CarryingAbility?.CarryingBox != null
			? $"{currentTarget.CarryingAbility.CarryingBox.Type} Box #{currentTarget.CarryingAbility.CarryingBox.BoxId}"
			: "None",
		HasContainer = currentTarget?.CarryingAbility?.CarryingBox != null,
		Items = ItemContainerDisplayUtility.BuildItemRows(currentTarget?.CarryingAbility?.CarryingBox),
		ManifestItems = ItemContainerDisplayUtility.BuildManifestRows(currentTarget?.CarryingAbility?.CarryingBox),
	};

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock(ResourceLabel, ResourceDisplay));
		infoBlocks.Add(new KeyValueBlock("MoveSpeed", MoveSpeedDisplay));
		infoBlocks.Add(new KeyValueBlock("Position", PositionDisplay));
		infoBlocks.Add(new KeyValueBlock("MainTaskType", MainTaskTypeDisplay));
		infoBlocks.Add(new KeyValueBlock("Action", ActionDisplay));
		infoBlocks.Add(new KeyValueBlock("Target", TargetDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 6)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(ResourceDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(MoveSpeedDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(PositionDisplay);
		(infoBlocks[3] as KeyValueBlock)?.UpdateValue(MainTaskTypeDisplay);
		(infoBlocks[4] as KeyValueBlock)?.UpdateValue(ActionDisplay);
		(infoBlocks[5] as KeyValueBlock)?.UpdateValue(TargetDisplay);
	}

	public static string GetWorkerTypeLabel(AIWorker worker)
	{
		if (worker == null)
			return "Unknown";

		if (worker.WorkerKind == WorkerKind.Robot)
		{
			return worker.RobotType switch
			{
				RobotType.Transfer => "Robot / Transfer",
				_ => $"Robot / {worker.RobotType}",
			};
		}

		return worker.HumanType switch
		{
			HumanType.FullTime => "Human / FullTime",
			HumanType.PartTime => "Human / PartTime",
			HumanType.Illegal => "Human / Illegal",
			_ => $"Human / {worker.HumanType}",
		};
	}

	private static string BuildAbilityDisplay(WorkerAbility ability)
	{
		if (ability == WorkerAbility.None)
			return "None";

		StringBuilder builder = new();
		foreach (WorkerAbility flag in Enum.GetValues(typeof(WorkerAbility)))
		{
			if (flag == WorkerAbility.None || ability.HasFlag(flag) == false)
				continue;

			if (builder.Length > 0)
				builder.Append('\n');

			builder.Append(flag);
		}

		return builder.Length > 0 ? builder.ToString() : "None";
	}

	private static string BuildTaskTypeDisplay(AIWorker worker)
	{
		if (worker == null || worker.AssignedTaskTypes.Count == 0)
			return "Undefined";

		if (worker.AssignedTaskTypes.Count == 1)
			return GetTaskTypeDisplayName(worker.AssignedTaskTypes[0]);

		StringBuilder builder = new();
		for (int i = 0; i < worker.AssignedTaskTypes.Count; ++i)
		{
			if (i > 0)
				builder.Append(", ");

			builder.Append(GetTaskTypeDisplayName(worker.AssignedTaskTypes[i]));
		}

		return builder.ToString();
	}

	private static string GetTaskTypeDisplayName(WorkerTask.TaskType taskType)
	{
		return taskType switch
		{
			WorkerTask.TaskType.IB => "CapsuleRelocation (Inbound)",
			WorkerTask.TaskType.CapsuleClear => "CapsuleRelocation (Clear)",
			WorkerTask.TaskType.CapsuleSupply => "CapsuleRelocation (Supply)",
			WorkerTask.TaskType.OB => "CapsuleRelocation (Outbound)",
			_ => taskType.ToString(),
		};
	}
}
