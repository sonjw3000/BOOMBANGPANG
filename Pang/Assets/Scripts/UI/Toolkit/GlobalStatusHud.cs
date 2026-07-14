using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class GlobalStatusHud : MonoBehaviour
	{
		private const string DocumentObjectName = "GlobalStatusHudDocument";
		private const string HistoryDocumentObjectName = "GlobalHistoryWindowDocument";
		private const string ContractDocumentObjectName = "ContractManagementWindowDocument";
		private const string WorkforceDocumentObjectName = "WorkforceManagementWindowDocument";
		private const int MaxVisibleEvents = 5;
		private const float EventFadeSeconds = 0.8f;
		private const float ReferenceWidth = 1920f;
		private const float ReferenceHeight = 1080f;
		private const float ReferenceUiScale = 1.0f;

		[SerializeField] private VisualTreeAsset visualTreeAsset;
		[SerializeField] private VisualTreeAsset hudEventEntryTemplate;
		[SerializeField] private VisualTreeAsset windowVisualTreeAsset;
		[SerializeField] private VisualTreeAsset historyContentTemplate;
		[SerializeField] private VisualTreeAsset historyRowTemplate;
		[SerializeField] private VisualTreeAsset contractContentTemplate;
		[SerializeField] private VisualTreeAsset activeContractRowTemplate;
		[SerializeField] private VisualTreeAsset contractMarketRowTemplate;
		[SerializeField] private VisualTreeAsset vendorContractRowTemplate;
		[SerializeField] private VisualTreeAsset workforceContentTemplate;
		[SerializeField] private VisualTreeAsset workforceRosterRowTemplate;
		[SerializeField] private VisualTreeAsset workforceCandidateRowTemplate;
		[SerializeField] private List<WorkforceMarketData_SO> workforceHumanMarkets = new();
		[SerializeField] private List<WorkforceMarketData_SO> workforceRobotMarkets = new();
		[SerializeField] private PanelSettings panelSettings;
		[SerializeField] private int sortingOrder = 100;

		private readonly List<ActiveHudEvent> activeEvents = new();
		private UIDocument uiDocument;
		private UnityEngine.UI.CanvasScaler legacyCanvasScaler;
		private VisualElement hudRoot;
		private VisualElement leftHud;
		private VisualElement timeCluster;
		private VisualElement economySummary;
		private VisualElement hudEventArea;
		private VisualElement hudEventList;
		private Label moneyValue;
		private Label reputationValue;
		private Label dateValue;
		private Label speedValue;
		private Button pauseButton;
		private Button normalSpeedButton;
		private Button doubleSpeedButton;
		private Button managementButton;
		private Button contractManagementButton;
		private Button workforceManagementButton;
		private VisualElement managementMenu;
		private GlobalHistoryWindow historyWindow;
		private ContractManagementWindow contractManagementWindow;
		private WorkforceManagementWindow workforceManagementWindow;
		private EconomyService economyService;
		private HudEventManager hudEventManager;
		private GameTime gameTime;
		private bool started;
		private bool? timeHudDockedRight;
		private int scaledScreenWidth = -1;
		private int scaledScreenHeight = -1;

		private sealed class ActiveHudEvent
		{
			public VisualElement Root;
			public float Elapsed;
			public float VisibleSeconds;
		}

		private void OnEnable()
		{
			ApplyPanelScale();
			EnsureDocument();
			EnsureHistoryWindow();
			EnsureContractManagementWindow();
			EnsureWorkforceManagementWindow();
			BindControls();

			if (started)
				BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
			ApplyPanelScale();

			for (int i = activeEvents.Count - 1; i >= 0; --i)
			{
				ActiveHudEvent activeEvent = activeEvents[i];
				activeEvent.Elapsed += Time.unscaledDeltaTime;
				float fade = Mathf.Clamp01((activeEvent.Elapsed - activeEvent.VisibleSeconds) / EventFadeSeconds);
				activeEvent.Root.style.opacity = 1f - fade;

				if (fade < 1f)
					continue;

				activeEvent.Root.RemoveFromHierarchy();
				activeEvents.RemoveAt(i);
			}
		}

		private void ApplyPanelScale()
		{
			legacyCanvasScaler ??= GetComponentInChildren<UnityEngine.UI.CanvasScaler>(true);
			if ((panelSettings == null && legacyCanvasScaler == null) ||
				(scaledScreenWidth == Screen.width && scaledScreenHeight == Screen.height))
			{
				return;
			}

			scaledScreenWidth = Screen.width;
			scaledScreenHeight = Screen.height;
			float widthScale = Screen.width / ReferenceWidth;
			float heightScale = Screen.height / ReferenceHeight;
			float uiScale = Mathf.Max(0.01f, ReferenceUiScale * Mathf.Min(widthScale, heightScale));
			if (panelSettings != null)
			{
				panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
				panelSettings.scale = uiScale;
			}

			if (legacyCanvasScaler != null)
			{
				legacyCanvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
				legacyCanvasScaler.scaleFactor = uiScale;
			}
		}

		private void OnDisable()
		{
			UnbindControls();
			UnbindServices();
		}

		private void EnsureDocument()
		{
			if (uiDocument != null)
				return;

			if (visualTreeAsset == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] VisualTreeAsset or PanelSettings is missing.", this);
				return;
			}

			GameObject documentObject = new(DocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			uiDocument = documentObject.AddComponent<UIDocument>();
			uiDocument.panelSettings = panelSettings;
			uiDocument.visualTreeAsset = visualTreeAsset;
			uiDocument.sortingOrder = sortingOrder;
			documentObject.SetActive(true);
		}

		private void EnsureHistoryWindow()
		{
			if (historyWindow != null)
				return;

			if (windowVisualTreeAsset == null || historyContentTemplate == null || historyRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] History window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(HistoryDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument historyDocument = documentObject.AddComponent<UIDocument>();
			historyDocument.panelSettings = panelSettings;
			historyDocument.visualTreeAsset = windowVisualTreeAsset;
			historyDocument.sortingOrder = sortingOrder + 10;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			historyWindow = documentObject.AddComponent<GlobalHistoryWindow>();
			historyWindow.Configure(window, historyContentTemplate, historyRowTemplate);
			documentObject.SetActive(true);
		}

		private void EnsureContractManagementWindow()
		{
			if (contractManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || contractContentTemplate == null || activeContractRowTemplate == null ||
				contractMarketRowTemplate == null || vendorContractRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Contract management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(ContractDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument contractDocument = documentObject.AddComponent<UIDocument>();
			contractDocument.panelSettings = panelSettings;
			contractDocument.visualTreeAsset = windowVisualTreeAsset;
			contractDocument.sortingOrder = sortingOrder + 20;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			contractManagementWindow = documentObject.AddComponent<ContractManagementWindow>();
			contractManagementWindow.Configure(window, contractContentTemplate, activeContractRowTemplate,
				contractMarketRowTemplate, vendorContractRowTemplate);
			documentObject.SetActive(true);
		}

		private void EnsureWorkforceManagementWindow()
		{
			if (workforceManagementWindow != null)
				return;

			if (windowVisualTreeAsset == null || workforceContentTemplate == null || workforceRosterRowTemplate == null ||
				workforceCandidateRowTemplate == null || panelSettings == null)
			{
				Debug.LogError("[GlobalStatusHud] Workforce management window assets are missing.", this);
				return;
			}

			GameObject documentObject = new(WorkforceDocumentObjectName);
			documentObject.SetActive(false);
			documentObject.transform.SetParent(transform, false);

			UIDocument workforceDocument = documentObject.AddComponent<UIDocument>();
			workforceDocument.panelSettings = panelSettings;
			workforceDocument.visualTreeAsset = windowVisualTreeAsset;
			workforceDocument.sortingOrder = sortingOrder + 30;

			UIWindow window = documentObject.AddComponent<UIWindow>();
			window.SetOpenOnEnable(false);
			workforceManagementWindow = documentObject.AddComponent<WorkforceManagementWindow>();
			workforceManagementWindow.Configure(window, workforceContentTemplate, workforceRosterRowTemplate,
				workforceCandidateRowTemplate, workforceHumanMarkets, workforceRobotMarkets);
			documentObject.SetActive(true);
		}

		private void BindControls()
		{
			if (uiDocument == null)
				return;

			VisualElement root = uiDocument.rootVisualElement;
			root.pickingMode = PickingMode.Ignore;
			hudRoot = root;
			leftHud = root.Q<VisualElement>(className: "left-hud");
			timeCluster = root.Q<VisualElement>(className: "time-cluster");
			economySummary = root.Q<VisualElement>("economy-summary");
			hudEventArea = root.Q<VisualElement>("hud-event-area");
			hudEventList = root.Q<VisualElement>("hud-event-list");
			moneyValue = root.Q<Label>("money-value");
			reputationValue = root.Q<Label>("reputation-value");
			dateValue = root.Q<Label>("date-value");
			speedValue = root.Q<Label>("speed-value");
			pauseButton = root.Q<Button>("pause-button");
			normalSpeedButton = root.Q<Button>("normal-speed-button");
			doubleSpeedButton = root.Q<Button>("double-speed-button");
			managementButton = root.Q<Button>("management-button");
			contractManagementButton = root.Q<Button>("contract-management-button");
			workforceManagementButton = root.Q<Button>("workforce-management-button");
			managementMenu = root.Q<VisualElement>("management-menu");

			if (leftHud == null || timeCluster == null || economySummary == null || hudEventArea == null ||
				hudEventList == null || moneyValue == null ||
				reputationValue == null || dateValue == null || speedValue == null || pauseButton == null ||
				normalSpeedButton == null || doubleSpeedButton == null || managementButton == null || contractManagementButton == null ||
				workforceManagementButton == null ||
				managementMenu == null)
			{
				Debug.LogError("[GlobalStatusHud] Required UXML elements are missing.", this);
				return;
			}

			economySummary.UnregisterCallback<ClickEvent>(OnEconomySummaryClicked);
			economySummary.RegisterCallback<ClickEvent>(OnEconomySummaryClicked);
			hudEventArea.UnregisterCallback<ClickEvent>(OnHudEventAreaClicked);
			hudEventArea.RegisterCallback<ClickEvent>(OnHudEventAreaClicked);
			pauseButton.clicked -= Pause;
			pauseButton.clicked += Pause;
			normalSpeedButton.clicked -= SetNormalSpeed;
			normalSpeedButton.clicked += SetNormalSpeed;
			doubleSpeedButton.clicked -= DoubleSpeed;
			doubleSpeedButton.clicked += DoubleSpeed;
			managementButton.clicked -= ToggleManagementMenu;
			managementButton.clicked += ToggleManagementMenu;
			contractManagementButton.clicked -= OpenContractManagement;
			contractManagementButton.clicked += OpenContractManagement;
			workforceManagementButton.clicked -= OpenWorkforceManagement;
			workforceManagementButton.clicked += OpenWorkforceManagement;
			ShowManagementMenu(false);
			hudRoot.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			hudRoot.RegisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			timeCluster.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			timeCluster.RegisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			hudRoot.schedule.Execute(UpdateTimeHudPlacement);
		}

		private void UnbindControls()
		{
			economySummary?.UnregisterCallback<ClickEvent>(OnEconomySummaryClicked);
			hudEventArea?.UnregisterCallback<ClickEvent>(OnHudEventAreaClicked);
			hudRoot?.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			timeCluster?.UnregisterCallback<GeometryChangedEvent>(OnHudGeometryChanged);
			if (pauseButton != null)
				pauseButton.clicked -= Pause;
			if (normalSpeedButton != null)
				normalSpeedButton.clicked -= SetNormalSpeed;
			if (doubleSpeedButton != null)
				doubleSpeedButton.clicked -= DoubleSpeed;
			if (managementButton != null)
				managementButton.clicked -= ToggleManagementMenu;
			if (contractManagementButton != null)
				contractManagementButton.clicked -= OpenContractManagement;
			if (workforceManagementButton != null)
				workforceManagementButton.clicked -= OpenWorkforceManagement;
		}

		private void BindServices()
		{
			UnbindServices();

			if (GameContext.HasInstance == false)
			{
				Debug.LogWarning("[GlobalStatusHud] GameContext is not ready.", this);
				return;
			}

			economyService = GameContext.Instance.EconomyService;
			hudEventManager = GameContext.Instance.HudEventManager;
			gameTime = GameContext.Instance.GameTime;

			if (economyService != null)
			{
				economyService.OnMoneyChanged += OnMoneyChanged;
				economyService.OnReputationChanged += OnReputationChanged;
			}

			if (hudEventManager != null)
				hudEventManager.OnRecordPublished += OnHudEventPublished;

			if (gameTime != null)
			{
				gameTime.OnTimeScaleChanged += OnTimeScaleChanged;
				gameTime.OnWeekPassed += OnWeekPassed;
			}

			RefreshAll();
		}

		private void UnbindServices()
		{
			if (economyService != null)
			{
				economyService.OnMoneyChanged -= OnMoneyChanged;
				economyService.OnReputationChanged -= OnReputationChanged;
			}

			if (hudEventManager != null)
				hudEventManager.OnRecordPublished -= OnHudEventPublished;

			if (gameTime != null)
			{
				gameTime.OnTimeScaleChanged -= OnTimeScaleChanged;
				gameTime.OnWeekPassed -= OnWeekPassed;
			}

			economyService = null;
			hudEventManager = null;
			gameTime = null;
		}

		private void OnEconomySummaryClicked(ClickEvent _)
		{
			historyWindow?.OpenEconomy();
		}

		private void OnHudEventAreaClicked(ClickEvent _)
		{
			historyWindow?.OpenEvents();
		}

		private void OnHudGeometryChanged(GeometryChangedEvent _)
		{
			UpdateTimeHudPlacement();
		}

		private void UpdateTimeHudPlacement()
		{
			if (hudRoot == null || leftHud == null || timeCluster == null)
				return;

			float panelWidth = hudRoot.resolvedStyle.width;
			float timeWidth = timeCluster.resolvedStyle.width;
			if (float.IsNaN(panelWidth) || float.IsNaN(timeWidth) || panelWidth <= 0f || timeWidth <= 0f)
				return;

			float centeredLeft = (panelWidth - timeWidth) * 0.5f;
			bool shouldDockRight = leftHud.worldBound.xMax + 10f > centeredLeft;
			if (timeHudDockedRight == shouldDockRight)
				return;

			timeHudDockedRight = shouldDockRight;
			if (shouldDockRight)
			{
				timeCluster.style.left = StyleKeyword.Auto;
				timeCluster.style.right = 12f;
				timeCluster.style.translate = new Translate(0f, 0f);
				return;
			}

			timeCluster.style.left = new Length(50f, LengthUnit.Percent);
			timeCluster.style.right = StyleKeyword.Auto;
			timeCluster.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), 0f);
		}

		private void Pause()
		{
			gameTime?.Pause();
		}

		private void SetNormalSpeed()
		{
			gameTime?.SetNormalSpeed();
		}

		private void DoubleSpeed()
		{
			gameTime?.DoubleSpeed();
		}

		private void ToggleManagementMenu()
		{
			if (managementMenu == null)
				return;

			ShowManagementMenu(managementMenu.resolvedStyle.display == DisplayStyle.None);
		}

		private void ShowManagementMenu(bool visible)
		{
			if (managementMenu == null || managementButton == null)
				return;

			managementMenu.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			managementButton.EnableInClassList("management-button--open", visible);
		}

		private void OpenContractManagement()
		{
			ShowManagementMenu(false);
			contractManagementWindow?.Open();
		}

		private void OpenWorkforceManagement()
		{
			ShowManagementMenu(false);
			workforceManagementWindow?.Open();
		}

		private void OnMoneyChanged(int value)
		{
			if (moneyValue != null)
				moneyValue.text = $"${value:N0}";
		}

		private void OnReputationChanged(float value)
		{
			if (reputationValue != null)
				reputationValue.text = value.ToString("F1");
		}

		private void OnTimeScaleChanged(float value)
		{
			RefreshSpeed(value);
			RefreshDate();
		}

		private void OnWeekPassed()
		{
			RefreshDate();
		}

		private void OnHudEventPublished(HudEventRecord record)
		{
			if (record == null || hudEventEntryTemplate == null || hudEventList == null)
				return;

			while (activeEvents.Count >= MaxVisibleEvents)
			{
				activeEvents[0].Root.RemoveFromHierarchy();
				activeEvents.RemoveAt(0);
			}

			TemplateContainer entry = hudEventEntryTemplate.CloneTree();
			VisualElement entryRoot = entry.Q<VisualElement>(className: "hud-event-entry");
			Label message = entry.Q<Label>("hud-event-message");
			entryRoot.AddToClassList($"hud-event-entry--{record.Type.ToString().ToLowerInvariant()}");
			message.text = record.Message;
			hudEventList.Add(entry);
			activeEvents.Add(new ActiveHudEvent
			{
				Root = entry,
				Elapsed = 0f,
				VisibleSeconds = record.VisibleSeconds,
			});
		}

		private void RefreshAll()
		{
			OnMoneyChanged(economyService != null ? economyService.Money : 0);
			OnReputationChanged(economyService != null ? economyService.Reputation : 0f);
			RefreshDate();
			RefreshSpeed(gameTime != null ? gameTime.TimeScale : 1f);
		}

		private void RefreshDate()
		{
			if (dateValue == null)
				return;

			dateValue.text = gameTime != null
				? $"{gameTime.Year + 1}년 {gameTime.Month}월 {gameTime.Week}째 주"
				: "1년 1월 1째 주";
		}

		private void RefreshSpeed(float value)
		{
			if (speedValue != null)
				speedValue.text = value <= 0f ? "PAUSED" : $"{value:0.#}x";

			if (doubleSpeedButton != null)
				doubleSpeedButton.SetEnabled(gameTime != null && value < gameTime.MaxTimeScale);
		}
	}
}
