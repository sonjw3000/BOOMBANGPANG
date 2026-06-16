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
	[SerializeField] private ZoneControlWindowContentView contentPrefab;
	[SerializeField] private ToggleRowView toggleRowPrefab;

	private readonly Dictionary<ZoneType, ToggleRowView> toggles = new();
	private bool initialized;
	private TextMeshProUGUI statusText;
	private TextButtonView createButton;
	private RectTransform toggleRoot;
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
		window?.Open();
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
		window?.Close();
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
			if (createButton.Button != null)
				createButton.Button.interactable = false;
			if (createButton.LabelText != null)
				createButton.LabelText.text = "Create Zone";
			statusText.text = "Interaction context is unavailable.";
			return;
		}

		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit;
		if (createButton.Button != null)
			createButton.Button.interactable = isCreating == false && contextBuilding != null;
		if (createButton.LabelText != null)
			createButton.LabelText.text = isCreating ? "Creating..." : "Create Zone";

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

		string buildingLabel = contextBuilding.DisplayName;
		statusText.text = isCreating
			? $"Left click start/end cells inside {buildingLabel}. Right click to cancel."
			: $"Select a zone type and create a new zone in {buildingLabel}.";
	}

	private void BuildContent()
	{
		if (window == null)
			return;

		RectTransform contentRoot = window.ContentRoot;
		contentRoot.DetachChildren();
		toggles.Clear();

		if (contentPrefab == null)
		{
			Debug.LogError("[ZoneControlWindow] Content prefab is missing.", this);
			return;
		}

		ZoneControlWindowContentView contentView = Instantiate(contentPrefab, contentRoot);
		contentView.name = "ZoneControlContent";
		statusText = contentView.StatusText;
		createButton = contentView.CreateButton;
		toggleRoot = contentView.ToggleRoot;
		createButton?.Configure("Create Zone", HandleCreateButtonClicked);

		ToggleGroup toggleGroup = toggleRoot != null ? toggleRoot.GetComponent<ToggleGroup>() : null;
		foreach (ZoneType zoneType in Enum.GetValues(typeof(ZoneType)))
		{
			ToggleRowView toggleRow = CreateToggle(zoneType, toggleRoot, toggleGroup);
			toggles[zoneType] = toggleRow;
			if (toggleRow != null && toggleRow.Toggle != null)
				toggleRow.Toggle.isOn = zoneType == selectedZoneType;
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

	private ToggleRowView CreateToggle(ZoneType zoneType, Transform parent, ToggleGroup group)
	{
		if (toggleRowPrefab == null)
		{
			Debug.LogError("[ZoneControlWindow] Toggle row prefab is missing.", this);
			return null;
		}

		ToggleRowView toggleRow = Instantiate(toggleRowPrefab, parent);
		toggleRow.name = zoneType + "Toggle";
		if (toggleRow.LabelText != null)
			toggleRow.LabelText.text = zoneType.ToString();

		if (toggleRow.Toggle == null)
			return toggleRow;

		toggleRow.Toggle.group = group;
		toggleRow.Toggle.targetGraphic = toggleRow.Background;
		if (toggleRow.LabelText != null)
			toggleRow.LabelText.margin = new Vector4(12f, 0f, 0f, 0f);

		toggleRow.Toggle.onValueChanged.AddListener(isOn =>
		{
			if (toggleRow.Background != null)
				toggleRow.Background.color = isOn ? new Color(0.26f, 0.45f, 0.72f, 1f) : new Color(0.22f, 0.22f, 0.22f, 0.95f);
			if (isOn)
				selectedZoneType = zoneType;
		});

		return toggleRow;
	}
}
