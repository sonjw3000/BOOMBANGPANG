using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
	public class PolicyControlWindow : MonoBehaviour
	{
		private const float MinSpeedMultiplier = 0.5f;
		private const float MaxSpeedMultiplier = 2.0f;

		private enum TabType
		{
			WorkApproach,
			WorkSpeed,
		}

		private sealed class WorkerSpeedControls
		{
			public Slider MoveSlider;
			public TMP_Text MoveValueText;
			public Slider WorkSlider;
			public TMP_Text WorkValueText;
		}

		private static readonly PlacingPolicyType[] PlacingPolicyOptions =
		{
			PlacingPolicyType.BelowAverageFilledNearest,
			PlacingPolicyType.Nearest,
		};

		private static readonly CollectingPolicyType[] CollectingPolicyOptions =
		{
			CollectingPolicyType.Nearest,
			CollectingPolicyType.LargestQuantityNearest,
		};

		private static Font defaultFont;

		[SerializeField] private UIWindow window;
		[SerializeField] private string windowTitle = "Policy Control";

		private readonly Dictionary<WorkerType, WorkerSpeedControls> speedControlsByType = new();

		private Dropdown storingCollectingPolicyDropdown;
		private Dropdown placingPolicyDropdown;
		private Dropdown pickingCollectingPolicyDropdown;
		private GameObject workApproachTabRoot;
		private GameObject workSpeedTabRoot;
		private TabType currentTab;
		private bool initialized;

		private WorkPolicyService WorkPolicyService => GameContext.HasInstance ? GameContext.Instance.WMSys?.WorkPolicyService : null;
		private InboundWorkflowService InboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;
		private OutboundWorkflowService OutboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;

		private void Awake()
		{
			EnsureInitialized();
		}

		private void OnEnable()
		{
			EnsureInitialized();
			RefreshFromState();
		}

		public void ToggleWindow()
		{
			EnsureInitialized();

			if (window == null)
				return;

			bool shouldOpen = gameObject.activeSelf == false || window.IsOpen == false;
			if (shouldOpen)
			{
				EnsureHostActive();
				RefreshFromState();
				window.Open();
			}
			else
			{
				window.Close();
			}
		}

		public void Open()
		{
			EnsureInitialized();
			EnsureHostActive();
			RefreshFromState();
			window?.Open();
		}

		public void Close()
		{
			EnsureInitialized();
			window?.Close();
		}

		private void EnsureInitialized()
		{
			if (initialized)
				return;

			window ??= GetComponent<UIWindow>();
			window ??= GetComponentInChildren<UIWindow>(true);
			if (window == null)
				return;

			window.SetTitle(windowTitle);
			BuildContent();
			SetupTabs();
			SetTab((int)TabType.WorkApproach);
			window.Close();
			initialized = true;
		}

		private void SetupTabs()
		{
			window.ClearTabs();
			window.AddTab("WorkApproach", SetTab);
			window.AddTab("WorkSpeed", SetTab);
			window.UpdateTabVisuals((int)currentTab);
		}

		private void SetTab(int tabIndex)
		{
			currentTab = (TabType)tabIndex;

			if (workApproachTabRoot != null)
				workApproachTabRoot.SetActive(currentTab == TabType.WorkApproach);
			if (workSpeedTabRoot != null)
				workSpeedTabRoot.SetActive(currentTab == TabType.WorkSpeed);

			window?.UpdateTabVisuals(tabIndex);
		}

		private void BuildContent()
		{
			RectTransform contentRoot = window.ContentRoot;
			if (contentRoot == null)
				return;

			ClearChildren(contentRoot);
			speedControlsByType.Clear();

			GameObject container = CreateVerticalContainer("PolicyControlContent", contentRoot, 12f);
			workApproachTabRoot = CreateVerticalContainer("WorkApproachTab", container.transform, 10f);
			workSpeedTabRoot = CreateVerticalContainer("WorkSpeedTab", container.transform, 10f);

			BuildWorkApproachTab(workApproachTabRoot.transform);
			BuildWorkSpeedTab(workSpeedTabRoot.transform);
		}

		private void BuildWorkApproachTab(Transform parent)
		{
			CreateSectionHeader(parent, "Storing");
			storingCollectingPolicyDropdown = CreateDropdownRow(parent, "Collecting Policy", HandleStoringCollectingPolicyChanged);
			storingCollectingPolicyDropdown.ClearOptions();
			storingCollectingPolicyDropdown.AddOptions(new List<string>
			{
				GetCollectingPolicyLabel(CollectingPolicyType.Nearest),
				GetCollectingPolicyLabel(CollectingPolicyType.LargestQuantityNearest),
			});

			placingPolicyDropdown = CreateDropdownRow(parent, "Placing Policy", HandlePlacingPolicyChanged);
			placingPolicyDropdown.ClearOptions();
			placingPolicyDropdown.AddOptions(new List<string>
			{
				GetPlacingPolicyLabel(PlacingPolicyType.BelowAverageFilledNearest),
				GetPlacingPolicyLabel(PlacingPolicyType.Nearest),
			});

			CreateSectionHeader(parent, "Picking");
			pickingCollectingPolicyDropdown = CreateDropdownRow(parent, "Collecting Policy", HandlePickingCollectingPolicyChanged);
			pickingCollectingPolicyDropdown.ClearOptions();
			pickingCollectingPolicyDropdown.AddOptions(new List<string>
			{
				GetCollectingPolicyLabel(CollectingPolicyType.Nearest),
				GetCollectingPolicyLabel(CollectingPolicyType.LargestQuantityNearest),
			});
		}

		private void BuildWorkSpeedTab(Transform parent)
		{
			CreateSectionHeader(parent, "Worker Speed Multipliers");

			foreach (WorkerType workerType in Enum.GetValues(typeof(WorkerType)))
			{
				GameObject card = CreateCard($"{workerType}Card", parent);
				CreateText("WorkerTypeLabel", card.transform, workerType.ToString()).fontSize = 22f;

				WorkerSpeedControls controls = new();
				controls.MoveSlider = CreateSliderRow(card.transform, "Move Speed", value =>
				{
					WorkPolicyService?.SetMoveSpeedMultiplier(workerType, value);
					UpdateValueLabel(speedControlsByType[workerType], true, value);
				}, out TMP_Text moveValueText);
				controls.MoveValueText = moveValueText;
				controls.WorkSlider = CreateSliderRow(card.transform, "Work Speed", value =>
				{
					WorkPolicyService?.SetWorkSpeedMultiplier(workerType, value);
					UpdateValueLabel(speedControlsByType[workerType], false, value);
				}, out TMP_Text workValueText);
				controls.WorkValueText = workValueText;

				speedControlsByType[workerType] = controls;
			}
		}

		private void RefreshFromState()
		{
			if (initialized == false)
				return;

			if (InboundWorkflowService != null && placingPolicyDropdown != null)
			{
				if (storingCollectingPolicyDropdown != null)
				{
					int collectingDropdownIndex = Array.IndexOf(CollectingPolicyOptions, InboundWorkflowService.StoringCollectingPolicyType);
					storingCollectingPolicyDropdown.SetValueWithoutNotify(Mathf.Max(0, collectingDropdownIndex));
				}

				int dropdownIndex = Array.IndexOf(PlacingPolicyOptions, InboundWorkflowService.StoringPlacingPolicyType);
				placingPolicyDropdown.SetValueWithoutNotify(Mathf.Max(0, dropdownIndex));
			}

			if (OutboundWorkflowService != null && pickingCollectingPolicyDropdown != null)
			{
				int dropdownIndex = Array.IndexOf(CollectingPolicyOptions, OutboundWorkflowService.PickingCollectingPolicyType);
				pickingCollectingPolicyDropdown.SetValueWithoutNotify(Mathf.Max(0, dropdownIndex));
			}

			if (WorkPolicyService == null)
				return;

			foreach (KeyValuePair<WorkerType, WorkerSpeedControls> entry in speedControlsByType)
			{
				float moveValue = WorkPolicyService.GetMoveSpeedMultiplier(entry.Key);
				float workValue = WorkPolicyService.GetWorkSpeedMultiplier(entry.Key);
				entry.Value.MoveSlider.SetValueWithoutNotify(moveValue);
				entry.Value.WorkSlider.SetValueWithoutNotify(workValue);
				UpdateValueLabel(entry.Value, true, moveValue);
				UpdateValueLabel(entry.Value, false, workValue);
			}
		}

		private void HandlePlacingPolicyChanged(int optionIndex)
		{
			if (InboundWorkflowService == null)
				return;
			if (optionIndex < 0 || optionIndex >= PlacingPolicyOptions.Length)
				return;

			InboundWorkflowService.SetStoringPlacingPolicy(PlacingPolicyOptions[optionIndex]);
		}

		private void HandleStoringCollectingPolicyChanged(int optionIndex)
		{
			if (InboundWorkflowService == null)
				return;
			if (optionIndex < 0 || optionIndex >= CollectingPolicyOptions.Length)
				return;

			InboundWorkflowService.SetStoringCollectingPolicy(CollectingPolicyOptions[optionIndex]);
		}

		private void HandlePickingCollectingPolicyChanged(int optionIndex)
		{
			if (OutboundWorkflowService == null)
				return;
			if (optionIndex < 0 || optionIndex >= CollectingPolicyOptions.Length)
				return;

			OutboundWorkflowService.SetPickingCollectingPolicy(CollectingPolicyOptions[optionIndex]);
		}

		private void EnsureHostActive()
		{
			if (gameObject.activeSelf == false)
				gameObject.SetActive(true);
		}

		private static GameObject CreateVerticalContainer(string objectName, Transform parent, float spacing)
		{
			GameObject container = new(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			container.transform.SetParent(parent, false);

			VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = new RectOffset(8, 8, 8, 8);
			layout.childForceExpandHeight = false;
			layout.childForceExpandWidth = true;
			layout.childControlHeight = true;
			layout.childControlWidth = true;

			ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			return container;
		}

		private static GameObject CreateCard(string objectName, Transform parent)
		{
			GameObject card = CreateVerticalContainer(objectName, parent, 8f);
			LayoutElement layout = card.AddComponent<LayoutElement>();
			layout.minHeight = 120f;
			layout.preferredHeight = 120f;

			Image image = card.AddComponent<Image>();
			image.color = new Color(0.12f, 0.12f, 0.12f, 0.82f);
			return card;
		}

		private static TMP_Text CreateSectionHeader(Transform parent, string title)
		{
			TMP_Text text = CreateText($"{title}Header", parent, title);
			text.fontSize = 24f;
			return text;
		}

		private static TMP_Text CreateText(string objectName, Transform parent, string value)
		{
			GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
			textObject.transform.SetParent(parent, false);

			LayoutElement layout = textObject.GetComponent<LayoutElement>();
			layout.preferredHeight = 28f;

			TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
			text.text = value;
			text.fontSize = 20f;
			text.color = Color.white;
			text.alignment = TextAlignmentOptions.MidlineLeft;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Ellipsis;

			RectTransform rect = text.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			return text;
		}

		private static Dropdown CreateDropdownRow(Transform parent, string label, UnityEngine.Events.UnityAction<int> onChanged)
		{
			GameObject row = new($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
			row.transform.SetParent(parent, false);

			HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
			rowLayout.spacing = 12f;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;
			rowLayout.childControlHeight = true;
			rowLayout.childControlWidth = true;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = false;

			row.GetComponent<LayoutElement>().preferredHeight = 42f;

			TMP_Text labelText = CreateText("Label", row.transform, label);
			labelText.fontSize = 19f;
			labelText.GetComponent<LayoutElement>().preferredWidth = 180f;

			GameObject dropdownObject = new("Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
			dropdownObject.transform.SetParent(row.transform, false);

			Image dropdownImage = dropdownObject.GetComponent<Image>();
			dropdownImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

			LayoutElement dropdownLayout = dropdownObject.GetComponent<LayoutElement>();
			dropdownLayout.preferredHeight = 36f;
			dropdownLayout.preferredWidth = 320f;

			Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();

			Text captionText = CreateLegacyText("Label", dropdownObject.transform, "Option");
			captionText.alignment = TextAnchor.MiddleLeft;
			captionText.rectTransform.offsetMin = new Vector2(10f, 0f);
			captionText.rectTransform.offsetMax = new Vector2(-30f, 0f);

			Text arrowText = CreateLegacyText("Arrow", dropdownObject.transform, "v");
			arrowText.alignment = TextAnchor.MiddleCenter;
			arrowText.rectTransform.anchorMin = new Vector2(1f, 0f);
			arrowText.rectTransform.anchorMax = new Vector2(1f, 1f);
			arrowText.rectTransform.pivot = new Vector2(1f, 0.5f);
			arrowText.rectTransform.sizeDelta = new Vector2(24f, 0f);
			arrowText.rectTransform.anchoredPosition = new Vector2(-6f, 0f);

			GameObject templateObject = new("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
			templateObject.transform.SetParent(dropdownObject.transform, false);
			templateObject.SetActive(false);

			RectTransform templateRect = templateObject.GetComponent<RectTransform>();
			templateRect.anchorMin = new Vector2(0f, 0f);
			templateRect.anchorMax = new Vector2(1f, 0f);
			templateRect.pivot = new Vector2(0.5f, 1f);
			templateRect.anchoredPosition = new Vector2(0f, 2f);
			templateRect.sizeDelta = new Vector2(0f, 150f);

			Image templateImage = templateObject.GetComponent<Image>();
			templateImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);

			ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
			scrollRect.horizontal = false;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;

			GameObject viewportObject = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
			viewportObject.transform.SetParent(templateObject.transform, false);

			RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = Vector2.zero;
			viewportRect.offsetMax = Vector2.zero;

			Image viewportImage = viewportObject.GetComponent<Image>();
			viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
			Mask viewportMask = viewportObject.GetComponent<Mask>();
			viewportMask.showMaskGraphic = false;

			GameObject contentObject = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			contentObject.transform.SetParent(viewportObject.transform, false);

			RectTransform contentRect = contentObject.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.offsetMin = Vector2.zero;
			contentRect.offsetMax = Vector2.zero;

			VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
			contentLayout.childForceExpandHeight = false;
			contentLayout.childForceExpandWidth = true;
			contentLayout.childControlHeight = true;
			contentLayout.childControlWidth = true;
			contentLayout.spacing = 2f;

			ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
			contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

			GameObject itemObject = new("Item", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
			itemObject.transform.SetParent(contentObject.transform, false);

			LayoutElement itemLayout = itemObject.GetComponent<LayoutElement>();
			itemLayout.preferredHeight = 28f;

			Image itemImage = itemObject.GetComponent<Image>();
			itemImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

			Toggle itemToggle = itemObject.GetComponent<Toggle>();
			itemToggle.targetGraphic = itemImage;
			itemToggle.isOn = true;

			Text itemLabel = CreateLegacyText("Item Label", itemObject.transform, "Option");
			itemLabel.alignment = TextAnchor.MiddleLeft;
			itemLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
			itemLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

			scrollRect.viewport = viewportRect;
			scrollRect.content = contentRect;

			dropdown.targetGraphic = dropdownImage;
			dropdown.captionText = captionText;
			dropdown.template = templateRect;
			dropdown.itemText = itemLabel;

			dropdown.onValueChanged.RemoveAllListeners();
			if (onChanged != null)
				dropdown.onValueChanged.AddListener(onChanged);

			return dropdown;
		}

		private static Slider CreateSliderRow(Transform parent, string label, UnityEngine.Events.UnityAction<float> onChanged, out TMP_Text valueText)
		{
			GameObject row = new($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
			row.transform.SetParent(parent, false);

			HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
			rowLayout.spacing = 12f;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;
			rowLayout.childControlHeight = true;
			rowLayout.childControlWidth = true;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = false;

			row.GetComponent<LayoutElement>().preferredHeight = 36f;

			TMP_Text labelText = CreateText("Label", row.transform, label);
			labelText.fontSize = 18f;
			labelText.GetComponent<LayoutElement>().preferredWidth = 140f;

			GameObject sliderObject = new("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
			sliderObject.transform.SetParent(row.transform, false);
			LayoutElement sliderLayout = sliderObject.GetComponent<LayoutElement>();
			sliderLayout.preferredWidth = 260f;
			sliderLayout.preferredHeight = 28f;

			Slider slider = sliderObject.GetComponent<Slider>();
			slider.minValue = MinSpeedMultiplier;
			slider.maxValue = MaxSpeedMultiplier;
			slider.wholeNumbers = false;
			slider.direction = Slider.Direction.LeftToRight;

			GameObject background = new("Background", typeof(RectTransform), typeof(Image));
			background.transform.SetParent(sliderObject.transform, false);
			RectTransform backgroundRect = background.GetComponent<RectTransform>();
			backgroundRect.anchorMin = new Vector2(0f, 0.25f);
			backgroundRect.anchorMax = new Vector2(1f, 0.75f);
			backgroundRect.offsetMin = Vector2.zero;
			backgroundRect.offsetMax = Vector2.zero;
			background.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);

			GameObject fillArea = new("Fill Area", typeof(RectTransform));
			fillArea.transform.SetParent(sliderObject.transform, false);
			RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
			fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
			fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
			fillAreaRect.offsetMin = new Vector2(8f, 0f);
			fillAreaRect.offsetMax = new Vector2(-8f, 0f);

			GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
			fill.transform.SetParent(fillArea.transform, false);
			RectTransform fillRect = fill.GetComponent<RectTransform>();
			fillRect.anchorMin = new Vector2(0f, 0f);
			fillRect.anchorMax = new Vector2(1f, 1f);
			fillRect.offsetMin = Vector2.zero;
			fillRect.offsetMax = Vector2.zero;
			fill.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.85f, 1f);

			GameObject handleArea = new("Handle Slide Area", typeof(RectTransform));
			handleArea.transform.SetParent(sliderObject.transform, false);
			RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
			handleAreaRect.anchorMin = Vector2.zero;
			handleAreaRect.anchorMax = Vector2.one;
			handleAreaRect.offsetMin = new Vector2(8f, 0f);
			handleAreaRect.offsetMax = new Vector2(-8f, 0f);

			GameObject handle = new("Handle", typeof(RectTransform), typeof(Image));
			handle.transform.SetParent(handleArea.transform, false);
			RectTransform handleRect = handle.GetComponent<RectTransform>();
			handleRect.sizeDelta = new Vector2(18f, 18f);
			handle.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);

			slider.fillRect = fillRect;
			slider.handleRect = handleRect;
			slider.targetGraphic = handle.GetComponent<Image>();

			valueText = CreateText("Value", row.transform, FormatMultiplier(1.0f));
			valueText.fontSize = 18f;
			valueText.alignment = TextAlignmentOptions.MidlineRight;
			valueText.GetComponent<LayoutElement>().preferredWidth = 56f;

			slider.onValueChanged.RemoveAllListeners();
			if (onChanged != null)
				slider.onValueChanged.AddListener(onChanged);

			return slider;
		}

		private static Text CreateLegacyText(string objectName, Transform parent, string value)
		{
			GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
			textObject.transform.SetParent(parent, false);

			Text text = textObject.GetComponent<Text>();
			text.font = GetDefaultFont();
			text.text = value;
			text.color = Color.white;
			text.alignment = TextAnchor.MiddleLeft;

			RectTransform rect = text.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			return text;
		}

		private static Font GetDefaultFont()
		{
			if (defaultFont == null)
			{
				defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				if (defaultFont == null)
					defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
			}

			return defaultFont;
		}

		private static void UpdateValueLabel(WorkerSpeedControls controls, bool isMoveSlider, float value)
		{
			if (controls == null)
				return;

			TMP_Text target = isMoveSlider ? controls.MoveValueText : controls.WorkValueText;
			if (target != null)
				target.text = FormatMultiplier(value);
		}

		private static string FormatMultiplier(float value) => value.ToString("0.00");

		private static string GetPlacingPolicyLabel(PlacingPolicyType type)
		{
			switch (type)
			{
				case PlacingPolicyType.Nearest:
					return "Nearest";

				case PlacingPolicyType.BelowAverageFilledNearest:
				default:
					return "Below Avg Filled + Nearest";
			}
		}

		private static string GetCollectingPolicyLabel(CollectingPolicyType type)
		{
			switch (type)
			{
				case CollectingPolicyType.LargestQuantityNearest:
					return "Largest Qty + Nearest";

				case CollectingPolicyType.Nearest:
				default:
					return "Nearest";
			}
		}

		private static void ClearChildren(Transform parent)
		{
			for (int i = parent.childCount - 1; i >= 0; i--)
			{
				Destroy(parent.GetChild(i).gameObject);
			}
		}
	}

}
