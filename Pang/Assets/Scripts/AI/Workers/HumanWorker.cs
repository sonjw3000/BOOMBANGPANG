using UnityEngine;

public class HumanWorker : AIWorker
{
	private static WorkPolicyService WorkPolicy => GameContext.Instance.WMSys.WorkPolicyService;
	private float experience;

	[SerializeField] private float fatigue;
	[SerializeField] private float fatigueIncreasePerTask = 2.0f;

	public float Fatigue => fatigue;

	protected override IBaseNode BuildWorkerBaseNode()
	{
		SelectorNode root = new SelectorNode();

		var incident = BuildHumanIncidentNode();
		var recovery = BuildRecoveryNode();
		var standby = BuildStandbyNode();
		root.Add(incident);
		root.Add(recovery);
		root.Add(standby);

		return root;
	}

	public override float GetWorkSpeedMultiplier()
	{
		return Mathf.Lerp(BaseWorkSpeedMultiplier, MinimumWorkSpeedMultiplier, fatigue / 100.0f);
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

	public override void TickRecovery(float deltaTime)
	{
		fatigue = Mathf.Max(0.0f, fatigue - WorkPolicy.WorkerRestRecoveryPerSecond * deltaTime);
	}

	public override WorkerStatusAction GetRecoveryAction() => WorkerStatusAction.Resting;

	public override ZoneType GetRecoveryZoneType() => ZoneType.Resting;

	protected override void CaptureSubclassState(WorkerSaveData data)
	{
		data.Fatigue = fatigue;
		data.Experience = experience;
	}

	protected override void RestoreSubclassState(WorkerSaveData data)
	{
		fatigue = data.Fatigue;
		experience = data.Experience;
	}
}
