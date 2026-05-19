using UnityEngine;

public class RobotWorkerUIProvider : UIProvider<RobotWorker>
{
	public override string Name => currentTarget != null ? currentTarget.Name : "Unknown Worker";
	public override Sprite Icon => null; // Placeholder for shelf icon

	public float BatteryLevel => currentTarget != null ? currentTarget.BatteryLevel : 0f;
	//public int3 GoalPosition => currentTarget != null ? currentTarget


	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("BatteryLevel", $"{BatteryLevel}%"));
		infoBlocks.Add(new KeyValueBlock("MoveSpeed", $"x{currentTarget.GetMoveSpeedMultiplier()}"));
		infoBlocks.Add(new KeyValueBlock("Position", $"{currentTarget.GridPosition}"));
		infoBlocks.Add(new KeyValueBlock("AssignedTaskType", $"{currentTarget.TaskType}"));
		infoBlocks.Add(new KeyValueBlock("Action", $"{currentTarget.WorkerState.Action}"));
		infoBlocks.Add(new KeyValueBlock("Target", $"{currentTarget.WorkerState.Target}"));
	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock).UpdateValue($"{BatteryLevel}%");
		(infoBlocks[1] as KeyValueBlock).UpdateValue($"x{currentTarget.GetMoveSpeedMultiplier()}");
		(infoBlocks[2] as KeyValueBlock).UpdateValue($"{currentTarget.GridPosition}");
		(infoBlocks[3] as KeyValueBlock).UpdateValue($"{currentTarget.TaskType}");
		(infoBlocks[4] as KeyValueBlock).UpdateValue($"{currentTarget.WorkerState.Action}");
		(infoBlocks[5] as KeyValueBlock).UpdateValue($"{currentTarget.WorkerState.Target}");
	}
}
