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
		};
	}

	public void RestoreState(ResearchServiceSaveData data)
	{
		UnbindWeekEvent();
		researchedIds.Clear();
		activeResearchId = null;
		remainingWeeks = 0;

		if (data == null)
		{
			OnResearchStateChanged?.Invoke();
			return;
		}

		if (data.ResearchedIds != null)
		{
			foreach (string researchId in data.ResearchedIds)
			{
				if (catalog != null && catalog.TryGet(researchId, out _))
					researchedIds.Add(researchId);
				else
					Debug.LogWarning($"[Research] Ignored unknown researched UID from save: {researchId}");
			}
		}

		if (data.RemainingWeeks > 0 &&
			catalog != null &&
			catalog.TryGet(data.ActiveResearchId, out _) &&
			researchedIds.Contains(data.ActiveResearchId) == false)
		{
			activeResearchId = data.ActiveResearchId;
			remainingWeeks = data.RemainingWeeks;
			BindWeekEvent();
		}

		OnResearchStateChanged?.Invoke();
	}
}
