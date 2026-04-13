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
	}
}
