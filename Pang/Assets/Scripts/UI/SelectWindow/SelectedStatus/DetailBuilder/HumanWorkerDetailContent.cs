using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HumanWorkerDetailContent : DetailContent<AIWorker>
{
	private enum WorkerDetailTab
	{
		Basic,
		Status,
		Task,
		Carry,
		Action,
	}

	private UIWindow window;
	private RectTransform bodyRoot;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<Button> actionButtons = new();
	private WorkerDetailTab currentTab;

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI workerIdValue;
	private TextMeshProUGUI resourceValue;
	private TextMeshProUGUI moveSpeedValue;
	private TextMeshProUGUI workSpeedValue;
	private TextMeshProUGUI mainTaskTypeValue;
	private TextMeshProUGUI abilityValue;
	private TextMeshProUGUI monthlyCostValue;

	private TextMeshProUGUI positionValue;
	private TextMeshProUGUI destinationValue;
	private TextMeshProUGUI actionValue;
	private TextMeshProUGUI targetValue;

	private Button currentTaskButton;
	private TextMeshProUGUI currentTaskButtonLabel;
	private TextMeshProUGUI currentTaskSummary;

	private TextMeshProUGUI carryStateLabel;
	private TextMeshProUGUI carryFillLabel;
	private RectTransform carryListRoot;
	private readonly List<GameObject> carryRows = new();
	private RectTransform actionRoot;

	private bool uiBuilt;

	protected override void AddListener()
	{
	}

	protected override void RemoveListeners()
	{
		currentTaskButton?.onClick.RemoveListener(OnTaskButtonClicked);
		RemoveCarryRowListeners();
		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				actionButton.onClick.RemoveAllListeners();
		}
	}

	protected override void LinkData()
	{
		EnsureUi();
		BuildActionTab();
		currentTaskButton.onClick.RemoveListener(OnTaskButtonClicked);
		currentTaskButton.onClick.AddListener(OnTaskButtonClicked);
		SetupTabs();
		SetTab((int)WorkerDetailTab.Basic);
		RefreshAll();
	}

	protected override void UpdateData()
	{
		RefreshAll();
	}

	private void EnsureUi()
	{
		if (uiBuilt)
			return;

		HideLegacyVisuals();
		window = GetComponentInParent<UIWindow>(true);
		Transform contentParent = window != null ? window.ContentRoot : transform;
		bodyRoot = CreateVerticalContainer("WorkerDetailBody", contentParent, 10f);
		SetStretch(bodyRoot, 12f, 12f, 12f, 12f);

		GameObject basicTab = CreateVerticalContainer("BasicTab", bodyRoot, 8f).gameObject;
		nameValue = AddLabeledValue(basicTab.transform, "Name");
		typeValue = AddLabeledValue(basicTab.transform, "Type");
		workerIdValue = AddLabeledValue(basicTab.transform, "ID");
		resourceValue = AddLabeledValue(basicTab.transform, "Resource");
		moveSpeedValue = AddLabeledValue(basicTab.transform, "Move Speed");
		workSpeedValue = AddLabeledValue(basicTab.transform, "Work Speed");
		mainTaskTypeValue = AddLabeledValue(basicTab.transform, "Main TaskType");
		abilityValue = AddLabeledValue(basicTab.transform, "Abilities");
		monthlyCostValue = AddLabeledValue(basicTab.transform, "Monthly Cost");
		tabRoots.Add(basicTab);

		GameObject statusTab = CreateVerticalContainer("StatusTab", bodyRoot, 8f).gameObject;
		positionValue = AddLabeledValue(statusTab.transform, "Position");
		destinationValue = AddLabeledValue(statusTab.transform, "Destination");
		actionValue = AddLabeledValue(statusTab.transform, "Action");
		targetValue = AddLabeledValue(statusTab.transform, "Target");
		tabRoots.Add(statusTab);

		GameObject taskTab = CreateVerticalContainer("TaskTab", bodyRoot, 8f).gameObject;
		currentTaskButton = CreateButton("CurrentTaskButton", taskTab.transform, out currentTaskButtonLabel, 42f);
		currentTaskSummary = CreateBodyText("CurrentTaskSummary", taskTab.transform);
		tabRoots.Add(taskTab);

		GameObject carryTab = CreateVerticalContainer("CarryTab", bodyRoot, 8f).gameObject;
		carryStateLabel = CreateBodyText("CarryState", carryTab.transform);
		carryFillLabel = CreateBodyText("CarryFill", carryTab.transform);
		carryListRoot = CreateVerticalContainer("CarryList", carryTab.transform, 8f);
		tabRoots.Add(carryTab);

		GameObject actionTab = CreateVerticalContainer("ActionTab", bodyRoot, 8f).gameObject;
		actionRoot = CreateVerticalContainer("ActionRoot", actionTab.transform, 8f);
		tabRoots.Add(actionTab);

		uiBuilt = true;
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Basic", SetTab);
		window.AddTab("Status", SetTab);
		window.AddTab("Task", SetTab);
		window.AddTab("Carry", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals((int)currentTab);
	}

	private void SetTab(int tabIndex)
	{
		currentTab = (WorkerDetailTab)tabIndex;

		for (int i = 0; i < tabRoots.Count; i++)
		{
			tabRoots[i].SetActive(i == tabIndex);
		}

		window?.UpdateTabVisuals(tabIndex);
	}

	private void RefreshAll()
	{
		if (provider is not IWorkerUIProvider workerProvider)
			return;

		nameValue.text = workerProvider.Name;
		typeValue.text = workerProvider.WorkerTypeLabel;
		workerIdValue.text = workerProvider.Target != null ? workerProvider.Target.WorkerID.ToString() : "0";
		resourceValue.text = $"{GetResourceLabel(workerProvider)}: {workerProvider.ResourceDisplay}";
		moveSpeedValue.text = workerProvider.MoveSpeedDisplay;
		workSpeedValue.text = workerProvider.WorkSpeedDisplay;
		mainTaskTypeValue.text = workerProvider.MainTaskTypeDisplay;
		abilityValue.text = workerProvider.AbilityDisplay;
		monthlyCostValue.text = workerProvider.MonthlyCostDisplay;

		positionValue.text = workerProvider.PositionDisplay;
		destinationValue.text = workerProvider.DestinationDisplay;
		actionValue.text = workerProvider.ActionDisplay;
		targetValue.text = workerProvider.TargetDisplay;

		currentTaskButtonLabel.text = workerProvider.CurrentTaskButtonLabel;
		currentTaskButton.interactable = workerProvider.HasAssignedTask;
		currentTaskSummary.text = workerProvider.CurrentTaskSummary;

		RefreshCarryTab(workerProvider);
	}

	private void RefreshCarryTab(IWorkerUIProvider workerProvider)
	{
		RemoveCarryRowListeners();
		foreach (GameObject row in carryRows)
		{
			if (row != null)
				Destroy(row);
		}
		carryRows.Clear();

		if (workerProvider.HasCarriedBox == false)
		{
			carryStateLabel.text = "No carrying box.";
			carryFillLabel.text = string.Empty;
			return;
		}

		carryStateLabel.text = "Carrying box.";
		carryFillLabel.text = $"Filled: {workerProvider.CarriedBoxFillDisplay}";

		foreach (WorkerBoxStackDisplayInfo stackInfo in workerProvider.GetCarriedBoxStacks())
		{
			GameObject row = CreateCarryRow(stackInfo);
			carryRows.Add(row);
		}
	}

	private GameObject CreateCarryRow(WorkerBoxStackDisplayInfo stackInfo)
	{
		RectTransform rowRoot = CreateHorizontalContainer($"{stackInfo.ItemName}Row", carryListRoot, 8f);
		rowRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

		Button itemButton = CreateButton("ItemButton", rowRoot, out TextMeshProUGUI itemButtonText, 32f);
		itemButtonText.text = string.Empty;
		itemButton.onClick.AddListener(() => Debug.Log($"[WorkerDetail] Item button clicked. item={stackInfo.ItemName}"));

		TextMeshProUGUI quantityLabel = CreateBodyText("QuantityLabel", rowRoot);
		quantityLabel.text = $"{stackInfo.ItemName}: {stackInfo.Quantity}";

		Button orderButton = CreateButton("OrderButton", rowRoot, out TextMeshProUGUI orderButtonText, 32f);
		orderButtonText.text = stackInfo.RelatedOrderId.HasValue ? $"Order #{stackInfo.RelatedOrderId.Value}" : string.Empty;
		orderButton.gameObject.SetActive(stackInfo.RelatedOrderId.HasValue);
		if (stackInfo.RelatedOrderId.HasValue)
		{
			int orderId = stackInfo.RelatedOrderId.Value;
			orderButton.onClick.AddListener(() => Debug.Log($"[WorkerDetail] Order button clicked. orderId={orderId}"));
		}

		return rowRoot.gameObject;
	}

	private void OnTaskButtonClicked()
	{
		if (provider is not IWorkerUIProvider workerProvider || workerProvider.Target?.CurrentTask == null)
			return;

		Debug.Log($"[WorkerDetail] Task button clicked. task={workerProvider.Target.CurrentTask.GetType().Name}");
	}

	private void BuildActionTab()
	{
		if (actionRoot == null)
			return;

		foreach (Button actionButton in actionButtons)
		{
			if (actionButton != null)
				Destroy(actionButton.gameObject);
		}

		actionButtons.Clear();
		actionButtons.Add(CreateDeleteActionButton(actionRoot));
	}

	private void RemoveCarryRowListeners()
	{
		foreach (GameObject row in carryRows)
		{
			if (row == null)
				continue;

			foreach (Button button in row.GetComponentsInChildren<Button>(true))
			{
				button.onClick.RemoveAllListeners();
			}
		}
	}

	private static string GetResourceLabel(IWorkerUIProvider workerProvider)
	{
		return workerProvider is RobotWorkerUIProvider ? "Battery" : "Fatigue";
	}

	private static RectTransform CreateVerticalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		RectTransform rect = root.GetComponent<RectTransform>();
		rect.localScale = Vector3.one;
		return rect;
	}

	private static RectTransform CreateHorizontalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.MiddleLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		RectTransform rect = root.GetComponent<RectTransform>();
		rect.localScale = Vector3.one;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
		return rect;
	}

	private static TextMeshProUGUI AddLabeledValue(Transform parent, string label)
	{
		RectTransform row = CreateHorizontalContainer($"{label}Row", parent, 8f);

		TextMeshProUGUI labelText = CreateBodyText($"{label}Label", row);
		labelText.text = $"{label}:";
		labelText.fontStyle = FontStyles.Bold;
		LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
		labelLayout.flexibleWidth = 0f;
		labelLayout.preferredWidth = 180f;

		TextMeshProUGUI valueText = CreateBodyText($"{label}Value", row);
		valueText.text = string.Empty;
		LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
		valueLayout.flexibleWidth = 1f;
		valueLayout.minWidth = 0f;
		return valueText;
	}

	private static Button CreateButton(string name, Transform parent, out TextMeshProUGUI buttonText, float preferredHeight)
	{
		GameObject buttonRoot = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonRoot.transform.SetParent(parent, false);

		LayoutElement layout = buttonRoot.GetComponent<LayoutElement>();
		layout.preferredHeight = preferredHeight;
		layout.minHeight = preferredHeight;
		layout.preferredWidth = 160f;
		layout.flexibleWidth = 0f;

		Image image = buttonRoot.GetComponent<Image>();
		image.color = new Color(0.18f, 0.18f, 0.18f, 0.85f);

		GameObject textRoot = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textRoot.transform.SetParent(buttonRoot.transform, false);
		buttonText = textRoot.GetComponent<TextMeshProUGUI>();
		buttonText.alignment = TextAlignmentOptions.Center;
		buttonText.fontSize = 20f;
		buttonText.color = Color.white;

		RectTransform textRect = buttonText.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		return buttonRoot.GetComponent<Button>();
	}

	private static TextMeshProUGUI CreateBodyText(string name, Transform parent)
	{
		GameObject textRoot = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		textRoot.transform.SetParent(parent, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.fontSize = 22f;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.Left;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Overflow;
		text.text = string.Empty;
		LayoutElement layout = textRoot.AddComponent<LayoutElement>();
		layout.flexibleWidth = 1f;
		layout.minWidth = 0f;
		return text;
	}

	private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
		rect.pivot = new Vector2(0.5f, 0.5f);
	}
}
