using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingControlWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window;
	[SerializeField] private BuildingPlacementOverlayController overlayController;
	[SerializeField] private ZoneOverlayController zoneOverlayController;
	[SerializeField] private string windowTitle = "Building Control";

	private bool initialized;
	private TMP_Text statusText;
	private Button createButton;
	private TMP_Text createButtonText;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;

	private void Awake()
	{
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
			Interaction.OnBuildingPlacementChanged -= HandleBuildingPlacementChanged;
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
		overlayController ??= GetComponent<BuildingPlacementOverlayController>();
		if (overlayController == null)
			overlayController = gameObject.AddComponent<BuildingPlacementOverlayController>();
		zoneOverlayController ??= FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);

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
			Interaction.OnBuildingPlacementChanged -= HandleBuildingPlacementChanged;
			Interaction.OnBuildingPlacementChanged += HandleBuildingPlacementChanged;
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

	private void BuildContent()
	{
		RectTransform contentRoot = window.ContentRoot;
		if (contentRoot == null)
			return;

		contentRoot.DetachChildren();

		GameObject container = new("BuildingControlContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

		statusText = CreateText("StatusText", container.transform, "Create a rectangular building footprint.");
		statusText.textWrappingMode = TextWrappingModes.Normal;
		statusText.fontSize = 20f;

		createButton = CreateButton("CreateButton", container.transform, "Create Building", HandleCreateButtonClicked, out createButtonText);
	}

	private void HandleWindowOpened()
	{
		Interaction.EnterBuildingSelectMode();
		overlayController?.SetOverlayVisible(false);
		zoneOverlayController?.SetBuildingModeActive(true);
		UpdateStatus();
	}

	private void HandleWindowClosed()
	{
		overlayController?.SetOverlayVisible(false);
		zoneOverlayController?.SetBuildingModeActive(false);
		Interaction.ExitBuildingMode();
		UpdateStatus();
	}

	private void HandleBuildingPlacementChanged(int floor)
	{
		UpdateStatus();
	}

	private void HandleCreateButtonClicked()
	{
		overlayController?.BeginCreate();
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
				createButtonText.text = "Create Building";
			statusText.text = "Interaction context is unavailable.";
			return;
		}

		bool isCreating = Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement;
		createButton.interactable = isCreating == false;
		if (createButtonText != null)
			createButtonText.text = isCreating ? "Creating..." : "Create Building";

		if (window.IsOpen == false)
		{
			statusText.text = "Open the window to create building walls.";
			return;
		}

		statusText.text = isCreating
			? "Left click start/end cells. Right click to cancel."
			: "Drag a rectangle to create walls on the inside border.";
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
}
