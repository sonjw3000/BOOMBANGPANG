using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class GlobalStatusHud : MonoBehaviour
	{
		private const string DocumentObjectName = "GlobalStatusHudDocument";
		private const string HistoryDocumentObjectName = "GlobalHistoryWindowDocument";
		private const int MaxVisibleEvents = 5;
		private const float EventFadeSeconds = 0.8f;

		[SerializeField] private VisualTreeAsset visualTreeAsset;
		[SerializeField] private VisualTreeAsset hudEventEntryTemplate;
		[SerializeField] private VisualTreeAsset windowVisualTreeAsset;
		[SerializeField] private VisualTreeAsset historyContentTemplate;
		[SerializeField] private VisualTreeAsset historyRowTemplate;
		[SerializeField] private PanelSettings panelSettings;
		[SerializeField] private int sortingOrder = 100;

		private readonly List<ActiveHudEvent> activeEvents = new();
		private UIDocument uiDocument;
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
		private GlobalHistoryWindow historyWindow;
		private EconomyService economyService;
		private HudEventManager hudEventManager;
		private GameTime gameTime;
		private bool started;

		private sealed class ActiveHudEvent
		{
			public VisualElement Root;
			public float Elapsed;
			public float VisibleSeconds;
		}

		private void OnEnable()
		{
			EnsureDocument();
			EnsureHistoryWindow();
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

		private void BindControls()
		{
			if (uiDocument == null)
				return;

			VisualElement root = uiDocument.rootVisualElement;
			root.pickingMode = PickingMode.Ignore;
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

			if (economySummary == null || hudEventArea == null || hudEventList == null || moneyValue == null ||
				reputationValue == null || dateValue == null || speedValue == null || pauseButton == null ||
				normalSpeedButton == null || doubleSpeedButton == null)
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
		}

		private void UnbindControls()
		{
			economySummary?.UnregisterCallback<ClickEvent>(OnEconomySummaryClicked);
			hudEventArea?.UnregisterCallback<ClickEvent>(OnHudEventAreaClicked);
			if (pauseButton != null)
				pauseButton.clicked -= Pause;
			if (normalSpeedButton != null)
				normalSpeedButton.clicked -= SetNormalSpeed;
			if (doubleSpeedButton != null)
				doubleSpeedButton.clicked -= DoubleSpeed;
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
