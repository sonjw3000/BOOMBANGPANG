using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CargoPortDetailBuilder : DetailContent<CargoPort>
{
	[SerializeField] private Button forceLoadButton;
	private RectTransform runtimeInfoRoot;
	private TextMeshProUGUI nameText;
	private TextMeshProUGUI typeText;
	private TextMeshProUGUI fillText;
	private TextMeshProUGUI inputReadyText;

	static private OutboundWorkflowManager OBManager => GameContext.Instance.OBWorkflowMgr;


	protected override void BuildActionButtons(RectTransform actionRoot)
	{
		base.BuildActionButtons(actionRoot);

		var prov = (CargoPortUIProvider)provider;
		forceLoadButton?.gameObject.SetActive(false);

		if (prov.Target != null && prov.Target.IsInbound == false)
		{
			RegisterActionButton(CreateRuntimeActionButton(actionRoot, "Force Load", () =>
			{
				OBManager.BuildLoadingTask(prov.Target);
			}));
		}
	}

	protected override void LinkData()
	{
		forceLoadButton?.gameObject.SetActive(false);
		deleteButton?.gameObject.SetActive(false);
		BuildRuntimeInfo();
		RefreshRuntimeInfo();
	}

	protected override void UpdateData()
	{
		RefreshRuntimeInfo();
	}

	private void BuildRuntimeInfo()
	{
		if (InfoTabRoot == null || runtimeInfoRoot != null)
			return;

		foreach (Transform child in InfoTabRoot)
		{
			child.gameObject.SetActive(false);
		}

		runtimeInfoRoot = CreateInfoContainer("CargoPortInfoRoot", InfoTabRoot, 6f);
		nameText = CreateInfoText("NameText", runtimeInfoRoot);
		typeText = CreateInfoText("TypeText", runtimeInfoRoot);
		fillText = CreateInfoText("FillText", runtimeInfoRoot);
		inputReadyText = CreateInfoText("InputReadyText", runtimeInfoRoot);
	}

	private void RefreshRuntimeInfo()
	{
		if (provider is not CargoPortUIProvider cargoPortProvider || cargoPortProvider.Target == null)
			return;

		nameText.text = $"Name: {cargoPortProvider.Name}";
		typeText.text = $"Type: {cargoPortProvider.Subtitle}";
		fillText.text = $"Filled: {cargoPortProvider.FilledPercent:0.0}%";
		inputReadyText.text = $"Input Ready: {(cargoPortProvider.Target.InputReady ? "Yes" : "No")}";
	}

	private static RectTransform CreateInfoContainer(string name, Transform parent, float spacing)
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

		return root.GetComponent<RectTransform>();
	}

	private static TextMeshProUGUI CreateInfoText(string name, Transform parent)
	{
		GameObject textRoot = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textRoot.transform.SetParent(parent, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.fontSize = 22f;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.overflowMode = TextOverflowModes.Truncate;

		LayoutElement layout = textRoot.GetComponent<LayoutElement>();
		layout.flexibleWidth = 1f;
		layout.minWidth = 0f;

		return text;
	}
}
