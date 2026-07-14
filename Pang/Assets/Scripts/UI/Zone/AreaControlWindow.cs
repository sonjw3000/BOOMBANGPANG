using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class AreaControlWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window;
	[FormerlySerializedAs("overlayController")]
	[SerializeField] private AreaOverlayController overlayController;
	[SerializeField] private string windowTitle = "Area Control";
	[FormerlySerializedAs("defaultAreaType")]
	[SerializeField] private AreaType defaultAreaType = AreaType.WorkerSpawn;
	[FormerlySerializedAs("contentPrefab")]
	[SerializeField] private AreaControlWindowContentView contentPrefab;

	private bool initialized;
	private TextMeshProUGUI statusText;
	private TextButtonView createButton;
	private AreaType selectedAreaType;
	private int selectedFloor;

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
			Interaction.OnAreaPlacementChanged -= HandleAreaPlacementChanged;
	}

	public void ToggleWindow()
	{
		EnsureInitialized();
		if (window == null)
			return;

		if (gameObject.activeSelf == false || window.IsOpen == false)
			OpenForAreaType(selectedAreaType, selectedFloor);
		else
			window.Close();
	}

	public void OpenForAreaType(AreaType areaType, int floor = 0)
	{
		EnsureInitialized();
		EnsureHostActive();
		if (window == null)
			return;

		selectedAreaType = areaType;
		selectedFloor = floor;
		window.SetTitle(BuildTitle(areaType));
		overlayController?.SetAreaModeActive(true, areaType, floor);
		window.Open();
		UpdateStatus();
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
		overlayController ??= FindFirstObjectByType<AreaOverlayController>(FindObjectsInactive.Include);
		selectedAreaType = defaultAreaType;
		if (window == null)
			return;

		window.SetTitle(windowTitle);
		BuildContent();
		window.Opened += HandleWindowOpened;
		window.Closed += HandleWindowClosed;
		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
			Interaction.OnAreaPlacementChanged += HandleAreaPlacementChanged;

		window.Close();
		initialized = true;
	}

	private void BuildContent()
	{
		if (window == null || contentPrefab == null)
		{
			Debug.LogError("[AreaControlWindow] Content prefab is missing.", this);
			return;
		}

		RectTransform contentRoot = window.ContentRoot;
		contentRoot.DetachChildren();
		AreaControlWindowContentView contentView = Instantiate(contentPrefab, contentRoot);
		contentView.name = "AreaControlContent";
		statusText = contentView.StatusText;
		createButton = contentView.CreateButton;
		if (contentView.ToggleRoot != null)
			contentView.ToggleRoot.gameObject.SetActive(false);

		createButton?.Configure("Create Area", HandleCreateButtonClicked);
	}

	private void HandleWindowOpened()
	{
		overlayController?.SetAreaModeActive(true, selectedAreaType, selectedFloor);
		UpdateStatus();
	}

	private void HandleWindowClosed()
	{
		overlayController?.SetAreaModeActive(false, selectedAreaType, selectedFloor);
		UpdateStatus();
	}

	private void HandleCreateButtonClicked()
	{
		overlayController?.BeginCreate(selectedAreaType, selectedFloor);
		UpdateStatus();
	}

	private void HandleAreaPlacementChanged(AreaType areaType)
	{
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		if (statusText == null || createButton == null)
			return;

		bool isCreating = GameContext.HasInstance
			&& GameContext.Instance.InteractionCtx != null
			&& Interaction.Mode == InteractionContext.InteractionMode.AreaEdit;
		if (createButton.Button != null)
			createButton.Button.interactable = isCreating == false;
		if (createButton.LabelText != null)
			createButton.LabelText.text = isCreating ? "Creating..." : "Create Area";

		string areaLabel = selectedAreaType == AreaType.WorkerSpawn ? "worker spawn" : "rocket landing";
		statusText.text = isCreating
			? $"Left click the start and end outdoor cells for the {areaLabel} area. Right click to cancel."
			: $"Create, inspect, or delete {areaLabel} areas.";
	}

	private void EnsureHostActive()
	{
		if (gameObject.activeSelf == false)
			gameObject.SetActive(true);
	}

	private string BuildTitle(AreaType areaType)
	{
		return areaType == AreaType.WorkerSpawn ? "Worker Spawn Areas" : "Rocket Landing Areas";
	}
}
