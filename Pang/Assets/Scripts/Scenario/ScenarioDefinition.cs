using System;
using System.Collections.Generic;
using Assets.Scripts.Contract.ItemContract;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenario/Scenario Definition")]
public sealed class ScenarioDefinition : ScriptableObject
{
	[SerializeField] private string scenarioId;
	[SerializeField] private string displayName;
	[SerializeField] private List<ScenarioObjectiveDefinition> objectives = new();

	public string ScenarioId => scenarioId;
	public string DisplayName => displayName;
	public IReadOnlyList<ScenarioObjectiveDefinition> Objectives => objectives;

	public bool Validate(out string error)
	{
		if (string.IsNullOrWhiteSpace(scenarioId))
		{
			error = "Scenario ID is empty.";
			return false;
		}

		HashSet<string> objectiveIds = new();
		for (int i = 0; i < objectives.Count; ++i)
		{
			ScenarioObjectiveDefinition objective = objectives[i];
			if (objective == null)
			{
				error = $"Objective {i} is null.";
				return false;
			}

			if (objective.Validate(out error) == false)
			{
				error = $"Objective {i}: {error}";
				return false;
			}

			if (objectiveIds.Add(objective.ObjectiveId) == false)
			{
				error = $"Objective ID is duplicated: {objective.ObjectiveId}";
				return false;
			}
		}

		if (objectives.Count == 0)
		{
			error = "Scenario has no objectives.";
			return false;
		}

		error = string.Empty;
		return true;
	}
}

[Serializable]
public sealed class ScenarioObjectiveDefinition
{
	[Header("Identity")]
	[SerializeField] private string objectiveId;
	[SerializeField] private string title;
	[TextArea]
	[SerializeField] private string description;

	[Header("Order Requirement")]
	[SerializeField, Min(0)] private int requiredSettledOrderCount;
	[SerializeField] private bool requireOnTime;
	[SerializeField] private ContractDefinition targetContract;

	[Header("Research Requirement")]
	[SerializeField] private List<string> requiredResearchUids = new();

	[Header("Reputation Requirement")]
	[SerializeField] private bool requireMinimumReputation;
	[SerializeField] private float minimumReputation;

	public string ObjectiveId => objectiveId;
	public string Title => title;
	public string Description => description;
	public int RequiredSettledOrderCount => requiredSettledOrderCount;
	public bool RequireOnTime => requireOnTime;
	public ContractDefinition TargetContract => targetContract;
	public IReadOnlyList<string> RequiredResearchUids => requiredResearchUids;
	public bool RequireMinimumReputation => requireMinimumReputation;
	public float MinimumReputation => minimumReputation;
	public bool HasOrderRequirement => requiredSettledOrderCount > 0;

	public bool Validate(out string error)
	{
		if (string.IsNullOrWhiteSpace(objectiveId))
		{
			error = "Objective ID is empty.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(title))
		{
			error = $"Objective '{objectiveId}' has no title.";
			return false;
		}

		bool hasResearchRequirement = requiredResearchUids != null && requiredResearchUids.Count > 0;
		if (HasOrderRequirement == false && hasResearchRequirement == false && requireMinimumReputation == false)
		{
			error = $"Objective '{objectiveId}' has no completion requirement.";
			return false;
		}

		if (requiredResearchUids != null)
		{
			for (int i = 0; i < requiredResearchUids.Count; ++i)
			{
				if (string.IsNullOrWhiteSpace(requiredResearchUids[i]))
				{
					error = $"Objective '{objectiveId}' has an empty research UID.";
					return false;
				}
			}
		}

		error = string.Empty;
		return true;
	}
}
