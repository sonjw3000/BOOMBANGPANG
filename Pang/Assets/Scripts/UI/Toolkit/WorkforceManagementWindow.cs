using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class WorkforceManagementWindow : MonoBehaviour
	{
		private const string SelectedTabClass = "workforce-tab-button--selected";
		private const string SelectedRowClass = "workforce-worker-row--selected";
		private const string SelectedCategoryClass = "workforce-category-button--selected";
		private const int CandidateCount = 15;
		private static readonly List<string> RosterFilters = new() { "All", "Human", "Robot", "Unassigned" };
		private static readonly List<string> HandleGroups = new() { "Undefined", "Cargo Handle", "Item Handle" };

		private enum HandleGroup { Undefined, Cargo, Item }

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset rosterRowTemplate;
		private VisualTreeAsset candidateRowTemplate;
		private IReadOnlyList<WorkforceMarketData_SO> humanMarkets;
		private IReadOnlyList<WorkforceMarketData_SO> robotMarkets;
		private Button rosterButton;
		private Button hiringButton;
		private VisualElement rosterTab;
		private VisualElement hiringTab;
		private Label totalLabel;
		private Label unassignedLabel;
		private Label workingLabel;
		private Label blockedLabel;
		private Label payrollLabel;
		private DropdownField filterField;
		private ScrollView workerList;
		private Label workerListEmpty;
		private VisualElement workerDetail;
		private VisualElement workerDetailEmpty;
		private Label workerName;
		private Label workerKind;
		private Label workerStatus;
		private Label workerCondition;
		private Label workerWear;
		private Label workerPay;
		private DropdownField buildingField;
		private DropdownField handleField;
		private ScrollView taskToggles;
		private ScrollView categoryList;
		private ScrollView candidateList;
		private Label hiringMessage;
		private Button createSpawnAreaButton;
		private readonly List<uint> buildingIds = new();
		private readonly List<WorkerArchetype> candidates = new();
		private readonly HashSet<WorkerArchetype> hiredCandidates = new();
		private WorkerManager workerManager;
		private EconomyService economyService;
		private WorkforceMarketData_SO selectedMarket;
		private AIWorker selectedWorker;
		private HandleGroup selectedHandleGroup;
		private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetRosterRowTemplate, VisualTreeAsset targetCandidateRowTemplate,
			IReadOnlyList<WorkforceMarketData_SO> targetHumanMarkets,
			IReadOnlyList<WorkforceMarketData_SO> targetRobotMarkets)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			rosterRowTemplate = targetRosterRowTemplate;
			candidateRowTemplate = targetCandidateRowTemplate;
			humanMarkets = targetHumanMarkets;
			robotMarkets = targetRobotMarkets;
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
			UnbindControls();
			UnbindServices();
			initialized = false;
		}

		public void Open()
		{
			if (InitializeView() == false) return;
			if (workerManager == null) BindServices();
			GenerateCandidatesOnOpen();
			RefreshRoster();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized) return true;
			if (window == null || contentTemplate == null || rosterRowTemplate == null ||
				candidateRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[WorkforceManagementWindow] Window or VisualTreeAsset references are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			rosterButton = content.Q<Button>("workforce-roster-button");
			hiringButton = content.Q<Button>("workforce-hiring-button");
			rosterTab = content.Q<VisualElement>("workforce-roster-tab");
			hiringTab = content.Q<VisualElement>("workforce-hiring-tab");
			totalLabel = content.Q<Label>("workforce-total");
			unassignedLabel = content.Q<Label>("workforce-unassigned");
			workingLabel = content.Q<Label>("workforce-working");
			blockedLabel = content.Q<Label>("workforce-blocked");
			payrollLabel = content.Q<Label>("workforce-payroll");
			filterField = content.Q<DropdownField>("worker-filter-field");
			workerList = content.Q<ScrollView>("worker-list");
			workerListEmpty = content.Q<Label>("worker-list-empty");
			workerDetail = content.Q<VisualElement>("worker-detail");
			workerDetailEmpty = content.Q<VisualElement>("worker-detail-empty");
			workerName = content.Q<Label>("worker-detail-name");
			workerKind = content.Q<Label>("worker-detail-kind");
			workerStatus = content.Q<Label>("worker-detail-status");
			workerCondition = content.Q<Label>("worker-detail-condition");
			workerWear = content.Q<Label>("worker-detail-wear");
			workerPay = content.Q<Label>("worker-detail-pay");
			buildingField = content.Q<DropdownField>("worker-building-field");
			handleField = content.Q<DropdownField>("worker-handle-field");
			taskToggles = content.Q<ScrollView>("worker-task-toggles");
			categoryList = content.Q<ScrollView>("workforce-category-list");
			candidateList = content.Q<ScrollView>("workforce-candidate-list");
			hiringMessage = content.Q<Label>("workforce-hiring-message");
			createSpawnAreaButton = content.Q<Button>("create-worker-spawn-area-button");

			if (rosterButton == null || hiringButton == null || rosterTab == null || hiringTab == null ||
				totalLabel == null || filterField == null || workerList == null || workerDetail == null ||
				buildingField == null || handleField == null || taskToggles == null || categoryList == null ||
				candidateList == null || hiringMessage == null || createSpawnAreaButton == null)
			{
				Debug.LogError("[WorkforceManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Workforce Management");
			window.SetContent(content);
			rosterButton.clicked += OpenRoster;
			hiringButton.clicked += OpenHiring;
			filterField.choices = new List<string>(RosterFilters);
			filterField.SetValueWithoutNotify(RosterFilters[0]);
			filterField.RegisterValueChangedCallback(OnFilterChanged);
			buildingField.RegisterValueChangedCallback(OnBuildingChanged);
			handleField.choices = new List<string>(HandleGroups);
			handleField.RegisterValueChangedCallback(OnHandleChanged);
			createSpawnAreaButton.clicked += BeginWorkerSpawnAreaCreation;
			initialized = true;
			SelectTab(true);
			return true;
		}

		private void UnbindControls()
		{
			if (rosterButton != null) rosterButton.clicked -= OpenRoster;
			if (hiringButton != null) hiringButton.clicked -= OpenHiring;
			filterField?.UnregisterValueChangedCallback(OnFilterChanged);
			buildingField?.UnregisterValueChangedCallback(OnBuildingChanged);
			handleField?.UnregisterValueChangedCallback(OnHandleChanged);
			if (createSpawnAreaButton != null) createSpawnAreaButton.clicked -= BeginWorkerSpawnAreaCreation;
		}

		private void BeginWorkerSpawnAreaCreation()
		{
			AreaOverlayController overlay = null;
			if (GameContext.HasInstance && GameContext.Instance.AreaMgr != null)
				GameContext.Instance.AreaMgr.TryGetComponent(out overlay);

			overlay ??= FindAnyObjectByType<AreaOverlayController>(FindObjectsInactive.Include);
			if (overlay == null)
			{
				hiringMessage.text = "Worker Spawn Area placement is unavailable.";
				return;
			}

			window.Close();
			overlay.BeginCreateOneShot(AreaType.WorkerSpawn, 0);
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false) return;
			workerManager = GameContext.Instance.WorkerMgr;
			economyService = GameContext.Instance.EconomyService;
			if (workerManager != null)
			{
				workerManager.OnWorkersChanged += RefreshRoster;
				workerManager.OnWorkerChanged += OnWorkerChanged;
			}
			if (economyService != null) economyService.OnMoneyChanged += OnMoneyChanged;
		}

		private void UnbindServices()
		{
			if (workerManager != null)
			{
				workerManager.OnWorkersChanged -= RefreshRoster;
				workerManager.OnWorkerChanged -= OnWorkerChanged;
			}
			if (economyService != null) economyService.OnMoneyChanged -= OnMoneyChanged;
			workerManager = null;
			economyService = null;
		}

		private void OpenRoster() => SelectTab(true);
		private void OpenHiring() => SelectTab(false);

		private void SelectTab(bool roster)
		{
			rosterTab.style.display = roster ? DisplayStyle.Flex : DisplayStyle.None;
			hiringTab.style.display = roster ? DisplayStyle.None : DisplayStyle.Flex;
			rosterButton.EnableInClassList(SelectedTabClass, roster);
			hiringButton.EnableInClassList(SelectedTabClass, roster == false);
		}

		private void OnFilterChanged(ChangeEvent<string> _) => RefreshRoster();

		private void RefreshRoster()
		{
			if (workerList == null) return;
			IReadOnlyList<AIWorker> workers = workerManager?.Workers;
			int count = workers?.Count ?? 0;
			int unassigned = 0;
			int working = 0;
			for (int i = 0; i < count; ++i)
			{
				AIWorker worker = workers[i];
				if (worker == null) continue;
				if (worker.AssignedTaskTypes.Count == 0) ++unassigned;
				if (worker.EffectiveStatusAction == WorkerStatusAction.Working) ++working;
			}
			totalLabel.text = $"TOTAL {count}";
			unassignedLabel.text = $"UNASSIGNED {unassigned}";
			workingLabel.text = $"WORKING {working}";
			blockedLabel.text = $"BLOCKED {workerManager?.TrafficBlockedCount ?? 0}";
			payrollLabel.text = $"PAYROLL ${workerManager?.CostPerMonth ?? 0:N0} / MONTH";

			workerList.Clear();
			bool selectedVisible = false;
			AIWorker firstVisible = null;
			for (int i = 0; i < count; ++i)
			{
				AIWorker worker = workers[i];
				if (worker == null || MatchesFilter(worker) == false) continue;
				firstVisible ??= worker;
				selectedVisible |= worker == selectedWorker;
				workerList.Add(CreateWorkerRow(worker));
			}
			if (selectedVisible == false) selectedWorker = firstVisible;
			workerListEmpty.style.display = firstVisible == null ? DisplayStyle.Flex : DisplayStyle.None;
			RefreshSelectedWorker();
		}

		private bool MatchesFilter(AIWorker worker)
		{
			return filterField.value switch
			{
				"Human" => worker.WorkerKind == WorkerKind.Human,
				"Robot" => worker.WorkerKind == WorkerKind.Robot,
				"Unassigned" => worker.AssignedTaskTypes.Count == 0,
				_ => true,
			};
		}

		private VisualElement CreateWorkerRow(AIWorker worker)
		{
			TemplateContainer row = rosterRowTemplate.CloneTree();
			VisualElement root = row.Q<VisualElement>(className: "workforce-worker-row");
			row.Q<Label>("worker-row-name").text = $"{worker.Name}  #{worker.WorkerID}";
			row.Q<Label>("worker-row-kind").text = GetWorkerKind(worker);
			row.Q<Label>("worker-row-status").text = worker.EffectiveStatusAction.ToString();
			row.Q<Label>("worker-row-condition").text = GetCondition(worker);
			row.Q<Label>("worker-row-wear").text = GetWear(worker);
			row.Q<Label>("worker-row-building").text = GetBuildingName(worker.PrimaryBuildingId);
			root.EnableInClassList(SelectedRowClass, worker == selectedWorker);
			root.RegisterCallback<ClickEvent>(_ => SelectWorker(worker));
			return row;
		}

		private void SelectWorker(AIWorker worker)
		{
			selectedWorker = worker;
			selectedHandleGroup = GetHandleGroup(worker.AssignedTaskTypes);
			RefreshRoster();
		}

		private void RefreshSelectedWorker()
		{
			bool hasWorker = selectedWorker != null;
			workerDetail.style.display = hasWorker ? DisplayStyle.Flex : DisplayStyle.None;
			workerDetailEmpty.style.display = hasWorker ? DisplayStyle.None : DisplayStyle.Flex;
			if (hasWorker == false) return;

			workerName.text = $"{selectedWorker.Name}  #{selectedWorker.WorkerID}";
			workerKind.text = GetWorkerKind(selectedWorker);
			workerStatus.text = $"STATUS  {selectedWorker.EffectiveStatusAction}";
			workerCondition.text = GetCondition(selectedWorker).ToUpperInvariant();
			workerWear.text = GetWear(selectedWorker).ToUpperInvariant();
			workerPay.text = $"${selectedWorker.MonthlyCost:N0} / MONTH";
			RefreshBuildingField();
			if (selectedWorker.AssignedTaskTypes.Count > 0)
				selectedHandleGroup = GetHandleGroup(selectedWorker.AssignedTaskTypes);
			handleField.SetValueWithoutNotify(HandleGroups[(int)selectedHandleGroup]);
			RefreshTaskToggles();
		}

		private void RefreshBuildingField()
		{
			buildingIds.Clear();
			List<string> choices = new() { "None (Outdoor)" };
			buildingIds.Add(0);
			int selectedIndex = 0;
			BuildingManager manager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
			if (manager != null)
			{
				foreach (Building building in manager.RegisteredBuildings)
				{
					if (building == null) continue;
					if (building.RuntimeBuildingId == selectedWorker.PrimaryBuildingId) selectedIndex = buildingIds.Count;
					buildingIds.Add(building.RuntimeBuildingId);
					choices.Add(building.DisplayName);
				}
			}
			buildingField.choices = choices;
			buildingField.SetValueWithoutNotify(choices[Mathf.Clamp(selectedIndex, 0, choices.Count - 1)]);
		}

		private void OnBuildingChanged(ChangeEvent<string> _)
		{
			if (selectedWorker == null || buildingField.index < 0 || buildingField.index >= buildingIds.Count) return;
			selectedHandleGroup = HandleGroup.Undefined;
			workerManager?.TrySetWorkerPrimaryBuilding(selectedWorker, buildingIds[buildingField.index]);
		}

		private void OnHandleChanged(ChangeEvent<string> _)
		{
			if (selectedWorker == null) return;
			selectedHandleGroup = (HandleGroup)Mathf.Clamp(handleField.index, 0, HandleGroups.Count - 1);
			workerManager?.SetWorkerAssignedTaskTypes(selectedWorker, Array.Empty<WorkerTask.TaskType>());
			RefreshTaskToggles();
		}

		private void RefreshTaskToggles()
		{
			taskToggles.Clear();
			if (selectedWorker == null || selectedHandleGroup == HandleGroup.Undefined) return;
			List<WorkerTask.TaskType> assignable = new();
			WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(selectedWorker, assignable);
			foreach (WorkerTask.TaskType type in assignable)
			{
				if (GetHandleGroup(type) != selectedHandleGroup) continue;
				Toggle toggle = new(GetTaskName(type)) { value = selectedWorker.IsAssignedToTaskType(type) };
				toggle.AddToClassList("workforce-task-toggle");
				toggle.RegisterValueChangedCallback(evt => SetTaskAssigned(type, evt.newValue));
				taskToggles.Add(toggle);
			}
		}

		private void SetTaskAssigned(WorkerTask.TaskType type, bool assigned)
		{
			if (selectedWorker == null) return;
			List<WorkerTask.TaskType> types = new(selectedWorker.AssignedTaskTypes);
			if (assigned && types.Contains(type) == false) types.Add(type);
			if (assigned == false) types.Remove(type);
			workerManager?.SetWorkerAssignedTaskTypes(selectedWorker, types);
		}

		private void GenerateCandidatesOnOpen()
		{
			RefreshCategories();
			if (selectedMarket != null) GenerateCandidates(selectedMarket);
		}

		private void RefreshCategories()
		{
			categoryList.Clear();
			if (ContainsMarket(selectedMarket) == false) selectedMarket = FirstMarket();
			AddMarketButtons(humanMarkets, "Human");
			AddMarketButtons(robotMarkets, "Robot");
		}

		private void AddMarketButtons(IReadOnlyList<WorkforceMarketData_SO> markets, string prefix)
		{
			if (markets == null) return;
			for (int i = 0; i < markets.Count; ++i)
			{
				WorkforceMarketData_SO market = markets[i];
				if (market == null) continue;
				Button button = new(() => SelectMarket(market)) { text = $"{prefix} · {market.WorkForceMarketName}" };
				button.AddToClassList("workforce-category-button");
				button.EnableInClassList(SelectedCategoryClass, market == selectedMarket);
				categoryList.Add(button);
			}
		}

		private void SelectMarket(WorkforceMarketData_SO market)
		{
			selectedMarket = market;
			RefreshCategories();
			GenerateCandidates(market);
		}

		private void GenerateCandidates(WorkforceMarketData_SO market)
		{
			candidates.Clear();
			hiredCandidates.Clear();
			candidateList.Clear();
			if (market == null) return;
			System.Random random = new(unchecked(Environment.TickCount * 397) ^ market.GetHashCode());
			int count = Mathf.Min(CandidateCount, market.GetMaxCount());
			for (int i = 0; i < count; ++i)
			{
				WorkerArchetype candidate = new();
				market.FillWorkerArchetype(candidate, random, 0, i);
				candidates.Add(candidate);
				candidateList.Add(CreateCandidateRow(candidate));
			}
			hiringMessage.text = $"{market.WorkForceMarketName} · {count} candidates";
		}

		private VisualElement CreateCandidateRow(WorkerArchetype candidate)
		{
			TemplateContainer row = candidateRowTemplate.CloneTree();
			WorkerAbilityDefinition ability = candidate.AbilityDefinition;
			WorkerBaseStatDefinition stats = candidate.WorkerBaseStat;
			row.Q<Label>("candidate-name").text = GetCandidateName(candidate);
			row.Q<Label>("candidate-kind").text = ability.WorkerKind == WorkerKind.Robot ? ability.RobotType.ToString() : ability.HumanType.ToString();
			row.Q<Label>("candidate-ability").text = ability.abilities.ToString();
			row.Q<Label>("candidate-stats").text = $"Move {stats.baseMoveSpeedMultiplier:0.00} · Work {stats.baseWorkSpeedMultiplier:0.00}";
			row.Q<Label>("candidate-cost").text = $"${ability.installCost:N0} + ${ability.monthlyCost:N0}/mo";
			Button hireButton = row.Q<Button>("candidate-hire-button");
			hireButton.SetEnabled(hiredCandidates.Contains(candidate) == false && economyService != null &&
				economyService.CanAfford(Mathf.Max(0, ability.installCost)));
			hireButton.clicked += () => Hire(candidate, hireButton);
			return row;
		}

		private void Hire(WorkerArchetype candidate, Button hireButton)
		{
			int cost = Mathf.Max(0, candidate.AbilityDefinition.installCost);
			if (economyService == null || economyService.CanAfford(cost) == false)
			{
				hiringMessage.text = $"Insufficient funds. ${cost:N0} required.";
				return;
			}
			WorkerSpawnManager spawnManager = GameContext.HasInstance ? GameContext.Instance.WorkerSpawnMgr : null;
			hiredCandidates.Add(candidate);
			if (spawnManager == null || spawnManager.TryHireWorker(candidate, this, out AIWorker worker) == false)
			{
				hiredCandidates.Remove(candidate);
				hiringMessage.text = "Hiring failed. Check that a matching Worker Spawn Area has available space.";
				return;
			}
			hireButton.SetEnabled(false);
			hiringMessage.text = $"{worker.Name} hired and placed successfully.";
		}

		private void OnWorkerChanged(AIWorker worker)
		{
			RefreshRoster();
		}

		private void OnMoneyChanged(int _) => RefreshCandidateAffordability();

		private void RefreshCandidateAffordability()
		{
			if (selectedMarket != null) DisplayExistingCandidates();
		}

		private void DisplayExistingCandidates()
		{
			candidateList.Clear();
			foreach (WorkerArchetype candidate in candidates) candidateList.Add(CreateCandidateRow(candidate));
		}

		private bool ContainsMarket(WorkforceMarketData_SO market) => ContainsMarket(humanMarkets, market) || ContainsMarket(robotMarkets, market);
		private static bool ContainsMarket(IReadOnlyList<WorkforceMarketData_SO> markets, WorkforceMarketData_SO market)
		{
			if (market == null || markets == null) return false;
			for (int i = 0; i < markets.Count; ++i) if (markets[i] == market) return true;
			return false;
		}
		private WorkforceMarketData_SO FirstMarket() => FirstMarket(humanMarkets) ?? FirstMarket(robotMarkets);
		private static WorkforceMarketData_SO FirstMarket(IReadOnlyList<WorkforceMarketData_SO> markets)
		{
			if (markets == null) return null;
			for (int i = 0; i < markets.Count; ++i) if (markets[i] != null) return markets[i];
			return null;
		}

		private static string GetCandidateName(WorkerArchetype candidate)
		{
			string name = $"{candidate.WorkerNameDefinition.WorkerFirstName} {candidate.WorkerNameDefinition.WorkerLastName}".Trim();
			return string.IsNullOrWhiteSpace(name) ? "Candidate" : name;
		}

		private static string GetWorkerKind(AIWorker worker) => worker.WorkerKind == WorkerKind.Robot ? $"Robot · {worker.RobotType}" : $"Human · {worker.HumanType}";
		private static string GetCondition(AIWorker worker)
		{
			if (worker is HumanWorker human) return $"Fatigue {human.Fatigue:0}%";
			if (worker is RobotWorker robot) return $"Battery {robot.BatteryLevel:0}%";
			return "Condition --";
		}
		private static string GetWear(AIWorker worker) =>
			worker is RobotWorker robot ? $"Wear {robot.Wear * 100.0f:0.0}%" : "Wear --";
		private static string GetBuildingName(uint id)
		{
			if (id == 0) return "Outdoor";
			return GameContext.HasInstance && GameContext.Instance.BuildingMgr != null && GameContext.Instance.BuildingMgr.TryGetBuilding(id, out Building building) && building != null
				? building.DisplayName : $"Building #{id}";
		}
		private static HandleGroup GetHandleGroup(IReadOnlyList<WorkerTask.TaskType> types) => types != null && types.Count > 0 ? GetHandleGroup(types[0]) : HandleGroup.Undefined;
		private static HandleGroup GetHandleGroup(WorkerTask.TaskType type)
		{
			return type switch
			{
				WorkerTask.TaskType.IB or WorkerTask.TaskType.CapsuleClear or WorkerTask.TaskType.CapsuleSupply or WorkerTask.TaskType.OB or WorkerTask.TaskType.CargoTransfer or WorkerTask.TaskType.Loading or WorkerTask.TaskType.Unloading => HandleGroup.Cargo,
				WorkerTask.TaskType.Picking or WorkerTask.TaskType.Storing or WorkerTask.TaskType.PackingInput or WorkerTask.TaskType.PackingOutput or WorkerTask.TaskType.LaunchSort or WorkerTask.TaskType.Packing or WorkerTask.TaskType.Labeling => HandleGroup.Item,
				_ => HandleGroup.Undefined,
			};
		}
		private static string GetTaskName(WorkerTask.TaskType type) => type switch
		{
			WorkerTask.TaskType.IB => "Capsule Relocation (Inbound)",
			WorkerTask.TaskType.CapsuleClear => "Capsule Relocation (Clear)",
			WorkerTask.TaskType.CapsuleSupply => "Capsule Relocation (Supply)",
			WorkerTask.TaskType.OB => "Capsule Relocation (Outbound)",
			WorkerTask.TaskType.LaunchSort => "Launch Sort",
			_ => type.ToString(),
		};
	}
}
