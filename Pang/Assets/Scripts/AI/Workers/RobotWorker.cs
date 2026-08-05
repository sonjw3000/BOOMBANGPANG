using UnityEngine;

public class RobotWorker : AIWorker, IWearable
{
	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;
	[SerializeField] private float batteryLevel = 100f;
	[SerializeField] private ChargingType chargingType = ChargingType.Standard;
	[SerializeField] private RobotNavigationDependency navigationDependency = RobotNavigationDependency.HubOrchestrated;
	[SerializeField, Min(0)] private int requiredNavigationCompute = 100;
	private float batteryEfficiency;
	[SerializeField] private WearState wear = new();
	private int navigationRegionId;
	private int navigationCoverageVersion;

	public float BatteryLevel => batteryLevel;
	public ChargingType ChargingType => chargingType;
	public float Wear => wear.Wear;
	public float WearEfficiency => wear.Efficiency;
	public float PassiveWearPerQuarterWeek => wear.PassiveWearPerQuarterWeek;
	public float OperatingWearPerQuarterWeek => wear.OperatingWearPerQuarterWeek;
	public RobotNavigationDependency NavigationDependency => navigationDependency;
	public int RequiredNavigationCompute => navigationDependency == RobotNavigationDependency.HubOrchestrated
		? Mathf.Max(0, requiredNavigationCompute)
		: 0;
	public bool RequiresNavigationCoverage => navigationDependency != RobotNavigationDependency.FullyAutonomous;
	public bool RequiresOrchestrationCompute => navigationDependency == RobotNavigationDependency.HubOrchestrated;
	public int NavigationRegionId => navigationRegionId;
	public int NavigationCoverageVersion => navigationCoverageVersion;

	[SerializeField] private int monthlyMaintenanceCost = 100;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		SelectorNode root = new();
		root.Add(BuildRecoveryNode(WorkerStatusTarget.Charger, InteractionKind.Charge));
		return root;
	}

	public override void TickVitals(float deltaTime)
	{
		batteryLevel = Mathf.Max(0.0f, batteryLevel - Mathf.Max(0.0f, deltaTime) * 0.01f);
	}

	public override void AddFatigue(float fatigue)
	{
	}

	public override float GetFatigue() => 0;
	private float BatterySpeedMultiplier => batteryLevel <= 0.0f ? 0.01f : 1.0f;
	public override float GetWorkSpeedMultiplier() => BaseWorkSpeedMultiplier * WearEfficiency * BatterySpeedMultiplier;
	public override float GetMoveSpeedMultiplier() => BaseMoveSpeedMultiplier * WearEfficiency * BatterySpeedMultiplier;

	public void ApplyWear(float amount) => wear.Apply(amount);
	public void SetWearFromSave(float value) => wear.SetFromSave(value);

	internal void SetNavigationCache(int regionId, int coverageVersion)
	{
		navigationRegionId = Mathf.Max(0, regionId);
		navigationCoverageVersion = Mathf.Max(0, coverageVersion);
	}

	public override bool NeedsRecovery() => batteryLevel <= WorkPolicy.RobotChargeBatteryThreshold;

	public override bool IsRecoveryComplete() => batteryLevel >= WorkPolicy.RobotChargeTargetBattery;

	public override void TickRecovery(float recoveryPerSecond, float deltaTime)
	{
		batteryLevel = Mathf.Min(100.0f, batteryLevel + Mathf.Max(0.0f, recoveryPerSecond) * deltaTime);
	}

	public override WorkerStatusAction GetRecoveryAction() => WorkerStatusAction.Charging;

	protected override void CaptureSubclassState(WorkerSaveData data)
	{
		data.BatteryLevel = batteryLevel;
		data.BatteryEfficiency = batteryEfficiency;
		data.HasNavigationProfile = true;
		data.NavigationDependency = navigationDependency;
		data.RequiredNavigationCompute = requiredNavigationCompute;
	}

	protected override void RestoreSubclassState(WorkerSaveData data)
	{
		batteryLevel = data.BatteryLevel;
		batteryEfficiency = data.BatteryEfficiency;
		if (data.HasNavigationProfile)
		{
			navigationDependency = data.NavigationDependency;
			requiredNavigationCompute = Mathf.Max(0, data.RequiredNavigationCompute);
		}
	}
}
