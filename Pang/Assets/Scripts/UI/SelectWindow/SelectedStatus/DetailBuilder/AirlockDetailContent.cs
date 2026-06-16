using TMPro;
using UnityEngine;

public sealed class AirlockDetailContent : DetailContent<Airlock>
{
	[SerializeField] private DetailInfoRowView infoRowPrefab = null;

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
		if (infoRowPrefab == null)
		{
			Debug.LogError("[AirlockDetailContent] Info row prefab is missing.", this);
			return null;
		}

		DetailInfoRowView row = Instantiate(infoRowPrefab, InfoTabRoot);
		row.name = label.Replace(" ", string.Empty) + "Row";
		row.SetLabel(label);
		return row.ValueText;
	}
}
