using UnityEngine;

// 사고 확률 연산 방식
// 기본 확률 * 피로도 배율 * 부스트 계수 * 기타 감소된 확률(교육/장비)

public enum HumanIncidentResponseType
{
	None,
	AbortTask,				// by collapse
	WorkMistake			// by just mistake
}

public class HumanIncidentPayload
{
	public readonly HumanIncidentType type;
	public readonly HumanIncidentResponseType responseType;

	public HumanIncidentPayload(HumanIncidentType type, HumanIncidentResponseType responseType)
	{
		this.type = type;
		this.responseType = responseType;
	}
}


public class HumanIncidentService : MonoBehaviour
{
	[SerializeField] private HumanIncidentDefinition baseChance;

	private WorkPolicyService WorkPolicyService => GameContext.Instance.WMSys.WorkPolicyService;

	public HumanIncidentPayload TryCreateIncident(AIWorker worker, WorkActionType action)
	{
		float chance = GetIncidenceChance(worker, action);
		float random = Random.Range(20, 100);

		if (chance * 100.0f <= random)
			return null;
		
		Debug.Log($"사고 발생, chance: {chance * 100.0f}, rand: {random}, taskType: {worker.TaskType}, action: {action}");

		HumanIncidentType incidentType;
		HumanIncidentResponseType responseType;

		switch (action)
		{
			case WorkActionType.PickBox:
			case WorkActionType.PutBox:
				responseType = HumanIncidentResponseType.AbortTask;
				incidentType = HumanIncidentType.Collapse;
				break;

			default:
				responseType = HumanIncidentResponseType.WorkMistake;
				incidentType = HumanIncidentType.WorkMistake;
				break;
		}

		HumanIncidentPayload result = new HumanIncidentPayload(incidentType, responseType);
		return result;
	}

	private float GetIncidenceChance(AIWorker worker, WorkActionType workType)
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
