using UnityEngine;

public class HumanWorker : AIWorker
{
	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;
	private float experience;

	[SerializeField] private float fatigue;
	[SerializeField] private float fatigueIncreasePerTask = 2.0f;
	[SerializeField] private bool isSuitRemoved;

	public float Fatigue => fatigue;
	public bool IsSuitRemoved => isSuitRemoved;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		SelectorNode root = new SelectorNode();
		root.Add(BuildHumanIncidentNode());
		root.Add(BuildRecoveryNode(WorkerStatusTarget.RestFacility, InteractionKind.Rest));

		return root;
	}

	public override float GetWorkSpeedMultiplier()
	{
		float workerMultiplier = Mathf.Lerp(
			BaseWorkSpeedMultiplier,
			MinimumWorkSpeedMultiplier,
			fatigue / 100.0f);
		if (isSuitRemoved == false || GameContext.HasInstance == false)
			return workerMultiplier;

		return workerMultiplier * GameContext.Instance.OxygenSvc.GetSuitlessWorkSpeedMultiplier(this);
	}

	public override float GetMoveSpeedMultiplier()
	{
		return Mathf.Lerp(BaseMoveSpeedMultiplier, MinimumMoveSpeedMultiplier, fatigue / 100.0f);
	}

	public override void OnTaskCompleted()
	{
		base.OnTaskCompleted();
		experience += 10.0f; // 경험치 증가
		fatigue += fatigueIncreasePerTask; // 피로도 증가
	}

	public override void TickVitals(float deltaTime)
	{
		//fatigue += deltaTime * 0.1f;
	}

	public override void AddFatigue(float fatigue)
	{
		this.fatigue += fatigue;
	}
	public override float GetFatigue()
	{
		return fatigue;
	}

	public override bool NeedsRecovery() => fatigue >= WorkPolicy.WorkerRestFatigueThreshold;

	public override bool IsRecoveryComplete() => fatigue <= WorkPolicy.WorkerRestTargetFatigue;

	public override void TickRecovery(float recoveryPerSecond, float deltaTime)
	{
		fatigue = Mathf.Max(0.0f, fatigue - Mathf.Max(0.0f, recoveryPerSecond) * deltaTime);
	}

	public override WorkerStatusAction GetRecoveryAction() => WorkerStatusAction.Resting;

	internal void PrepareForAirlockTransit(AirlockDirection direction)
	{
		if (direction == AirlockDirection.InsideToOutside)
			isSuitRemoved = false;
	}

	internal void ReconcileSuitStateFromCurrentLocation()
	{
		isSuitRemoved = CanRemoveSuitAtCurrentLocation();
	}

	internal void ForceSuitOn() => isSuitRemoved = false;

	private bool CanRemoveSuitAtCurrentLocation()
	{
		if (GameContext.HasInstance == false)
			return false;

		GridService gridService = GameContext.Instance.GridService;
		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		GridCell cell = gridService?.GetCell(GridPosition);
		return cell != null &&
			cell.IsIndoor &&
			buildingManager != null &&
			buildingManager.TryGetBuilding(cell.BuildingId, out Building building) &&
			building != null &&
			building.IsSuitRemovalPolicyActive;
	}

	protected override void CaptureSubclassState(WorkerSaveData data)
	{
		data.Fatigue = fatigue;
		data.Experience = experience;
		data.IsSuitRemoved = isSuitRemoved;
	}

	protected override void RestoreSubclassState(WorkerSaveData data)
	{
		fatigue = data.Fatigue;
		experience = data.Experience;
		isSuitRemoved = data.IsSuitRemoved && CanRemoveSuitAtCurrentLocation();
	}
}
