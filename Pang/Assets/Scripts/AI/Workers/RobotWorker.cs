using UnityEngine;

public class RobotWorker : AIWorker
{
	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;
	[SerializeField] private float batteryLevel = 100f;
	private float batteryEfficiency;

	public float BatteryLevel => batteryLevel;

	[SerializeField] private int monthlyMaintenanceCost = 100;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		SelectorNode root = new();
		root.Add(BuildRecoveryNode());
		root.Add(BuildStandbyNode());

		return root;
	}

	public override void TickVitals(float deltaTime)
	{
		batteryEfficiency -= deltaTime * 0.01f;

		// todo
		// efficiency가 낮아질수록 배터리 소모량이 늘어나도록 해야

		batteryLevel -= deltaTime * 0.01f;
	}

	public override void AddFatigue(float fatigue)
	{
	}

	public override float GetFatigue() => 0;

	public override bool NeedsRecovery() => batteryLevel <= WorkPolicy.RobotChargeBatteryThreshold;

	public override bool IsRecoveryComplete() => batteryLevel >= WorkPolicy.RobotChargeTargetBattery;

	public override void TickRecovery(float deltaTime)
	{
		batteryLevel = Mathf.Min(100.0f, batteryLevel + WorkPolicy.RobotChargeRecoveryPerSecond * deltaTime);
	}

	public override WorkerStatusAction GetRecoveryAction() => WorkerStatusAction.Charging;

	public override ZoneType GetRecoveryZoneType() => ZoneType.Charge;

	protected override void CaptureSubclassState(WorkerSaveData data)
	{
		data.BatteryLevel = batteryLevel;
		data.BatteryEfficiency = batteryEfficiency;
	}

	protected override void RestoreSubclassState(WorkerSaveData data)
	{
		batteryLevel = data.BatteryLevel;
		batteryEfficiency = data.BatteryEfficiency;
	}
}
