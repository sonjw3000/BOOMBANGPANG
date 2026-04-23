using Unity.Properties;
using Unity.VisualScripting;
using UnityEngine;

public class HumanWorkerUIProvider : UIProvider<HumanWorker>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Worker";
	public override Sprite Icon => null; // Placeholder for shelf icon

	public float Fatigue => currentTarget != null ? currentTarget.Fatigue : 0f;
	//public int3 GoalPosition => currentTarget != null ? currentTarget


	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Fatigue", $"{Fatigue}%"));
		infoBlocks.Add(new KeyValueBlock("MoveSpeed", $"x{currentTarget.GetMoveSpeedMultiplier()}"));
		infoBlocks.Add(new KeyValueBlock("Position", $"{currentTarget.GridPosition}"));
		infoBlocks.Add(new KeyValueBlock("Action", $"{currentTarget.WorkerState.Action}"));
		infoBlocks.Add(new KeyValueBlock("Target", $"{currentTarget.WorkerState.Target}"));
	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock).UpdateValue($"{Fatigue}%");
		(infoBlocks[1] as KeyValueBlock).UpdateValue($"x{currentTarget.GetMoveSpeedMultiplier()}");
		(infoBlocks[2] as KeyValueBlock).UpdateValue($"{currentTarget.GridPosition}");
		(infoBlocks[3] as KeyValueBlock).UpdateValue($"{currentTarget.WorkerState.Action}");
		(infoBlocks[4] as KeyValueBlock).UpdateValue($"{currentTarget.WorkerState.Target}");
	}
}
