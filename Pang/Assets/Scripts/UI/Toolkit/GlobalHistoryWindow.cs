using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class GlobalHistoryWindow : MonoBehaviour
	{
		private const string SelectedButtonClass = "history-section-button--selected";

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset rowTemplate;
		private Button economyButton;
		private Button eventButton;
		private VisualElement economySection;
		private VisualElement eventSection;
		private ScrollView economyList;
		private ScrollView eventList;
		private Label economyEmptyLabel;
		private Label eventEmptyLabel;
		private EconomyService economyService;
		private HudEventManager hudEventManager;
		private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate, VisualTreeAsset targetRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			rowTemplate = targetRowTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
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
			initialized = false;
		}

		public void OpenEconomy()
		{
			Open(0);
		}

		public void OpenEvents()
		{
			Open(1);
		}

		private void Open(int sectionIndex)
		{
			if (InitializeView() == false)
				return;

			if (economyService == null && hudEventManager == null)
				BindServices();

			RefreshHistory();
			SelectSection(sectionIndex);
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || rowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[GlobalHistoryWindow] Window or VisualTreeAsset references are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			economyButton = content.Q<Button>("economy-history-button");
			eventButton = content.Q<Button>("event-history-button");
			economySection = content.Q<VisualElement>("economy-history-section");
			eventSection = content.Q<VisualElement>("event-history-section");
			economyList = content.Q<ScrollView>("economy-history-list");
			eventList = content.Q<ScrollView>("event-history-list");
			economyEmptyLabel = content.Q<Label>("economy-history-empty");
			eventEmptyLabel = content.Q<Label>("event-history-empty");

			if (economyButton == null || eventButton == null || economySection == null || eventSection == null ||
				economyList == null || eventList == null || economyEmptyLabel == null || eventEmptyLabel == null)
			{
				Debug.LogError("[GlobalHistoryWindow] Required history content elements are missing.", this);
				return false;
			}

			window.SetTitle("Company History");
			window.SetContent(content);
			economyButton.clicked += OpenEconomy;
			eventButton.clicked += OpenEvents;
			initialized = true;
			SelectSection(0);
			return true;
		}

		private void UnbindControls()
		{
			if (economyButton != null)
				economyButton.clicked -= OpenEconomy;
			if (eventButton != null)
				eventButton.clicked -= OpenEvents;
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			economyService = GameContext.Instance.EconomyService;
			hudEventManager = GameContext.Instance.HudEventManager;

			if (economyService != null)
				economyService.OnTransactionApplied += OnTransactionApplied;
			if (hudEventManager != null)
				hudEventManager.OnRecordPublished += OnHudEventPublished;
		}

		private void UnbindServices()
		{
			if (economyService != null)
				economyService.OnTransactionApplied -= OnTransactionApplied;
			if (hudEventManager != null)
				hudEventManager.OnRecordPublished -= OnHudEventPublished;

			economyService = null;
			hudEventManager = null;
		}

		private void OnTransactionApplied(EconomyTransaction _)
		{
			if (window != null && window.IsOpen)
				RefreshEconomyHistory();
		}

		private void OnHudEventPublished(HudEventRecord _)
		{
			if (window != null && window.IsOpen)
				RefreshEventHistory();
		}

		private void SelectSection(int sectionIndex)
		{
			bool showEconomy = sectionIndex == 0;
			economySection.style.display = showEconomy ? DisplayStyle.Flex : DisplayStyle.None;
			eventSection.style.display = showEconomy ? DisplayStyle.None : DisplayStyle.Flex;
			economyButton.EnableInClassList(SelectedButtonClass, showEconomy);
			eventButton.EnableInClassList(SelectedButtonClass, showEconomy == false);
		}

		private void RefreshHistory()
		{
			RefreshEconomyHistory();
			RefreshEventHistory();
		}

		private void RefreshEconomyHistory()
		{
			if (economyList == null)
				return;

			economyList.Clear();
			IReadOnlyList<EconomyTransaction> history = economyService?.History;
			int count = history?.Count ?? 0;
			for (int i = count - 1; i >= 0; --i)
			{
				EconomyTransaction transaction = history[i];
				if (transaction == null)
					continue;

				string delta = FormatEconomyDelta(transaction);
				economyList.Add(CreateRow("TRANSACTION", EconomyService.FormatReason(transaction.reason), delta, "history-row--economy"));
			}

			economyEmptyLabel.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void RefreshEventHistory()
		{
			if (eventList == null)
				return;

			eventList.Clear();
			IReadOnlyCollection<HudEventRecord> history = hudEventManager?.History;
			if (history != null)
			{
				List<HudEventRecord> records = new(history);
				for (int i = records.Count - 1; i >= 0; --i)
				{
					HudEventRecord record = records[i];
					eventList.Add(CreateRow(record.Type.ToString().ToUpperInvariant(), record.Message, string.Empty,
						$"history-row--{record.Type.ToString().ToLowerInvariant()}"));
				}
			}

			eventEmptyLabel.style.display = history == null || history.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private VisualElement CreateRow(string kind, string message, string delta, string modifierClass)
		{
			TemplateContainer row = rowTemplate.CloneTree();
			row.Q<VisualElement>(className: "history-row").AddToClassList(modifierClass);
			row.Q<Label>("history-row-kind").text = kind;
			row.Q<Label>("history-row-message").text = message;
			Label deltaLabel = row.Q<Label>("history-row-delta");
			deltaLabel.text = delta;
			deltaLabel.style.display = string.IsNullOrEmpty(delta) ? DisplayStyle.None : DisplayStyle.Flex;
			return row;
		}

		private static string FormatEconomyDelta(EconomyTransaction transaction)
		{
			string money = transaction.moneyDelta == 0 ? string.Empty : $"{transaction.moneyDelta:+$#,0;-$#,0}";
			string reputation = Mathf.Approximately(transaction.reputationDelta, 0f)
				? string.Empty
				: $"{transaction.reputationDelta:+0.#;-0.#} REP";

			if (string.IsNullOrEmpty(money))
				return reputation;
			if (string.IsNullOrEmpty(reputation))
				return money;
			return $"{money}  {reputation}";
		}
	}
}
