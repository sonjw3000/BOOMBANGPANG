using Assets.Scripts.UI;
using System.Collections.Generic;
using UnityEngine;

public sealed class FacilityRuleFacilityListWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window = null;
	[SerializeField] private FacilityRuleFacilityListWindowView view = null;
	[SerializeField] private string emptyMessage = "No facilities use this preset.";

	private bool initialized;

	private FacilityRuleManager RuleManager => GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;

	private void Awake()
	{
		EnsureInitialized();
	}

	public void OpenForPreset(FacilityRulePreset preset)
	{
		EnsureInitialized();
		if (preset == null || window == null)
			return;

		if (gameObject.activeSelf == false)
			gameObject.SetActive(true);

		window.SetTitle($"{preset.DisplayName} Facilities");
		RefreshRows(preset);
		window.Open();
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		window ??= GetComponent<UIWindow>();
		window ??= GetComponentInChildren<UIWindow>(true);
		view ??= GetComponentInChildren<FacilityRuleFacilityListWindowView>(true);

		if (window == null || view == null)
			return;

		window.Close();
		initialized = true;
	}

	private void RefreshRows(FacilityRulePreset preset)
	{
		TextRowView[] rows = view.FacilityRows;
		if (rows == null)
			return;

		for (int i = 0; i < rows.Length; ++i)
		{
			if (rows[i] == null)
				continue;

			rows[i].gameObject.SetActive(false);
		}

		IReadOnlyList<IFacility> facilities = RuleManager != null
			? RuleManager.GetFacilitiesForPreset(preset.Id)
			: null;

		int totalCount = 0;
		int visibleCount = 0;
		if (facilities != null)
		{
			for (int i = 0; i < facilities.Count; ++i)
			{
				IFacility facility = facilities[i];
				if (facility == null)
					continue;

				totalCount += 1;
				if (visibleCount >= rows.Length || rows[visibleCount] == null)
					continue;

				rows[visibleCount].gameObject.SetActive(true);
				if (rows[visibleCount].Text != null)
					rows[visibleCount].Text.text = $"{visibleCount + 1}. {GetFacilityName(facility)}";

				visibleCount += 1;
			}
		}

		if (view.StatusRow?.Text != null)
		{
			view.StatusRow.Text.text = totalCount > 0
				? $"Showing {visibleCount}/{totalCount} facilities using {preset.DisplayName}."
				: emptyMessage;
		}
	}

	private static string GetFacilityName(IFacility facility)
	{
		if (facility is Component component && component != null)
			return component.name;

		return facility != null ? facility.ToString() : "Unknown Facility";
	}
}
