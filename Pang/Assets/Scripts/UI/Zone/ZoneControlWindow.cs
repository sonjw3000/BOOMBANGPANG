using System;
using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ZoneControlWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window;
	[SerializeField] private ZoneOverlayController overlayController;
	[SerializeField] private string windowTitle = "Zone Control";
	[SerializeField] private ZoneType defaultZoneType = ZoneType.Storage;

	private readonly Dictionary<ZoneType, Toggle> toggles = new();
	private bool initialized;

	private RectTransform contentRoot;
	private TMP_Text statusText;
	private Button createButton;
	private TMP_Text createButtonText;
	private ZoneType selectedZoneType;
	private Building contextBuilding;
	private bool ownsBuildingMode;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;

	private void Awake()
	{
		if (gameObject.activeSelf)
		{
			gameObject.SetActive(false);
			return;
		}

		EnsureInitialized();
	}

	private void OnDestroy()
	{
		if (window != null)
		{
			window.Opened -= HandleWindowOpened;
			window.Closed -= HandleWindowClosed;
		}

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
			Interaction.OnZonePlacementChanged -= HandleZonePlacementChanged;
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
		if (window == null)
			return;

		window.Open();
	}

	public void OpenForBuilding(Building building)
	{
		EnsureInitialized();
		EnsureHostActive();
		if (window == null)
			return;

		contextBuilding = building;
		if (overlayController != null)
		{
			if (building != null)
			{
				if (overlayController.BuildingModeActive == false)
				{
					overlayController.SetBuildingModeActive(true);
					ownsBuildingMode = true;
				}
				else
				{
					ownsBuildingMode = false;
				}

				overlayController.SetOverlayVisible(true, building);
			}
			else
			{
				ownsBuildingMode = false;
				overlayController.SetOverlayVisible(true);
			}
		}

		window.Open();
	}

	public void Close()
	{
		EnsureInitialized();
		if (window == null)
			return;

		window.Close();
	}

	private void HandleWindowOpened()
	{
		if (contextBuilding == null && overlayController != null)
			contextBuilding = overlayController.CurrentBuilding;

		overlayController?.SetOverlayVisible(true, contextBuilding);
		UpdateStatus();
	}

	private void HandleWindowClosed()
	{
		overlayController?.SetOverlayVisible(false);
		if (ownsBuildingMode && overlayController != null)
		{
			overlayController.SetBuildingModeActive(false);
			ownsBuildingMode = false;
		}

		contextBuilding = null;
		UpdateStatus();
	}

	private void HandleZonePlacementChanged(ZoneType zoneType)
	{
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		if (statusText == null || createButton == null)
			return;

		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
		{
			createButton.interactable = false;
			if (createButtonText != null)
				createButtonText.text = "Create Zone";
			statusText.text = "Interaction context is unavailable.";
			return;
		}

		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit;
		createButton.interactable = isCreating == false && contextBuilding != null;
		createButtonText.text = isCreating ? "Creating..." : "Create Zone";

		if (window.IsOpen == false)
		{
			statusText.text = "Open the window to inspect zones.";
			return;
		}

		if (contextBuilding == null)
		{
			statusText.text = "Open this window from a building to create building-owned zones.";
			return;
		}

		string buildingLabel = contextBuilding != null ? contextBuilding.DisplayName : "current building";
		statusText.text = isCreating
			? $"Left click start/end cells inside {buildingLabel}. Right click to cancel."
			: $"Select a zone type and create a new zone in {buildingLabel}.";
	}

	private void BuildContent()
	{
		if (window == null)
			return;

		contentRoot = window.ContentRoot;
		contentRoot.DetachChildren();

		GameObject container = new("ZoneControlContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		container.transform.SetParent(contentRoot, false);

		var layout = container.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 10f;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;
		layout.childControlHeight = true;
		layout.childControlWidth = true;

		var fitter = container.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		statusText = CreateText("StatusText", container.transform, "Select a zone or create a new zone.");
		statusText.textWrappingMode = TextWrappingModes.Normal;
		statusText.fontSize = 20f;

		createButton = CreateButton("CreateButton", container.transform, "Create Zone", HandleCreateButtonClicked, out createButtonText);

		CreateText("ToggleHeader", container.transform, "Zone Type").fontSize = 22f;

		GameObject toggleRoot = new("ZoneTypeToggleRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ToggleGroup));
		toggleRoot.transform.SetParent(container.transform, false);

		var toggleLayout = toggleRoot.GetComponent<VerticalLayoutGroup>();
		toggleLayout.spacing = 6f;
		toggleLayout.childForceExpandHeight = false;
		toggleLayout.childForceExpandWidth = true;
		toggleLayout.childControlHeight = true;
		toggleLayout.childControlWidth = true;

		var toggleGroup = toggleRoot.GetComponent<ToggleGroup>();

		foreach (ZoneType zoneType in Enum.GetValues(typeof(ZoneType)))
		{
			Toggle toggle = CreateToggle(zoneType, toggleRoot.transform, toggleGroup);
			toggles[zoneType] = toggle;
			toggle.isOn = zoneType == selectedZoneType;
		}
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		window ??= GetComponent<UIWindow>();
		window ??= GetComponentInChildren<UIWindow>(true);
		overlayController ??= FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);
		selectedZoneType = defaultZoneType;

		if (window == null)
			return;

		window.SetTitle(windowTitle);
		BuildContent();
		window.Opened -= HandleWindowOpened;
		window.Closed -= HandleWindowClosed;
		window.Opened += HandleWindowOpened;
		window.Closed += HandleWindowClosed;

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
		{
			Interaction.OnZonePlacementChanged -= HandleZonePlacementChanged;
			Interaction.OnZonePlacementChanged += HandleZonePlacementChanged;
		}

		window.Close();
		UpdateStatus();
		initialized = true;
	}

	private void EnsureHostActive()
	{
		if (gameObject.activeSelf == false)
			gameObject.SetActive(true);
	}

	private void HandleCreateButtonClicked()
	{
		overlayController?.BeginCreate(selectedZoneType);
		UpdateStatus();
	}

	private Toggle CreateToggle(ZoneType zoneType, Transform parent, ToggleGroup group)
	{
		GameObject root = new(zoneType.ToString(), typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
		root.transform.SetParent(parent, false);

		var layout = root.GetComponent<LayoutElement>();
		layout.preferredHeight = 34f;

		var background = root.GetComponent<Image>();
		background.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

		var toggle = root.GetComponent<Toggle>();
		toggle.group = group;
		toggle.targetGraphic = background;

		TMP_Text label = CreateText("Label", root.transform, zoneType.ToString());
		label.alignment = TextAlignmentOptions.MidlineLeft;
		label.margin = new Vector4(12f, 0f, 0f, 0f);

		toggle.onValueChanged.AddListener(isOn =>
		{
			background.color = isOn ? new Color(0.26f, 0.45f, 0.72f, 1f) : new Color(0.22f, 0.22f, 0.22f, 0.95f);
			if (isOn)
				selectedZoneType = zoneType;
		});

		return toggle;
	}

	private static Button CreateButton(string objectName, Transform parent, string label, UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
	{
		GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(parent, false);

		var layout = buttonObject.GetComponent<LayoutElement>();
		layout.preferredHeight = 38f;

		var image = buttonObject.GetComponent<Image>();
		image.color = new Color(0.2f, 0.5f, 0.82f, 1f);

		var button = buttonObject.GetComponent<Button>();
		button.onClick.AddListener(onClick);

		labelText = CreateText("Label", buttonObject.transform, label);
		labelText.alignment = TextAlignmentOptions.Center;

		return button;
	}

	private static TMP_Text CreateText(string objectName, Transform parent, string value)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(parent, false);

		var text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = value;
		text.fontSize = 18f;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.MidlineLeft;

		var rect = text.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		return text;
	}
}
