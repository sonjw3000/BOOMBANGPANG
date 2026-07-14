using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class GlobalStatusHud : MonoBehaviour
	{
		private const string DocumentObjectName = "GlobalStatusHudDocument";

		[SerializeField] private VisualTreeAsset visualTreeAsset;
		[SerializeField] private PanelSettings panelSettings;
		[SerializeField] private int sortingOrder = 100;

		private UIDocument uiDocument;
		private Label moneyValue;
		private Label reputationValue;
		private Label speedValue;
		private Button pauseButton;
		private Button normalSpeedButton;
		private Button doubleSpeedButton;
		private EconomyService economyService;
		private GameTime gameTime;
		private bool started;

		private void OnEnable()
		{
			EnsureDocument();
			BindControls();

			if (started)
				BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
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

		private void BindControls()
		{
			if (uiDocument == null)
				return;

			VisualElement root = uiDocument.rootVisualElement;
			moneyValue = root.Q<Label>("money-value");
			reputationValue = root.Q<Label>("reputation-value");
			speedValue = root.Q<Label>("speed-value");
			pauseButton = root.Q<Button>("pause-button");
			normalSpeedButton = root.Q<Button>("normal-speed-button");
			doubleSpeedButton = root.Q<Button>("double-speed-button");

			if (moneyValue == null || reputationValue == null || speedValue == null ||
				pauseButton == null || normalSpeedButton == null || doubleSpeedButton == null)
			{
				Debug.LogError("[GlobalStatusHud] Required UXML elements are missing.", this);
				return;
			}

			pauseButton.clicked -= Pause;
			pauseButton.clicked += Pause;
			normalSpeedButton.clicked -= SetNormalSpeed;
			normalSpeedButton.clicked += SetNormalSpeed;
			doubleSpeedButton.clicked -= DoubleSpeed;
			doubleSpeedButton.clicked += DoubleSpeed;
		}

		private void UnbindControls()
		{
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
			gameTime = GameContext.Instance.GameTime;

			if (economyService != null)
			{
				economyService.OnMoneyChanged += OnMoneyChanged;
				economyService.OnReputationChanged += OnReputationChanged;
			}

			if (gameTime != null)
				gameTime.OnTimeScaleChanged += OnTimeScaleChanged;

			RefreshAll();
		}

		private void UnbindServices()
		{
			if (economyService != null)
			{
				economyService.OnMoneyChanged -= OnMoneyChanged;
				economyService.OnReputationChanged -= OnReputationChanged;
			}

			if (gameTime != null)
				gameTime.OnTimeScaleChanged -= OnTimeScaleChanged;

			economyService = null;
			gameTime = null;
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
		}

		private void RefreshAll()
		{
			OnMoneyChanged(economyService != null ? economyService.Money : 0);
			OnReputationChanged(economyService != null ? economyService.Reputation : 0f);
			RefreshSpeed(gameTime != null ? gameTime.TimeScale : 1f);
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
