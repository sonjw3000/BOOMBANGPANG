using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FacilityRulePresetRowView : MonoBehaviour
{
	[SerializeField] private TMP_Text nameText = null;
	[SerializeField] private TMP_Text countText = null;
	[SerializeField] private Image colorImage = null;
	[SerializeField] private TextButtonView listButton = null;
	[SerializeField] private TextButtonView editButton = null;
	[SerializeField] private TextButtonView applyButton = null;

	private uint presetId;
	private Action<uint> listRequested;
	private Action<uint> editRequested;
	private Action<uint> applyRequested;

	public uint PresetId => presetId;

	public void Configure(
		FacilityRulePreset preset,
		int appliedCount,
		Action<uint> onListRequested,
		Action<uint> onEditRequested,
		Action<uint> onApplyRequested)
	{
		presetId = preset != null ? preset.Id : FacilityRuleManager.NoRulePresetId;
		listRequested = onListRequested;
		editRequested = onEditRequested;
		applyRequested = onApplyRequested;

		if (nameText != null)
			nameText.text = preset != null ? preset.DisplayName : "None";

		if (countText != null)
			countText.text = appliedCount.ToString();

		if (colorImage != null)
			colorImage.color = preset != null ? preset.Color : Color.white;

		listButton?.Configure("List", HandleListClicked);
		editButton?.Configure("Edit", HandleEditClicked);
		applyButton?.Configure("Apply", HandleApplyClicked);

		if (listButton?.Button != null)
			listButton.Button.interactable = preset != null;
		if (editButton?.Button != null)
			editButton.Button.interactable = preset != null;
		if (applyButton?.Button != null)
			applyButton.Button.interactable = preset != null;
	}

	public void Clear()
	{
		presetId = FacilityRuleManager.NoRulePresetId;
		listRequested = null;
		editRequested = null;
		applyRequested = null;

		if (nameText != null)
			nameText.text = string.Empty;
		if (countText != null)
			countText.text = string.Empty;
		if (colorImage != null)
			colorImage.color = Color.clear;

		gameObject.SetActive(false);
	}

	private void HandleListClicked()
	{
		if (presetId != FacilityRuleManager.NoRulePresetId)
			listRequested?.Invoke(presetId);
	}

	private void HandleEditClicked()
	{
		if (presetId != FacilityRuleManager.NoRulePresetId)
			editRequested?.Invoke(presetId);
	}

	private void HandleApplyClicked()
	{
		if (presetId != FacilityRuleManager.NoRulePresetId)
			applyRequested?.Invoke(presetId);
	}
}
