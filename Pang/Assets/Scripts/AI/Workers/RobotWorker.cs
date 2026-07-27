using UnityEngine;

public class RobotWorker : AIWorker, IWearable
{
	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;
	[SerializeField] private float batteryLevel = 100f;
	private float batteryEfficiency;
	[SerializeField] private WearState wear = new();

	public float BatteryLevel => batteryLevel;
	public float Wear => wear.Wear;
	public float WearEfficiency => wear.Efficiency;
	public float PassiveWearPerQuarterWeek => wear.PassiveWearPerQuarterWeek;
	public float OperatingWearPerQuarterWeek => wear.OperatingWearPerQuarterWeek;

	[SerializeField] private int monthlyMaintenanceCost = 100;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		return null;
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
	public override float GetWorkSpeedMultiplier() => BaseWorkSpeedMultiplier * WearEfficiency;
	public override float GetMoveSpeedMultiplier() => BaseMoveSpeedMultiplier * WearEfficiency;

	public void ApplyWear(float amount) => wear.Apply(amount);
	public void SetWearFromSave(float value) => wear.SetFromSave(value);

	public override bool NeedsRecovery() => batteryLevel <= WorkPolicy.RobotChargeBatteryThreshold;

	public override bool IsRecoveryComplete() => batteryLevel >= WorkPolicy.RobotChargeTargetBattery;

	public override void TickRecovery(float deltaTime)
	{
		batteryLevel = Mathf.Min(100.0f, batteryLevel + WorkPolicy.RobotChargeRecoveryPerSecond * deltaTime);
	}

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
