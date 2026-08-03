using UnityEngine;

public sealed class ChargingFacility : RecoveryFacilityBase
{
	[SerializeField] private ChargingType chargingType = ChargingType.Standard;
	[SerializeField, Min(0)] private int powerPerActiveRobot = 50;

	public override InteractionKind RecoveryInteractionKind => InteractionKind.Charge;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.Charger;
	public override int PowerConsumption => ActiveUserCount * Mathf.Max(0, powerPerActiveRobot);
	public ChargingType ChargingType => chargingType;
	public int PowerPerActiveRobot => Mathf.Max(0, powerPerActiveRobot);

	public override bool CanServe(AIWorker worker)
	{
		return worker is RobotWorker robot &&
			chargingType != ChargingType.None &&
			robot.ChargingType == chargingType;
	}

	protected override float GetOperatingEfficiency()
	{
		return this.GetPowerEfficiency();
	}

	protected override void OnActiveUsersChanged()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.FacilityMgr?.ReportPowerConsumptionChanged(this);
	}
}
