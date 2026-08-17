using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed partial class WorkforceManagementWindow : MonoBehaviour
	{
		private const string SelectedTabClass = "workforce-tab-button--selected";
		private const string SelectedRowClass = "workforce-worker-row--selected";
		private const string DraggingRowClass = "workforce-worker-row--dragging";
		private const string SelectedCategoryClass = "workforce-category-button--selected";
		private const string AssignmentModeClass = "workforce-assignment-mode-button--active";
		private const string OutdoorDropActiveClass = "workforce-outdoor-drop-zone--active";
		private const float DragThreshold = 8f;
		private const int CandidateCount = 15;
		private static readonly List<string> RosterFilters = new() { "All", "Human", "Robot", "Unassigned" };
		private static readonly List<string> HandleGroups = new() { "Undefined", "Cargo Handle", "Item Handle" };

		private enum HandleGroup { Undefined, Cargo, Item }
		private enum WorkforceTab { Assignments, Workers, Hiring }

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
		private Button assignmentModeButton;
		private Label assignmentStatus;
		private ScrollView workerList;
		private Label workerListEmpty;
		private VisualElement outdoorDropZone;
		private Label detailTitle;
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
		private Label workerAssignmentMessage;
		private VisualElement buildingDetail;
		private Label buildingName;
		private Label buildingSummary;
		private ScrollView buildingMatrix;
		private Label buildingEmpty;
		private ScrollView categoryList;
		private ScrollView candidateList;
		private Label hiringMessage;
		private Button createSpawnAreaButton;
		private readonly List<uint> buildingIds = new();
		private readonly List<WorkerArchetype> candidates = new();
		private readonly HashSet<WorkerArchetype> hiredCandidates = new();
		private WorkforceAssignmentModeController assignmentModeController;
		private WorkerManager workerManager;
		private EconomyService economyService;
		private ResearchService researchService;
		private WorkforceMarketData_SO selectedMarket;
		private AIWorker selectedWorker;
		private Building selectedBuilding;
		private HandleGroup selectedHandleGroup;
		private AIWorker pointerWorker;
		private VisualElement pointerRow;
		private Vector2 pointerStart;
		private int activePointerId = -1;
		private bool workerDragStarted;
		private bool endingWorkerPointer;
		private bool rosterRefreshPending;
		private WorkforceTab selectedTab;
		[System.NonSerialized] private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetRosterRowTemplate, VisualTreeAsset targetCandidateRowTemplate,
			IReadOnlyList<WorkforceMarketData_SO> targetHumanMarkets,
			IReadOnlyList<WorkforceMarketData_SO> targetRobotMarkets,
			WorkforceAssignmentModeController targetAssignmentModeController)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			rosterRowTemplate = targetRosterRowTemplate;
			candidateRowTemplate = targetCandidateRowTemplate;
			humanMarkets = targetHumanMarkets;
			robotMarkets = targetRobotMarkets;
			assignmentModeController = targetAssignmentModeController;
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
			EndWorkerPointer(cancelDrag: true);
			assignmentModeController?.EndMode();
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
			RefreshAssignments();
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
			assignmentModeButton = content.Q<Button>("workforce-assignment-mode-button");
			assignmentStatus = content.Q<Label>("workforce-assignment-status");
			workerList = content.Q<ScrollView>("worker-list");
			workerListEmpty = content.Q<Label>("worker-list-empty");
			outdoorDropZone = content.Q<VisualElement>("workforce-outdoor-drop-zone");
			detailTitle = content.Q<Label>("workforce-detail-title");
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
			workerAssignmentMessage = content.Q<Label>("worker-assignment-message");
			buildingDetail = content.Q<VisualElement>("workforce-building-detail");
			buildingName = content.Q<Label>("workforce-building-name");
			buildingSummary = content.Q<Label>("workforce-building-summary");
			buildingMatrix = content.Q<ScrollView>("workforce-building-matrix");
			buildingEmpty = content.Q<Label>("workforce-building-empty");
			categoryList = content.Q<ScrollView>("workforce-category-list");
			candidateList = content.Q<ScrollView>("workforce-candidate-list");
			hiringMessage = content.Q<Label>("workforce-hiring-message");
			createSpawnAreaButton = content.Q<Button>("create-worker-spawn-area-button");
			InitializeAssignmentsView(content);

			if (HasRequiredAssignmentsView() == false ||
				rosterButton == null || hiringButton == null || rosterTab == null || hiringTab == null ||
				totalLabel == null || filterField == null || workerList == null || workerDetail == null ||
				buildingField == null || handleField == null || taskToggles == null || categoryList == null ||
				candidateList == null || hiringMessage == null || createSpawnAreaButton == null ||
				assignmentModeButton == null || assignmentStatus == null || outdoorDropZone == null ||
				detailTitle == null || workerAssignmentMessage == null || buildingDetail == null ||
				buildingName == null || buildingSummary == null || buildingMatrix == null || buildingEmpty == null)
			{
				Debug.LogError("[WorkforceManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Workforce Management");
			window.SetContent(content);
			assignmentsButton.clicked += OpenAssignments;
			rosterButton.clicked += OpenRoster;
			hiringButton.clicked += OpenHiring;
			filterField.choices = new List<string>(RosterFilters);
			filterField.SetValueWithoutNotify(RosterFilters[0]);
			filterField.RegisterValueChangedCallback(OnFilterChanged);
			assignmentModeButton.clicked += ToggleAssignmentMode;
			workerList.RegisterCallback<PointerMoveEvent>(OnWorkerListPointerMove);
			workerList.RegisterCallback<PointerUpEvent>(OnWorkerListPointerUp);
			workerList.RegisterCallback<PointerCancelEvent>(OnWorkerListPointerCancel);
			workerList.RegisterCallback<PointerCaptureOutEvent>(OnWorkerListPointerCaptureOut);
			buildingField.RegisterValueChangedCallback(OnBuildingChanged);
			handleField.choices = new List<string>(HandleGroups);
			handleField.RegisterValueChangedCallback(OnHandleChanged);
			createSpawnAreaButton.clicked += BeginWorkerSpawnAreaCreation;
			window.Closed += OnWindowClosed;
			if (assignmentModeController != null)
			{
				assignmentModeController.StateChanged += OnAssignmentModeStateChanged;
				assignmentModeController.BuildingSelected += OnAssignmentBuildingSelected;
				assignmentModeController.WorkerDropped += OnAssignmentWorkerDropped;
			}
			initialized = true;
			SelectTab(WorkforceTab.Assignments);
			RefreshAssignmentModeState();
			return true;
		}

		private void UnbindControls()
		{
			if (assignmentsButton != null) assignmentsButton.clicked -= OpenAssignments;
			if (rosterButton != null) rosterButton.clicked -= OpenRoster;
			if (hiringButton != null) hiringButton.clicked -= OpenHiring;
			filterField?.UnregisterValueChangedCallback(OnFilterChanged);
			if (assignmentModeButton != null) assignmentModeButton.clicked -= ToggleAssignmentMode;
			if (workerList != null)
			{
				workerList.UnregisterCallback<PointerMoveEvent>(OnWorkerListPointerMove);
				workerList.UnregisterCallback<PointerUpEvent>(OnWorkerListPointerUp);
				workerList.UnregisterCallback<PointerCancelEvent>(OnWorkerListPointerCancel);
				workerList.UnregisterCallback<PointerCaptureOutEvent>(OnWorkerListPointerCaptureOut);
			}
			buildingField?.UnregisterValueChangedCallback(OnBuildingChanged);
			handleField?.UnregisterValueChangedCallback(OnHandleChanged);
			if (createSpawnAreaButton != null) createSpawnAreaButton.clicked -= BeginWorkerSpawnAreaCreation;
			if (window != null) window.Closed -= OnWindowClosed;
			if (assignmentModeController != null)
			{
				assignmentModeController.StateChanged -= OnAssignmentModeStateChanged;
				assignmentModeController.BuildingSelected -= OnAssignmentBuildingSelected;
				assignmentModeController.WorkerDropped -= OnAssignmentWorkerDropped;
			}
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
			buildingManager = GameContext.Instance.BuildingMgr;
			economyService = GameContext.Instance.EconomyService;
			researchService = GameContext.Instance.ResearchService;
			if (workerManager != null)
			{
				workerManager.OnWorkersChanged += OnWorkersChanged;
				workerManager.OnWorkerChanged += OnWorkerChanged;
			}
			if (buildingManager != null) buildingManager.OnBuildingsChanged += OnBuildingsChanged;
			if (economyService != null) economyService.OnMoneyChanged += OnMoneyChanged;
			if (researchService != null) researchService.OnResearchStateChanged += OnResearchStateChanged;
		}

		private void UnbindServices()
		{
			if (workerManager != null)
			{
				workerManager.OnWorkersChanged -= OnWorkersChanged;
				workerManager.OnWorkerChanged -= OnWorkerChanged;
			}
			if (buildingManager != null) buildingManager.OnBuildingsChanged -= OnBuildingsChanged;
			if (economyService != null) economyService.OnMoneyChanged -= OnMoneyChanged;
			if (researchService != null) researchService.OnResearchStateChanged -= OnResearchStateChanged;
			workerManager = null;
			buildingManager = null;
			economyService = null;
			researchService = null;
		}

		private void OpenAssignments() => SelectTab(WorkforceTab.Assignments);
		private void OpenRoster() => SelectTab(WorkforceTab.Workers);
		private void OpenHiring() => SelectTab(WorkforceTab.Hiring);

		private void SelectTab(WorkforceTab tab)
		{
			selectedTab = tab;
			bool assignments = tab == WorkforceTab.Assignments;
			bool workers = tab == WorkforceTab.Workers;
			assignmentsTab.style.display = assignments ? DisplayStyle.Flex : DisplayStyle.None;
			rosterTab.style.display = workers ? DisplayStyle.Flex : DisplayStyle.None;
			hiringTab.style.display = tab == WorkforceTab.Hiring ? DisplayStyle.Flex : DisplayStyle.None;
			assignmentsButton.EnableInClassList(SelectedTabClass, assignments);
			rosterButton.EnableInClassList(SelectedTabClass, workers);
			hiringButton.EnableInClassList(SelectedTabClass, tab == WorkforceTab.Hiring);
			if (assignments)
			{
				if (assignmentsRefreshPending || assignmentTree.contentContainer.childCount == 0)
					RefreshAssignments();
			}
			else if (workers)
				RefreshRoster();
		}

		private void OnFilterChanged(ChangeEvent<string> _) => RefreshRoster();

		private void RefreshRoster()
		{
			if (workerList == null) return;
			if (activePointerId >= 0)
			{
				rosterRefreshPending = true;
				return;
			}

			rosterRefreshPending = false;
			IReadOnlyList<AIWorker> workers = workerManager?.Workers;
			int count = workers?.Count ?? 0;
			int unassigned = 0;
			int working = 0;
			for (int i = 0; i < count; ++i)
			{
				AIWorker worker = workers[i];
				if (worker == null) continue;
				if (worker.IsOperational && worker.AssignedTaskTypes.Count == 0) ++unassigned;
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
				"Unassigned" => worker.IsOperational && worker.AssignedTaskTypes.Count == 0,
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
			row.Q<Label>("worker-row-building").text = GetWorkerBuildingDisplay(worker);
			root.EnableInClassList(SelectedRowClass, worker == selectedWorker);
			root.RegisterCallback<PointerDownEvent>(evt => OnWorkerRowPointerDown(evt, root, worker));
			return row;
		}

		private void SelectWorker(AIWorker worker)
		{
			selectedWorker = worker;
			selectedBuilding = null;
			selectedHandleGroup = GetHandleGroup(worker.AssignedTaskTypes);
			RefreshRoster();
		}

		private void RefreshSelectedWorker()
		{
			if (selectedBuilding != null)
			{
				detailTitle.text = "BUILDING STAFFING";
				workerDetail.style.display = DisplayStyle.None;
				workerDetailEmpty.style.display = DisplayStyle.None;
				buildingDetail.style.display = DisplayStyle.Flex;
				RefreshBuildingDetail();
				return;
			}

			detailTitle.text = "SELECTED WORKER";
			buildingDetail.style.display = DisplayStyle.None;
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
			buildingField.SetEnabled(selectedWorker.IsOperational);
			handleField.SetEnabled(selectedWorker.IsOperational);
			RefreshBuildingField();
			IReadOnlyList<WorkerTask.TaskType> editingTaskTypes = GetEditingTaskTypes(selectedWorker);
			if (editingTaskTypes.Count > 0)
				selectedHandleGroup = GetHandleGroup(editingTaskTypes);
			else if (selectedHandleGroup == HandleGroup.Undefined)
				selectedHandleGroup = GetFirstAssignableHandleGroup(selectedWorker, GetEditingBuildingType(selectedWorker));
			handleField.SetValueWithoutNotify(HandleGroups[(int)selectedHandleGroup]);
			workerAssignmentMessage.text = BuildWorkerAssignmentMessage(selectedWorker);
			RefreshTaskToggles();
		}

		private void RefreshBuildingField()
		{
			buildingIds.Clear();
			List<string> choices = new() { "None (Outdoor)" };
			buildingIds.Add(0);
			int selectedIndex = 0;
			uint editingBuildingId = GetEditingBuildingId(selectedWorker);
			BuildingManager manager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
			if (manager != null)
			{
				foreach (Building building in manager.RegisteredBuildings)
				{
					if (building == null) continue;
					if (building.RuntimeBuildingId == editingBuildingId) selectedIndex = buildingIds.Count;
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
			uint buildingId = buildingIds[buildingField.index];
			BuildingType? buildingType = ResolveBuildingType(buildingId);
			List<WorkerTask.TaskType> compatibleTypes = BuildCompatibleTaskTypes(
				selectedWorker,
				GetEditingTaskTypes(selectedWorker),
				buildingType);
			if (workerManager?.TryRequestWorkerAssignment(selectedWorker, buildingId, compatibleTypes) != true)
				return;

			selectedHandleGroup = compatibleTypes.Count > 0
				? GetHandleGroup(compatibleTypes)
				: GetFirstAssignableHandleGroup(selectedWorker, buildingType);
			RefreshRoster();
		}

		private void OnHandleChanged(ChangeEvent<string> _)
		{
			if (selectedWorker == null) return;
			selectedHandleGroup = (HandleGroup)Mathf.Clamp(handleField.index, 0, HandleGroups.Count - 1);
			RefreshTaskToggles();
		}

		private void RefreshTaskToggles()
		{
			taskToggles.Clear();
			if (selectedWorker == null || selectedHandleGroup == HandleGroup.Undefined) return;
			List<WorkerTask.TaskType> assignable = new();
			WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(selectedWorker, GetEditingBuildingType(selectedWorker), assignable);
			IReadOnlyList<WorkerTask.TaskType> editingTaskTypes = GetEditingTaskTypes(selectedWorker);
			foreach (WorkerTask.TaskType type in assignable)
			{
				if (GetHandleGroup(type) != selectedHandleGroup) continue;
				Toggle toggle = new(GetTaskName(type)) { value = ContainsTaskType(editingTaskTypes, type) };
				toggle.AddToClassList("workforce-task-toggle");
				toggle.SetEnabled(selectedWorker.IsOperational);
				toggle.RegisterValueChangedCallback(evt => SetTaskAssigned(type, evt.newValue));
				taskToggles.Add(toggle);
			}
		}

		private void SetTaskAssigned(WorkerTask.TaskType type, bool assigned)
		{
			if (selectedWorker == null) return;
			List<WorkerTask.TaskType> types = new(GetEditingTaskTypes(selectedWorker));
			if (assigned && types.Contains(type) == false) types.Add(type);
			if (assigned == false) types.Remove(type);
			SortTaskTypes(types);
			workerManager?.TryRequestWorkerAssignment(selectedWorker, GetEditingBuildingId(selectedWorker), types);
		}

		private void ToggleAssignmentMode()
		{
			if (assignmentModeController == null)
				return;

			if (assignmentModeController.IsPersistentMode)
				assignmentModeController.EndMode();
			else
				assignmentModeController.BeginPersistentMode();
		}

		private void OnWindowClosed()
		{
			EndWorkerPointer(cancelDrag: true);
			assignmentModeController?.EndMode();
		}

		private void OnAssignmentModeStateChanged()
		{
			RefreshAssignmentModeState();
		}

		private void OnAssignmentBuildingSelected(Building building)
		{
			if (building == null)
				return;

			selectedBuilding = building;
			RefreshSelectedWorker();
		}

		private void OnAssignmentWorkerDropped(AIWorker worker, Building building, bool assigned)
		{
			if (worker == null)
				return;

			selectedWorker = worker;
			selectedBuilding = null;
			selectedHandleGroup = GetEditingTaskTypes(worker).Count > 0
				? GetHandleGroup(GetEditingTaskTypes(worker))
				: GetFirstAssignableHandleGroup(worker, building != null ? building.Type : null);
			if (assigned)
				RefreshRoster();
			else
				RefreshSelectedWorker();
		}

		private void RefreshAssignmentModeState()
		{
			if (assignmentModeButton == null || assignmentStatus == null || outdoorDropZone == null)
				return;

			bool persistent = assignmentModeController?.IsPersistentMode == true;
			bool dragging = assignmentModeController?.IsDraggingWorker == true;
			assignmentModeButton.EnableInClassList(AssignmentModeClass, persistent);
			assignmentModeButton.text = persistent ? "Finish Assigning" : "Assign on Map";
			assignmentStatus.text = assignmentModeController?.StatusText ?? string.Empty;
			outdoorDropZone.EnableInClassList(
				OutdoorDropActiveClass,
				dragging && assignmentModeController.CanAssignToOutdoor(assignmentModeController.DraggedWorker));
		}

		private void OnWorkerRowPointerDown(PointerDownEvent evt, VisualElement row, AIWorker worker)
		{
			if (evt.button != 0 || worker == null || activePointerId >= 0 || workerList == null)
				return;

			pointerWorker = worker;
			pointerRow = row;
			pointerStart = evt.position;
			activePointerId = evt.pointerId;
			workerDragStarted = false;
			rosterRefreshPending = false;
			workerList.CapturePointer(activePointerId);
			evt.StopPropagation();
		}

		private void OnWorkerListPointerMove(PointerMoveEvent evt)
		{
			if (evt.pointerId != activePointerId || pointerWorker == null)
				return;

			if (workerDragStarted == false)
			{
				Vector2 current = evt.position;
				if ((current - pointerStart).sqrMagnitude < DragThreshold * DragThreshold)
					return;

				if (assignmentModeController?.BeginWorkerDrag(pointerWorker) != true)
				{
					EndWorkerPointer(cancelDrag: true);
					return;
				}

				workerDragStarted = true;
				pointerRow?.EnableInClassList(DraggingRowClass, true);
			}

			assignmentModeController?.UpdateDragPointer(Input.mousePosition);
			evt.StopPropagation();
		}

		private void OnWorkerListPointerUp(PointerUpEvent evt)
		{
			if (evt.pointerId != activePointerId)
				return;

			AIWorker worker = pointerWorker;
			bool wasDragging = workerDragStarted;
			VisualElement hit = workerList.panel?.Pick(evt.position);
			bool outdoorDrop = hit != null && (hit == outdoorDropZone || outdoorDropZone.Contains(hit));
			bool overUi = hit != null;
			ReleaseWorkerPointer();

			if (wasDragging)
			{
				assignmentModeController?.UpdateDragPointer(Input.mousePosition);
				if (outdoorDrop)
					assignmentModeController?.TryDropDraggedWorkerToOutdoor();
				else if (overUi == false)
					assignmentModeController?.TryDropDraggedWorker();
				else
					assignmentModeController?.CancelWorkerDrag();
			}
			else if (worker != null)
			{
				SelectWorker(worker);
			}

			FlushPendingRosterRefresh();
			evt.StopPropagation();
		}

		private void OnWorkerListPointerCancel(PointerCancelEvent evt)
		{
			if (evt.pointerId == activePointerId)
				EndWorkerPointer(cancelDrag: true);
		}

		private void OnWorkerListPointerCaptureOut(PointerCaptureOutEvent evt)
		{
			if (endingWorkerPointer || evt.pointerId != activePointerId)
				return;

			EndWorkerPointer(cancelDrag: true);
		}

		private void EndWorkerPointer(bool cancelDrag)
		{
			if (activePointerId < 0)
				return;

			bool wasDragging = workerDragStarted;
			ReleaseWorkerPointer();
			if (cancelDrag && wasDragging)
				assignmentModeController?.CancelWorkerDrag();
			FlushPendingRosterRefresh();
		}

		private void ReleaseWorkerPointer()
		{
			if (activePointerId < 0)
				return;

			int pointerId = activePointerId;
			VisualElement row = pointerRow;
			activePointerId = -1;
			pointerWorker = null;
			pointerRow = null;
			workerDragStarted = false;
			row?.EnableInClassList(DraggingRowClass, false);

			endingWorkerPointer = true;
			if (workerList != null && workerList.HasPointerCapture(pointerId))
				workerList.ReleasePointer(pointerId);
			endingWorkerPointer = false;
		}

		private void FlushPendingRosterRefresh()
		{
			if (rosterRefreshPending == false || activePointerId >= 0)
				return;

			rosterRefreshPending = false;
			RefreshRoster();
		}

		private void RefreshBuildingDetail()
		{
			Building building = selectedBuilding;
			if (building == null)
				return;

			buildingName.text = building.DisplayName;
			List<WorkerTask.TaskType> taskTypes = GetBuildingTaskTypes(building.Type);
			int currentCount = 0;
			int plannedCount = 0;
			IReadOnlyList<AIWorker> workers = workerManager?.Workers;
			if (workers != null)
			{
				for (int i = 0; i < workers.Count; ++i)
				{
					AIWorker worker = workers[i];
					if (worker == null)
						continue;

					if (worker.PrimaryBuildingId == building.RuntimeBuildingId)
						++currentCount;
					if (GetEditingBuildingId(worker) == building.RuntimeBuildingId)
						++plannedCount;
				}
			}

			buildingSummary.text = currentCount == plannedCount
				? $"WORKERS {currentCount}"
				: $"WORKERS {currentCount}  ·  PLANNED {plannedCount}";
			buildingMatrix.Clear();
			buildingMatrix.Add(CreateBuildingMatrixHeader(building, taskTypes, workers));

			int rowCount = 0;
			if (workers != null)
			{
				for (int i = 0; i < workers.Count; ++i)
				{
					AIWorker worker = workers[i];
					if (worker == null ||
						(worker.PrimaryBuildingId != building.RuntimeBuildingId &&
							(worker.HasPendingAssignment == false || worker.PendingPrimaryBuildingId != building.RuntimeBuildingId)))
					{
						continue;
					}

					buildingMatrix.Add(CreateBuildingWorkerRow(building, worker, taskTypes));
					++rowCount;
				}
			}

			buildingEmpty.style.display = rowCount > 0 ? DisplayStyle.None : DisplayStyle.Flex;
		}

		private VisualElement CreateBuildingMatrixHeader(
			Building building,
			IReadOnlyList<WorkerTask.TaskType> taskTypes,
			IReadOnlyList<AIWorker> workers)
		{
			VisualElement header = new();
			header.AddToClassList("workforce-building-matrix__header");
			Label identity = new("WORKER");
			identity.AddToClassList("workforce-building-matrix__identity");
			identity.AddToClassList("workforce-building-matrix__identity-header");
			header.Add(identity);

			for (int i = 0; i < taskTypes.Count; ++i)
			{
				WorkerTask.TaskType taskType = taskTypes[i];
				int count = CountPlannedWorkers(building.RuntimeBuildingId, taskType, workers);
				Label label = new($"{GetTaskName(taskType)}\n{count}");
				label.AddToClassList("workforce-building-matrix__task-header");
				header.Add(label);
			}

			return header;
		}

		private VisualElement CreateBuildingWorkerRow(
			Building building,
			AIWorker worker,
			IReadOnlyList<WorkerTask.TaskType> taskTypes)
		{
			VisualElement row = new();
			row.AddToClassList("workforce-building-matrix__row");

			VisualElement identity = new();
			identity.AddToClassList("workforce-building-matrix__identity");
			Label name = new($"{worker.Name}  #{worker.WorkerID}");
			name.AddToClassList("workforce-building-matrix__name");
			Label state = new(GetBuildingWorkerState(worker, building.RuntimeBuildingId));
			state.AddToClassList("workforce-building-matrix__state");
			identity.Add(name);
			identity.Add(state);
			row.Add(identity);

			bool leaving = worker.HasPendingAssignment &&
				worker.PrimaryBuildingId == building.RuntimeBuildingId &&
				worker.PendingPrimaryBuildingId != building.RuntimeBuildingId;
			IReadOnlyList<WorkerTask.TaskType> editingTypes =
				worker.HasPendingAssignment && worker.PendingPrimaryBuildingId == building.RuntimeBuildingId
					? worker.PendingAssignedTaskTypes
					: worker.AssignedTaskTypes;
			for (int i = 0; i < taskTypes.Count; ++i)
			{
				WorkerTask.TaskType taskType = taskTypes[i];
				Toggle toggle = new()
				{
					value = ContainsTaskType(editingTypes, taskType),
				};
				toggle.AddToClassList("workforce-building-matrix__task");
				toggle.SetEnabled(
					leaving == false &&
					worker.IsOperational &&
					WorkerTaskAssignmentPolicy.CanAssign(worker, building.Type, taskType));
				toggle.RegisterValueChangedCallback(evt =>
					SetBuildingTaskAssigned(building, worker, taskType, evt.newValue));
				row.Add(toggle);
			}

			return row;
		}

		private void SetBuildingTaskAssigned(
			Building building,
			AIWorker worker,
			WorkerTask.TaskType taskType,
			bool assigned)
		{
			if (building == null || worker == null)
				return;

			IReadOnlyList<WorkerTask.TaskType> sourceTypes =
				worker.HasPendingAssignment && worker.PendingPrimaryBuildingId == building.RuntimeBuildingId
					? worker.PendingAssignedTaskTypes
					: worker.AssignedTaskTypes;
			List<WorkerTask.TaskType> types = new(sourceTypes);
			if (assigned && types.Contains(taskType) == false)
				types.Add(taskType);
			else if (assigned == false)
				types.Remove(taskType);
			SortTaskTypes(types);
			workerManager?.TryRequestWorkerAssignment(worker, building.RuntimeBuildingId, types);
		}

		private static int CountPlannedWorkers(
			uint buildingId,
			WorkerTask.TaskType taskType,
			IReadOnlyList<AIWorker> workers)
		{
			if (workers == null)
				return 0;

			int count = 0;
			for (int i = 0; i < workers.Count; ++i)
			{
				AIWorker worker = workers[i];
				if (worker == null || GetEditingBuildingId(worker) != buildingId)
					continue;

				if (ContainsTaskType(GetEditingTaskTypes(worker), taskType))
					++count;
			}

			return count;
		}

		private void GenerateCandidatesOnOpen()
		{
			RefreshCategories();
			if (selectedMarket != null) GenerateCandidates(selectedMarket);
		}

		private void RefreshCategories()
		{
			categoryList.Clear();
			if (ContainsMarket(selectedMarket) == false || IsMarketUnlocked(selectedMarket) == false)
				selectedMarket = FirstUnlockedMarket();
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
				bool unlocked = IsMarketUnlocked(market);
				string suffix = unlocked ? string.Empty : $" · Requires {GetResearchName(market.RequiredResearchUid)}";
				Button button = new(() => SelectMarket(market)) { text = $"{prefix} · {market.WorkForceMarketName}{suffix}" };
				button.AddToClassList("workforce-category-button");
				button.EnableInClassList(SelectedCategoryClass, market == selectedMarket);
				button.SetEnabled(unlocked);
				categoryList.Add(button);
			}
		}

		private void SelectMarket(WorkforceMarketData_SO market)
		{
			if (IsMarketUnlocked(market) == false)
			{
				hiringMessage.text = $"Required research: {GetResearchName(market?.RequiredResearchUid)}.";
				return;
			}

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
			if (IsMarketUnlocked(market) == false)
			{
				hiringMessage.text = $"Required research: {GetResearchName(market.RequiredResearchUid)}.";
				return;
			}
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
			hireButton.SetEnabled(IsMarketUnlocked(selectedMarket) &&
				hiredCandidates.Contains(candidate) == false && economyService != null &&
				economyService.CanAfford(Mathf.Max(0, ability.installCost)));
			hireButton.clicked += () => Hire(candidate, hireButton);
			return row;
		}

		private void Hire(WorkerArchetype candidate, Button hireButton)
		{
			if (IsMarketUnlocked(selectedMarket) == false)
			{
				hiringMessage.text = $"Required research: {GetResearchName(selectedMarket?.RequiredResearchUid)}.";
				return;
			}

			int cost = Mathf.Max(0, candidate.AbilityDefinition.installCost);
			if (economyService == null || economyService.CanAfford(cost) == false)
			{
				hiringMessage.text = $"Insufficient funds. ${cost:N0} required.";
				return;
			}
			WorkerSpawnManager spawnManager = GameContext.HasInstance ? GameContext.Instance.WorkerSpawnMgr : null;
			hiredCandidates.Add(candidate);
			if (spawnManager == null || spawnManager.TryHireWorker(candidate, selectedMarket, this, out AIWorker worker) == false)
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
			assignmentModeController?.Refresh();
			RequestAssignmentsRefresh();
			RefreshRoster();
		}

		private void OnWorkersChanged()
		{
			assignmentModeController?.Refresh();
			RequestAssignmentsRefresh();
			RefreshRoster();
		}

		private void OnMoneyChanged(int _) => RefreshCandidateAffordability();

		private void OnResearchStateChanged()
		{
			if (initialized)
				GenerateCandidatesOnOpen();
		}

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
		private WorkforceMarketData_SO FirstUnlockedMarket() =>
			FirstUnlockedMarket(humanMarkets) ?? FirstUnlockedMarket(robotMarkets);

		private WorkforceMarketData_SO FirstUnlockedMarket(IReadOnlyList<WorkforceMarketData_SO> markets)
		{
			if (markets == null) return null;
			for (int i = 0; i < markets.Count; ++i)
			{
				if (IsMarketUnlocked(markets[i])) return markets[i];
			}
			return null;
		}

		private bool IsMarketUnlocked(WorkforceMarketData_SO market)
		{
			return market != null &&
				(market.RequiresResearch == false || researchService?.IsResearched(market.RequiredResearchUid) == true);
		}

		private string GetResearchName(string researchUid)
		{
			if (researchService?.Catalog?.TryGet(researchUid, out ResearchDefinition definition) == true)
				return definition.DisplayName;

			return string.IsNullOrWhiteSpace(researchUid) ? "Unknown Research" : researchUid;
		}
		private static uint GetEditingBuildingId(AIWorker worker)
		{
			if (worker == null)
				return 0;

			return worker.HasPendingAssignment ? worker.PendingPrimaryBuildingId : worker.PrimaryBuildingId;
		}

		private static IReadOnlyList<WorkerTask.TaskType> GetEditingTaskTypes(AIWorker worker)
		{
			if (worker == null)
				return Array.Empty<WorkerTask.TaskType>();

			return worker.HasPendingAssignment ? worker.PendingAssignedTaskTypes : worker.AssignedTaskTypes;
		}

		private static bool ContainsTaskType(
			IReadOnlyList<WorkerTask.TaskType> taskTypes,
			WorkerTask.TaskType taskType)
		{
			if (taskTypes == null)
				return false;

			for (int i = 0; i < taskTypes.Count; ++i)
			{
				if (taskTypes[i] == taskType)
					return true;
			}

			return false;
		}

		private static void SortTaskTypes(List<WorkerTask.TaskType> taskTypes)
		{
			taskTypes?.Sort((left, right) => ((int)left).CompareTo((int)right));
		}

		private static List<WorkerTask.TaskType> BuildCompatibleTaskTypes(
			AIWorker worker,
			IReadOnlyList<WorkerTask.TaskType> sourceTypes,
			BuildingType? buildingType)
		{
			List<WorkerTask.TaskType> results = new();
			if (worker == null || sourceTypes == null)
				return results;

			for (int i = 0; i < sourceTypes.Count; ++i)
			{
				WorkerTask.TaskType taskType = sourceTypes[i];
				if (WorkerTaskAssignmentPolicy.CanAssign(worker, buildingType, taskType))
					results.Add(taskType);
			}

			SortTaskTypes(results);
			return results;
		}

		private static List<WorkerTask.TaskType> GetBuildingTaskTypes(BuildingType buildingType)
		{
			List<WorkerTask.TaskType> results = new();
			foreach (WorkerTask.TaskType taskType in Enum.GetValues(typeof(WorkerTask.TaskType)))
			{
				if (taskType == WorkerTask.TaskType.Undefined ||
					taskType == WorkerTask.TaskType.HandleMistake ||
					WorkerTaskAssignmentPolicy.IsTaskTypeAllowedForBuilding(buildingType, taskType) == false)
				{
					continue;
				}

				results.Add(taskType);
			}

			SortTaskTypes(results);
			return results;
		}

		private static HandleGroup GetFirstAssignableHandleGroup(AIWorker worker, BuildingType? buildingType)
		{
			List<WorkerTask.TaskType> taskTypes = new();
			WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(worker, buildingType, taskTypes);
			for (int i = 0; i < taskTypes.Count; ++i)
			{
				HandleGroup group = GetHandleGroup(taskTypes[i]);
				if (group != HandleGroup.Undefined)
					return group;
			}

			return HandleGroup.Undefined;
		}

		private BuildingType? GetEditingBuildingType(AIWorker worker)
		{
			return ResolveBuildingType(GetEditingBuildingId(worker));
		}

		private static BuildingType? ResolveBuildingType(uint buildingId)
		{
			if (buildingId == 0)
				return null;

			BuildingManager manager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
			return manager != null &&
				manager.TryGetBuilding(buildingId, out Building building) &&
				building != null
					? building.Type
					: null;
		}

		private static string GetWorkerBuildingDisplay(AIWorker worker)
		{
			if (worker == null)
				return "Unavailable";

			string current = GetBuildingName(worker.PrimaryBuildingId);
			if (worker.HasPendingAssignment == false)
				return current;

			return $"{current} -> {GetBuildingName(worker.PendingPrimaryBuildingId)}";
		}

		private static string BuildWorkerAssignmentMessage(AIWorker worker)
		{
			if (worker == null)
				return string.Empty;

			IReadOnlyList<WorkerTask.TaskType> taskTypes = GetEditingTaskTypes(worker);
			if (worker.HasPendingAssignment)
			{
				string suffix = taskTypes.Count > 0
					? "You can edit the scheduled tasks below."
					: "No scheduled tasks selected. Choose at least one below.";
				return $"Scheduled for when the current task ends. {suffix}";
			}

			if (taskTypes.Count == 0)
				return "No allowed tasks selected. This worker will remain idle.";

			if (worker.IsAssignedToPackingStation)
				return "Station dedicated. Other allowed tasks wait until the Packing Station assignment is released.";

			return string.Empty;
		}

		private static string GetBuildingWorkerState(AIWorker worker, uint buildingId)
		{
			if (worker == null)
				return "Unavailable";

			if (worker.HasPendingAssignment)
			{
				if (worker.PrimaryBuildingId != buildingId && worker.PendingPrimaryBuildingId == buildingId)
					return $"SCHEDULED IN · {GetTaskState(worker)}";
				if (worker.PrimaryBuildingId == buildingId && worker.PendingPrimaryBuildingId != buildingId)
					return $"SCHEDULED OUT · {GetTaskState(worker)}";
				if (worker.PendingPrimaryBuildingId == buildingId)
					return $"TASK CHANGE SCHEDULED · {GetTaskState(worker)}";
			}

			return worker.IsAssignedToPackingStation
				? $"STATION DEDICATED · {GetTaskState(worker)}"
				: GetTaskState(worker);
		}

		private static string GetTaskState(AIWorker worker)
		{
			return worker?.CurrentTask != null
				? $"NOW {GetTaskName(worker.CurrentTask.Type)}"
				: worker?.EffectiveStatusAction.ToString().ToUpperInvariant() ?? "UNAVAILABLE";
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
				WorkerTask.TaskType.Picking or WorkerTask.TaskType.Storing or WorkerTask.TaskType.PackingInput or WorkerTask.TaskType.PackingOutput or WorkerTask.TaskType.LaunchSort or WorkerTask.TaskType.Packing or WorkerTask.TaskType.Labeling or WorkerTask.TaskType.WasteCollection => HandleGroup.Item,
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
			WorkerTask.TaskType.WasteCollection => "Waste Collection",
			_ => type.ToString(),
		};
	}
}
