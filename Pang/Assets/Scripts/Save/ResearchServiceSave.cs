using System.Collections.Generic;
using UnityEngine;

public sealed partial class ResearchService
{
	public ResearchServiceSaveData CaptureState()
	{
		return new ResearchServiceSaveData
		{
			ResearchedIds = new List<string>(researchedIds),
			ActiveResearchId = activeResearchId,
			RemainingWeeks = remainingWeeks,
			QueuedResearchIds = new List<string>(queuedResearchIds),
		};
	}

	public void RestoreState(ResearchServiceSaveData data)
	{
		UnbindWeekEvent();
		researchedIds.Clear();
		queuedResearchIds.Clear();
		activeResearchId = null;
		remainingWeeks = 0;
		isRestoringState = true;

		try
		{
			if (data != null && data.ResearchedIds != null)
			{
				foreach (string researchId in data.ResearchedIds)
				{
					if (catalog != null && catalog.TryGet(researchId, out _))
						researchedIds.Add(researchId);
					else
						Debug.LogWarning($"[Research] Ignored unknown researched UID from save: {researchId}");
				}
			}

			if (data != null &&
				(researchedIds.Contains(ResearchIds.ThermalOperations) ||
				data.ActiveResearchId == ResearchIds.ThermalOperations))
			{
				researchedIds.Add(ResearchIds.TemperatureMonitoring);
			}

			if (data != null &&
				data.RemainingWeeks > 0 &&
				catalog != null &&
				catalog.TryGet(data.ActiveResearchId, out _) &&
				researchedIds.Contains(data.ActiveResearchId) == false)
			{
				activeResearchId = data.ActiveResearchId;
				remainingWeeks = data.RemainingWeeks;
			}

			RestoreQueuedResearch(data?.QueuedResearchIds);
		}
		finally
		{
			isRestoringState = false;
		}

		if (IsResearching)
			BindWeekEvent();
		else
			TryStartNextQueuedResearch();

		OnResearchStateChanged?.Invoke();
	}

	private void RestoreQueuedResearch(IReadOnlyList<string> savedQueue)
	{
		if (savedQueue == null || catalog == null)
			return;

		HashSet<string> plannedResearchIds = new(researchedIds, System.StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(activeResearchId) == false)
			plannedResearchIds.Add(activeResearchId);

		for (int i = 0; i < savedQueue.Count; ++i)
		{
			string researchId = savedQueue[i];
			if (string.IsNullOrWhiteSpace(researchId) ||
				catalog.TryGet(researchId, out ResearchDefinition definition) == false)
			{
				Debug.LogWarning($"[Research] Ignored unknown queued UID from save: {researchId}");
				continue;
			}

			if (plannedResearchIds.Contains(researchId))
			{
				Debug.LogWarning($"[Research] Ignored duplicate queued UID from save: {researchId}");
				continue;
			}

			if (ArePrerequisitesSatisfied(definition, plannedResearchIds) == false)
			{
				Debug.LogWarning($"[Research] Ignored out-of-order queued UID from save: {researchId}");
				continue;
			}

			queuedResearchIds.Add(researchId);
			plannedResearchIds.Add(researchId);
		}
	}
}
