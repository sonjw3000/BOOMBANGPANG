using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AirlockDetailContent : DetailContent<Airlock>
{
	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI reservedWorkerValue;
	private TextMeshProUGUI directionValue;
	private TextMeshProUGUI delayValue;
	private TextMeshProUGUI positionValue;
	private bool uiBuilt;

	protected override void LinkData()
	{
		EnsureUi();
		RefreshAll();
	}

	protected override void UpdateData()
	{
		RefreshAll();
	}

	protected override void BuildActionButtons(RectTransform actionRoot)
	{
		RegisterActionButton(CreateDeleteActionButton(actionRoot));
		RegisterActionButton(CreateRuntimeActionButton(actionRoot, "Release Reservation", () =>
		{
			if (provider is AirlockUIProvider airlockProvider && airlockProvider.Target != null)
				GameContext.Instance.AirlockSvc.Release(airlockProvider.Target, null);
		}));
	}

	private void EnsureUi()
	{
		if (uiBuilt)
			return;

		nameValue = CreateInfoLine("Name");
		typeValue = CreateInfoLine("Type");
		stateValue = CreateInfoLine("State");
		reservedWorkerValue = CreateInfoLine("Reserved Worker");
		directionValue = CreateInfoLine("Direction");
		delayValue = CreateInfoLine("Entry Delay");
		positionValue = CreateInfoLine("Grid Position");
		uiBuilt = true;
	}

	private void RefreshAll()
	{
		if (provider is not AirlockUIProvider airlockProvider)
			return;

		Airlock target = airlockProvider.Target;
		nameValue.text = airlockProvider.Name;
		typeValue.text = airlockProvider.Subtitle;
		stateValue.text = airlockProvider.StateDisplay;
		reservedWorkerValue.text = airlockProvider.ReservedWorkerDisplay;
		directionValue.text = airlockProvider.DirectionDisplay;
		delayValue.text = airlockProvider.DelayDisplay;
		positionValue.text = target != null ? target.GridPosition.ToString() : "(0,0,0)";
	}

	private TextMeshProUGUI CreateInfoLine(string label)
	{
		GameObject rowObject = new(label.Replace(" ", string.Empty) + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		rowObject.transform.SetParent(InfoTabRoot, false);

		HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = 10f;
		layout.childAlignment = TextAnchor.MiddleLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;

		LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
		rowLayout.minHeight = 26f;
		rowLayout.preferredHeight = 30f;
		rowLayout.flexibleWidth = 1f;

		CreateLabelText(rowObject.transform, label);
		return CreateValueText(rowObject.transform);
	}

	private static TextMeshProUGUI CreateLabelText(Transform parent, string label)
	{
		GameObject textObject = new(label.Replace(" ", string.Empty) + "Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		LayoutElement layout = textObject.GetComponent<LayoutElement>();
		layout.minWidth = 140f;
		layout.preferredWidth = 140f;

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = label;
		text.fontSize = 22f;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.Left;
		return text;
	}

	private static TextMeshProUGUI CreateValueText(Transform parent)
	{
		GameObject textObject = new("Value", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		LayoutElement layout = textObject.GetComponent<LayoutElement>();
		layout.flexibleWidth = 1f;

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = "-";
		text.fontSize = 22f;
		text.color = new Color(0.86f, 0.9f, 0.96f, 1f);
		text.alignment = TextAlignmentOptions.Left;
		return text;
	}
}
