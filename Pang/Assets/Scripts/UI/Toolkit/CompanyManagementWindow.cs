using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class CompanyManagementWindow : MonoBehaviour
	{
		private const string SelectedTabClass = "company-tab-button--selected";
		private static readonly List<string> FinanceFilters = new() { "All", "Income", "Expense", "Reputation" };

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset historyRowTemplate;
		private VisualTreeAsset licenseRowTemplate;
		private VisualTreeAsset researchRowTemplate;
		private readonly List<Button> tabButtons = new();
		private readonly List<VisualElement> tabs = new();
		private Label moneyLabel;
		private Label reputationLabel;
		private Label contractsLabel;
		private Label workersLabel;
		private Label payrollLabel;
		private Label buildingsLabel;
		private Label licensesLabel;
		private Label researchLabel;
		private DropdownField financeFilter;
		private ScrollView financeList;
		private Label financeEmpty;
		private ScrollView licenseList;
		private Label licenseTitle;
		private Label licenseStatus;
		private ScrollView licenseDetail;
		private Label activeResearchLabel;
		private Label activeResearchTime;
		private Label researchQueueStatus;
		private ScrollView researchList;
		private Label researchEmpty;
		private Label researchMessage;
		private EconomyService economy;
		private WorkerManager workers;
		private ContractService contracts;
		private LicenseService licenseService;
		private ResearchService researchService;
		private LicenseDefinition selectedLicense;
		private LicenseGrade selectedGrade = LicenseGrade.None;
		[System.NonSerialized] private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetHistoryRowTemplate, VisualTreeAsset targetLicenseRowTemplate,
			VisualTreeAsset targetResearchRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			historyRowTemplate = targetHistoryRowTemplate;
			licenseRowTemplate = targetLicenseRowTemplate;
			researchRowTemplate = targetResearchRowTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started) BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void OnDisable()
		{
			UnbindServices();
			initialized = false;
		}

		public void Open()
		{
			if (InitializeView() == false) return;
			if (economy == null) BindServices();
			RefreshAll();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized) return true;
			if (window == null || contentTemplate == null || historyRowTemplate == null ||
				licenseRowTemplate == null || researchRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[CompanyManagementWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			string[] buttonNames = { "company-overview-button", "company-finance-button", "company-licenses-button", "company-research-button" };
			string[] tabNames = { "company-overview-tab", "company-finance-tab", "company-licenses-tab", "company-research-tab" };
			for (int i = 0; i < buttonNames.Length; ++i)
			{
				int captured = i;
				Button button = content.Q<Button>(buttonNames[i]);
				VisualElement tab = content.Q<VisualElement>(tabNames[i]);
				if (button == null || tab == null) return false;
				button.clicked += () => SelectTab(captured);
				tabButtons.Add(button);
				tabs.Add(tab);
			}

			moneyLabel = content.Q<Label>("company-money");
			reputationLabel = content.Q<Label>("company-reputation");
			contractsLabel = content.Q<Label>("company-contracts");
			workersLabel = content.Q<Label>("company-workers");
			payrollLabel = content.Q<Label>("company-payroll");
			buildingsLabel = content.Q<Label>("company-buildings");
			licensesLabel = content.Q<Label>("company-licenses");
			researchLabel = content.Q<Label>("company-research");
			financeFilter = content.Q<DropdownField>("company-finance-filter");
			financeList = content.Q<ScrollView>("company-finance-list");
			financeEmpty = content.Q<Label>("company-finance-empty");
			licenseList = content.Q<ScrollView>("company-license-list");
			licenseTitle = content.Q<Label>("company-license-title");
			licenseStatus = content.Q<Label>("company-license-status");
			licenseDetail = content.Q<ScrollView>("company-license-detail");
			activeResearchLabel = content.Q<Label>("company-active-research");
			activeResearchTime = content.Q<Label>("company-active-research-time");
			researchQueueStatus = content.Q<Label>("company-research-queue-status");
			researchList = content.Q<ScrollView>("company-research-list");
			researchEmpty = content.Q<Label>("company-research-empty");
			researchMessage = content.Q<Label>("company-research-message");
			if (moneyLabel == null || financeFilter == null || financeList == null || licenseList == null ||
				licenseDetail == null || activeResearchLabel == null || activeResearchTime == null ||
				researchQueueStatus == null || researchList == null || researchEmpty == null || researchMessage == null)
				return false;

			financeFilter.choices = new List<string>(FinanceFilters);
			financeFilter.SetValueWithoutNotify(FinanceFilters[0]);
			financeFilter.RegisterValueChangedCallback(_ => RefreshFinance());
			window.SetTitle("Company Management");
			window.SetContent(content);
			initialized = true;
			SelectTab(0);
			return true;
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false) return;
			economy = GameContext.Instance.EconomyService;
			workers = GameContext.Instance.WorkerMgr;
			contracts = GameContext.Instance.ContractMgr;
			licenseService = GameContext.Instance.LicenseService;
			researchService = GameContext.Instance.ResearchService;
			if (economy != null)
			{
				economy.OnMoneyChanged += OnMoneyChanged;
				economy.OnReputationChanged += OnReputationChanged;
				economy.OnTransactionApplied += OnTransactionApplied;
			}
			if (workers != null) workers.OnWorkersChanged += RefreshOverview;
			if (contracts != null) contracts.OnContractsChanged += RefreshOverview;
			if (licenseService != null) licenseService.OnLicensesChanged += OnLicensesChanged;
			if (researchService != null) researchService.OnResearchStateChanged += OnResearchChanged;
		}

		private void UnbindServices()
		{
			if (economy != null)
			{
				economy.OnMoneyChanged -= OnMoneyChanged;
				economy.OnReputationChanged -= OnReputationChanged;
				economy.OnTransactionApplied -= OnTransactionApplied;
			}
			if (workers != null) workers.OnWorkersChanged -= RefreshOverview;
			if (contracts != null) contracts.OnContractsChanged -= RefreshOverview;
			if (licenseService != null) licenseService.OnLicensesChanged -= OnLicensesChanged;
			if (researchService != null) researchService.OnResearchStateChanged -= OnResearchChanged;
			economy = null;
			workers = null;
			contracts = null;
			licenseService = null;
			researchService = null;
		}

		private void SelectTab(int index)
		{
			for (int i = 0; i < tabs.Count; ++i)
			{
				tabs[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
				tabButtons[i].EnableInClassList(SelectedTabClass, i == index);
			}
		}

		private void RefreshAll()
		{
			RefreshOverview();
			RefreshFinance();
			RefreshLicenses();
			RefreshResearch();
		}

		private void RefreshOverview()
		{
			moneyLabel.text = $"${economy?.Money ?? 0:N0}";
			reputationLabel.text = (economy?.Reputation ?? 0f).ToString("0.0");
			contractsLabel.text = (contracts?.ActiveContracts.Count ?? 0).ToString();
			workersLabel.text = (workers?.Workers.Count ?? 0).ToString();
			payrollLabel.text = $"${workers?.CostPerMonth ?? 0:N0}";
			int buildingCount = GameContext.HasInstance && GameContext.Instance.BuildingMgr != null ? GameContext.Instance.BuildingMgr.RegisteredBuildings.Count : 0;
			buildingsLabel.text = $"{buildingCount} / {CountFacilities()}";
			int acquired = licenseService?.AcquiredLicenses.Count ?? 0;
			int nonCompliant = licenseService?.NonCompliantLicenses.Count ?? 0;
			licensesLabel.text = nonCompliant > 0 ? $"{acquired} active · {nonCompliant} warning" : $"{acquired} active";
			if (researchService != null && researchService.IsResearching)
			{
				string queued = researchService.QueuedResearchCount > 0
					? $" · {researchService.QueuedResearchCount} queued"
					: string.Empty;
				researchLabel.text = $"{researchService.ActiveResearch?.DisplayName} · {researchService.RemainingWeeks}w{queued}";
			}
			else if (researchService != null && researchService.QueuedResearchCount > 0)
			{
				researchLabel.text = $"Queue paused · {researchService.QueuedResearchCount} queued";
			}
			else
			{
				researchLabel.text = "Idle";
			}
		}

		private int CountFacilities()
		{
			if (GameContext.HasInstance == false || GameContext.Instance.FacilityMgr == null) return 0;
			FacilityManager manager = GameContext.Instance.FacilityMgr;
			int count = 0;
			foreach (uint buildingId in manager.GetBuildingIds()) count += manager.GetFacilities<IFacility>(buildingId).Count;
			return count;
		}

		private void RefreshFinance()
		{
			financeList.Clear();
			IReadOnlyList<EconomyTransaction> history = economy?.History;
			int visible = 0;
			for (int i = (history?.Count ?? 0) - 1; i >= 0; --i)
			{
				EconomyTransaction transaction = history[i];
				if (transaction == null || MatchesFinanceFilter(transaction) == false) continue;
				TemplateContainer row = historyRowTemplate.CloneTree();
				row.Q<Label>("history-row-kind").text = "TRANSACTION";
				row.Q<Label>("history-row-message").text = EconomyService.FormatReason(transaction.reason);
				row.Q<Label>("history-row-delta").text = FormatTransactionDelta(transaction);
				financeList.Add(row);
				++visible;
			}
			financeEmpty.style.display = visible == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private bool MatchesFinanceFilter(EconomyTransaction transaction) => financeFilter.value switch
		{
			"Income" => transaction.moneyDelta > 0,
			"Expense" => transaction.moneyDelta < 0,
			"Reputation" => Mathf.Approximately(transaction.reputationDelta, 0f) == false,
			_ => true,
		};

		private void RefreshLicenses()
		{
			licenseList.Clear();
			IReadOnlyList<LicenseDefinition> definitions = licenseService?.Definitions;
			if (selectedLicense == null || ContainsLicense(definitions, selectedLicense) == false)
			{
				selectedLicense = definitions != null && definitions.Count > 0 ? definitions[0] : null;
				selectedGrade = GetFirstGrade(selectedLicense);
			}
			else if (selectedLicense.HasGrade(selectedGrade) == false)
				selectedGrade = GetFirstGrade(selectedLicense);
			if (definitions != null)
			{
				foreach (LicenseDefinition definition in definitions)
				{
					if (definition == null) continue;
					LicenseDefinition captured = definition;
					Button button = new(() =>
					{
						selectedLicense = captured;
						selectedGrade = GetFirstGrade(captured);
						RefreshLicenses();
					}) { text = FormatLicenseName(definition) };
					button.AddToClassList("company-license-button");
					button.EnableInClassList("company-license-button--selected", definition == selectedLicense);
					licenseList.Add(button);
				}
			}
			RefreshLicenseDetail();
		}

		private void RefreshLicenseDetail()
		{
			licenseDetail.Clear();
			if (selectedLicense == null || licenseService == null)
			{
				licenseTitle.text = "No License definitions";
				licenseStatus.text = string.Empty;
				return;
			}
			licenseTitle.text = selectedLicense.DisplayName;
			bool acquired = licenseService.TryGetAcquiredState(selectedLicense.LicenseId, out AcquiredLicenseState state);
			licenseStatus.text = acquired ? $"Active Grade {state.Grade} · {state.ComplianceState}" : "Not acquired";
			foreach (LicenseGradeDefinition gradeDefinition in selectedLicense.Grades)
			{
				if (gradeDefinition == null) continue;
				LicenseGrade grade = gradeDefinition.Grade;
				LicenseEvaluationResult evaluation = licenseService.Evaluate(selectedLicense, grade);
				LicenseGrade current = acquired ? state.Grade : LicenseGrade.None;
				bool upgrade = LicenseGradeUtility.IsUpgrade(current, grade);
				bool canAcquire = upgrade && evaluation.IsSatisfied;
				bool selected = grade == selectedGrade;
				string description = selected
					? BuildLicenseEvaluationSummary(evaluation)
					: evaluation.IsSatisfied ? "Ready" : "Requirements not met";
				VisualElement row = CreateLicenseRow(evaluation.IsSatisfied ? "OK" : "X", $"Grade {grade}", description,
					canAcquire ? "Acquire" : string.Empty, canAcquire, () => AcquireLicense(grade));
				row.EnableInClassList("company-detail-row--selected", selected);
				row.RegisterCallback<ClickEvent>(_ => SelectLicenseGrade(grade));
				licenseDetail.Add(row);
			}
			if (acquired)
			{
				string licenseId = selectedLicense.LicenseId;
				licenseDetail.Add(CreateLicenseRow("", "Return License", "Removes the currently held grade.", "Return", true,
					() => licenseService.TryReturnLicense(licenseId)));
			}
		}

		private VisualElement CreateLicenseRow(string marker, string title, string description, string actionText, bool enabled, Action action)
		{
			TemplateContainer row = licenseRowTemplate.CloneTree();
			row.Q<Label>("company-detail-marker").text = marker;
			row.Q<Label>("company-detail-title").text = title;
			row.Q<Label>("company-detail-description").text = description;
			Button button = row.Q<Button>("company-detail-action");
			button.text = actionText;
			button.style.display = string.IsNullOrWhiteSpace(actionText) ? DisplayStyle.None : DisplayStyle.Flex;
			button.SetEnabled(enabled);
			if (action != null) button.clicked += action;
			return row;
		}

		private void AcquireLicense(LicenseGrade grade) => licenseService?.TryAcquireLicense(selectedLicense, grade, out _);

		private void SelectLicenseGrade(LicenseGrade grade)
		{
			if (selectedGrade == grade) return;
			selectedGrade = grade;
			RefreshLicenseDetail();
		}

		private void RefreshResearch()
		{
			researchList.Clear();
			ResearchDefinition active = researchService?.ActiveResearch;
			activeResearchLabel.text = active != null ? active.DisplayName : "No active research";
			activeResearchTime.text = active != null ? $"{researchService.RemainingWeeks} weeks remaining" : string.Empty;
			researchQueueStatus.text = BuildResearchQueueStatus();
			IReadOnlyList<ResearchDefinition> definitions = researchService?.Definitions ?? Array.Empty<ResearchDefinition>();
			Dictionary<string, int> depths = new(StringComparer.Ordinal);
			VisualElement tree = new();
			tree.AddToClassList("company-research-tree");
			for (int depth = 0; depth < definitions.Count; ++depth)
			{
				VisualElement column = new();
				column.AddToClassList("company-research-tree__column");
				Label stage = new(depth == 0 ? "FOUNDATION" : $"STAGE {depth + 1}");
				stage.AddToClassList("company-research-tree__stage");
				column.Add(stage);

				for (int i = 0; i < definitions.Count; ++i)
				{
					ResearchDefinition definition = definitions[i];
					if (definition != null && GetResearchDepth(definition, depths, new HashSet<string>(StringComparer.Ordinal)) == depth)
						column.Add(CreateResearchRow(definition));
				}

				if (column.childCount > 1)
					tree.Add(column);
			}
			researchList.Add(tree);
			researchEmpty.style.display = definitions.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			if (definitions.Count == 0) researchMessage.text = string.Empty;
		}

		private int GetResearchDepth(
			ResearchDefinition definition,
			IDictionary<string, int> depths,
			ISet<string> visiting)
		{
			if (depths.TryGetValue(definition.Uid, out int cachedDepth))
				return cachedDepth;
			if (visiting.Add(definition.Uid) == false)
				return 0;

			int depth = 0;
			for (int i = 0; i < definition.PrerequisiteUids.Count; ++i)
			{
				if (researchService.Catalog.TryGet(definition.PrerequisiteUids[i], out ResearchDefinition prerequisite))
					depth = Math.Max(depth, GetResearchDepth(prerequisite, depths, visiting) + 1);
			}

			visiting.Remove(definition.Uid);
			depths[definition.Uid] = depth;
			return depth;
		}

		private VisualElement CreateResearchRow(ResearchDefinition definition)
		{
			TemplateContainer row = researchRowTemplate.CloneTree();
			ResearchState state = researchService.GetState(definition.Uid);
			int queueIndex = researchService.GetQueueIndex(definition.Uid);
			row.Q<Label>("research-name").text = definition.DisplayName;
			row.Q<Label>("research-description").text = definition.Description;
			row.Q<Label>("research-prerequisites").text = BuildPrerequisiteSummary(definition);
			row.Q<Label>("research-cost").text = $"${definition.Cost:N0}";
			row.Q<Label>("research-duration").text = $"{definition.DurationWeeks} week{(definition.DurationWeeks == 1 ? string.Empty : "s")}";
			row.Q<Label>("research-state").text = state == ResearchState.Queued
				? $"Queued #{queueIndex + 1}"
				: state.ToString();
			Button button = row.Q<Button>("research-start-button");
			Button upButton = row.Q<Button>("research-queue-up-button");
			Button downButton = row.Q<Button>("research-queue-down-button");
			VisualElement orderActions = row.Q<VisualElement>("research-order-actions");
			bool canEnqueue = researchService.CanEnqueueResearch(
				definition.Uid,
				out ResearchStartFailureReason reason);
			button.text = state switch
			{
				ResearchState.Completed => "Done",
				ResearchState.InProgress => "Active",
				ResearchState.Queued => "Remove",
				_ => "Queue",
			};
			button.SetEnabled(state == ResearchState.Queued || canEnqueue);
			orderActions.style.display = state == ResearchState.Queued ? DisplayStyle.Flex : DisplayStyle.None;
			upButton.SetEnabled(state == ResearchState.Queued && queueIndex > 0);
			downButton.SetEnabled(
				state == ResearchState.Queued &&
				queueIndex >= 0 &&
				queueIndex < researchService.QueuedResearchCount - 1);

			VisualElement rowRoot = row.Q<VisualElement>(className: "company-research-row");
			if (rowRoot != null)
			{
				rowRoot.EnableInClassList("company-research-row--queued", state == ResearchState.Queued);
				rowRoot.EnableInClassList("company-research-row--active", state == ResearchState.InProgress);
				rowRoot.EnableInClassList("company-research-row--completed", state == ResearchState.Completed);
			}

			row.SetTooltip(state == ResearchState.Available || state == ResearchState.Queued ||
				state == ResearchState.InProgress || state == ResearchState.Completed
				? UITooltipContent.DescriptionOnly(definition.DisplayName, definition.Description)
				: UITooltipContent.Locked(definition.DisplayName, definition.Description, FormatResearchFailure(reason)));
			button.clicked += () => HandleResearchAction(definition);
			upButton.clicked += () => MoveQueuedResearch(definition, -1);
			downButton.clicked += () => MoveQueuedResearch(definition, 1);
			return row;
		}

		private void HandleResearchAction(ResearchDefinition definition)
		{
			if (researchService.GetState(definition.Uid) == ResearchState.Queued)
			{
				if (researchService.TryRemoveQueuedResearch(definition.Uid, out ResearchStartFailureReason removeReason))
					researchMessage.text = $"Removed {definition.DisplayName} from the queue.";
				else
					researchMessage.text = FormatResearchFailure(removeReason);
				return;
			}

			if (researchService.TryEnqueueResearch(definition.Uid, out ResearchStartFailureReason reason) == false)
			{
				researchMessage.text = FormatResearchFailure(reason);
				return;
			}

			ResearchState state = researchService.GetState(definition.Uid);
			researchMessage.text = state == ResearchState.InProgress
				? $"Started {definition.DisplayName}."
				: $"Queued {definition.DisplayName} at #{researchService.GetQueueIndex(definition.Uid) + 1}.";
		}

		private void MoveQueuedResearch(ResearchDefinition definition, int direction)
		{
			int currentIndex = researchService.GetQueueIndex(definition.Uid);
			if (researchService.TryMoveQueuedResearch(
				definition.Uid,
				currentIndex + direction,
				out ResearchStartFailureReason reason))
			{
				ResearchState state = researchService.GetState(definition.Uid);
				researchMessage.text = state == ResearchState.InProgress
					? $"Started {definition.DisplayName}."
					: $"Moved {definition.DisplayName} to queue #{researchService.GetQueueIndex(definition.Uid) + 1}.";
			}
			else
			{
				researchMessage.text = FormatResearchFailure(reason);
			}
		}

		private string BuildResearchQueueStatus()
		{
			if (researchService == null || researchService.QueuedResearchCount == 0)
				return "Queue empty";

			string nextId = researchService.QueuedResearchIds[0];
			string nextName = researchService.Catalog != null &&
				researchService.Catalog.TryGet(nextId, out ResearchDefinition next)
					? next.DisplayName
					: nextId;
			if (researchService.TryGetQueueBlockReason(out ResearchStartFailureReason reason))
				return $"Queue paused · Next: {nextName} · {FormatResearchFailure(reason)}";

			return $"{researchService.QueuedResearchCount} queued · Next: {nextName}";
		}

		private void OnMoneyChanged(int _) { RefreshOverview(); RefreshResearch(); }
		private void OnReputationChanged(float _) => RefreshOverview();
		private void OnTransactionApplied(EconomyTransaction _) => RefreshFinance();
		private void OnLicensesChanged() { RefreshOverview(); RefreshLicenses(); }
		private void OnResearchChanged() { RefreshOverview(); RefreshResearch(); }

		private static string FormatTransactionDelta(EconomyTransaction transaction)
		{
			string money = transaction.moneyDelta == 0 ? string.Empty : $"{transaction.moneyDelta:+$#,0;-$#,0}";
			string reputation = Mathf.Approximately(transaction.reputationDelta, 0f) ? string.Empty : $"{transaction.reputationDelta:+0.#;-0.#} REP";
			return string.IsNullOrEmpty(money) ? reputation : string.IsNullOrEmpty(reputation) ? money : $"{money}  {reputation}";
		}

		private string FormatLicenseName(LicenseDefinition definition)
		{
			if (licenseService.TryGetAcquiredState(definition.LicenseId, out AcquiredLicenseState state) == false) return definition.DisplayName;
			return state.IsCompliant ? $"{definition.DisplayName} [{state.Grade}]" : $"{definition.DisplayName} [{state.Grade}]  !";
		}

		private static string BuildLicenseEvaluationSummary(LicenseEvaluationResult evaluation)
		{
			if (evaluation.Groups.Count == 0) return evaluation.IsSatisfied ? "No requirements." : "Unavailable.";
			StringBuilder text = new();
			foreach (LicenseConditionGroupEvaluation group in evaluation.Groups)
			{
				if (text.Length > 0) text.AppendLine();
				text.Append(group.IsSatisfied ? "Met" : "Missing");
				if (group.BuildingId != 0) text.Append($" at Building #{group.BuildingId}");
				if (group.Conditions.Count == 0)
				{
					IReadOnlyList<LicenseCondition> requiredConditions = group.Group?.Conditions;
					if (requiredConditions == null || requiredConditions.Count == 0)
					{
						text.AppendLine();
						text.Append("Active building required");
						continue;
					}

					text.Append(" · No current observation");
					foreach (LicenseCondition condition in requiredConditions)
					{
						if (condition == null) continue;
						text.AppendLine();
						text.Append($"[X] {condition.Metric}: unavailable " +
							$"{FormatComparison(condition.Comparison)} {condition.TargetValue:0.##}");
					}
					continue;
				}

				foreach (LicenseConditionEvaluation condition in group.Conditions)
				{
					string result = condition.IsSatisfied ? "OK" : "X";
					text.AppendLine();
					text.Append($"[{result}] {condition.Condition.Metric}: {condition.ObservedValue:0.##} " +
						$"{FormatComparison(condition.Condition.Comparison)} {condition.Condition.TargetValue:0.##}");
				}
			}
			return text.ToString();
		}

		private static string FormatComparison(LicenseNumericComparison comparison) => comparison switch
		{
			LicenseNumericComparison.Equal => "=",
			LicenseNumericComparison.LessThan => "<",
			LicenseNumericComparison.LessThanOrEqual => "<=",
			LicenseNumericComparison.GreaterThan => ">",
			LicenseNumericComparison.GreaterThanOrEqual => ">=",
			_ => "?",
		};

		private string BuildPrerequisiteSummary(ResearchDefinition definition)
		{
			if (definition.PrerequisiteUids.Count == 0) return "No prerequisites";
			List<string> parts = new();
			foreach (string id in definition.PrerequisiteUids)
			{
				string name = researchService.Catalog != null &&
					researchService.Catalog.TryGet(id, out ResearchDefinition prerequisite)
						? prerequisite.DisplayName
						: id;
				int queueIndex = researchService.GetQueueIndex(id);
				string marker = researchService.IsResearched(id)
					? "[DONE]"
					: string.Equals(researchService.ActiveResearchId, id, StringComparison.Ordinal)
						? "[ACTIVE]"
						: queueIndex >= 0
							? $"[QUEUE #{queueIndex + 1}]"
							: "[MISSING]";
				parts.Add($"{marker} {name}");
			}
			return string.Join(" · ", parts);
		}

		private static string FormatResearchFailure(ResearchStartFailureReason reason) => reason switch
		{
			ResearchStartFailureReason.MissingPrerequisite => "Required research is incomplete.",
			ResearchStartFailureReason.InsufficientFunds => "Insufficient funds.",
			ResearchStartFailureReason.ResearchInProgress => "Another research is already in progress.",
			ResearchStartFailureReason.AlreadyResearched => "Research is already completed.",
			ResearchStartFailureReason.AlreadyQueued => "Research is already queued.",
			ResearchStartFailureReason.NotQueued => "Research is not in the queue.",
			ResearchStartFailureReason.InvalidQueuePosition => "That queue position is unavailable.",
			ResearchStartFailureReason.InvalidQueueOrder => "That change would break prerequisite order.",
			ResearchStartFailureReason.ServiceUnavailable => "Research service is unavailable.",
			ResearchStartFailureReason.UnknownResearch => "Unknown research definition.",
			_ => string.Empty,
		};

		private static bool ContainsLicense(IReadOnlyList<LicenseDefinition> definitions, LicenseDefinition target)
		{
			if (definitions == null || target == null) return false;
			for (int i = 0; i < definitions.Count; ++i) if (definitions[i] == target) return true;
			return false;
		}

		private static LicenseGrade GetFirstGrade(LicenseDefinition definition)
		{
			if (definition?.Grades == null) return LicenseGrade.None;
			for (int i = 0; i < definition.Grades.Count; ++i)
			{
				LicenseGradeDefinition grade = definition.Grades[i];
				if (grade != null) return grade.Grade;
			}
			return LicenseGrade.None;
		}
	}
}
