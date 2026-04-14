using UnityEngine;

public class RobotWorker : AIWorker
{
	private float batteryLevel;
	private float batteryEfficiency;

	[SerializeField] private int montlyMaintenanceCost = 100;

	public override void TickVitals(float deltaTime)
	{
		batteryEfficiency -= deltaTime * 0.01f;

		// todo
		// efficiency가 낮아질수록 배터리 소모량이 늘어나도록 해야

		batteryLevel -= deltaTime * 0.01f;
	}

	public override void AddFatigue(float fatigue)
	{
		// todo
		// st battery
	}

	public override float GetFatigue() => 0;
}
