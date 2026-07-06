using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FacilityRulePresetRowView : MonoBehaviour
{
	[SerializeField] private TMP_Text nameText = null;
	[SerializeField] private TMP_Text countText = null;
	[SerializeField] private Image colorImage = null;
	[SerializeField] private TextButtonView editButton = null;
	[SerializeField] private TextButtonView applyButton = null;

	private uint presetId;
	private Action<uint> editRequested;
	private Action<uint> applyRequested;

	public uint PresetId => presetId;

	public void Configure(FacilityRulePreset preset, int appliedCount, Action<uint> onEditRequested, Action<uint> onApplyRequested)
	{
		presetId = preset != null ? preset.Id : FacilityRuleManager.NoRulePresetId;
		editRequested = onEditRequested;
		applyRequested = onApplyRequested;

		if (nameText != null)
			nameText.text = preset != null ? preset.DisplayName : "None";

		if (countText != null)
			countText.text = appliedCount.ToString();

		if (colorImage != null)
			colorImage.color = preset != null ? preset.Color : Color.white;

		editButton?.Configure("Edit", HandleEditClicked);
		applyButton?.Configure("Apply", HandleApplyClicked);

		if (editButton?.Button != null)
			editButton.Button.interactable = preset != null;
		if (applyButton?.Button != null)
			applyButton.Button.interactable = preset != null;
	}

	public void Clear()
	{
		presetId = FacilityRuleManager.NoRulePresetId;
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
