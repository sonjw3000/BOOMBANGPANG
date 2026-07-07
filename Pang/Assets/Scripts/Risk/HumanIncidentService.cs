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
		PublishHudEvent(worker, action, result);
		return result;
	}

	private static void PublishHudEvent(AIWorker worker, WorkActionType action, HumanIncidentPayload payload)
	{
		if (payload == null || GameContext.HasInstance == false)
			return;

		string workerName = worker != null && string.IsNullOrWhiteSpace(worker.Name) == false
			? worker.Name
			: "Worker";
		string incidentLabel = payload.type == HumanIncidentType.Collapse ? "collapsed" : "made a work mistake";
		string actionLabel = FormatEnumLabel(action.ToString());

		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Warning,
			$"{workerName} {incidentLabel} during {actionLabel}",
			worker);
	}

	private static string FormatEnumLabel(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return string.Empty;

		System.Text.StringBuilder builder = new(raw.Length + 8);
		for (int i = 0; i < raw.Length; ++i)
		{
			char current = raw[i];
			if (i > 0 && char.IsUpper(current) && char.IsLower(raw[i - 1]))
				builder.Append(' ');

			builder.Append(current);
		}

		return builder.ToString();
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
