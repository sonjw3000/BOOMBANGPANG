using AYellowpaper.SerializedCollections;
using UnityEngine;

public enum HumanIncidentType
{
	WorkMistake,
	Collapse
}

[CreateAssetMenu(menuName ="Risk/Human Incident Definition")]
public class HumanIncidentDefinition : ScriptableObject
{
	[Header("It's Percentage 0 ~ 1")]
	[SerializedDictionary("HumanIncidentType", "IncidentType/BaseChance")]
	[SerializeField] private SerializedDictionary<WorkerPolicyType, SerializedDictionary<HumanIncidentType, float>> incidenceChance;

	public float GetBaseIncidenceChance(HumanIncidentType type, AIWorker worker)
		=> incidenceChance[worker.WorkerPolicyType][type];
}
