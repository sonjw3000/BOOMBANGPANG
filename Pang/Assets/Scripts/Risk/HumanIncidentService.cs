using UnityEngine;

// 사고 확률 연산 방식
// 기본 확률 * 피로도 배율 * 부스트 계수 * 기타 감소된 확률(교육/장비)



public class HumanIncidentService : MonoBehaviour
{
	[SerializeField] private HumanIncidentDefinition baseChance;

	private WorkPolicyService WorkPolicyService => GameContext.Instance.WMSys.WorkPolicyService;

	public float GetIncidenceChance(AIWorker worker, WorkActionType workType)
	{
		float fatigue = worker.GetFatigue() / 100.0f;
		float boost = WorkPolicyService.GetBoost(worker, workType);
		float extraMultiplier = worker.GetIncidentMitigationMultiplier();

		float chance =
			baseChance.GetBaseIncidenceChance(HumanIncidentType.WorkMistake, worker)
			* fatigue
			* boost
			* extraMultiplier;

		return chance;
	}
}
