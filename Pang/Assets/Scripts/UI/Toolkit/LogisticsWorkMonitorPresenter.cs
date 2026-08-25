using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class LogisticsWorkMonitorPresenter : IDisposable
	{
		private const string ReturnedWarningClass = "workflow-monitor-value--warning";
		private const string BlockedDangerClass = "workflow-monitor-value--danger";
		private const string AllBuildingsLabel = "All Buildings";
		private const string HubUnassignedLabel = "Hub / Unassigned";

		private readonly struct RowDefinition
		{
			public readonly LogisticsWorkCategory Category;
			public readonly string ElementPrefix;
			public readonly bool ShowsItemQuantity;

			public RowDefinition(
				LogisticsWorkCategory category,
				string elementPrefix,
				bool showsItemQuantity = true)
			{
				Category = category;
				ElementPrefix = elementPrefix;
				ShowsItemQuantity = showsItemQuantity;
			}
		}

		private sealed class RowBinding
		{
			public Label Demand;
			public Label Items;
			public Label Waiting;
			public Label Ready;
			public Label Returned;
			public Label Active;
			public Label Blocked;

			public bool IsValid =>
				Demand != null && Items != null && Waiting != null && Ready != null && Returned != null &&
				Active != null && Blocked != null;
		}

		private static readonly RowDefinition[] RowDefinitions =
		{
			new(LogisticsWorkCategory.Labeling, "labeling"),
			new(LogisticsWorkCategory.Storing, "storing"),
			new(LogisticsWorkCategory.Picking, "picking"),
			new(LogisticsWorkCategory.PackingInput, "packing-input"),
			new(LogisticsWorkCategory.Packing, "packing"),
			new(LogisticsWorkCategory.PackingOutput, "packing-output"),
			new(LogisticsWorkCategory.LaunchSort, "launch-sort"),
			new(LogisticsWorkCategory.CapsuleRelocate, "capsule-relocate", showsItemQuantity: false),
		};

		private readonly RowBinding[] rows =
		{
			new(), new(), new(), new(), new(), new(), new(), new(),
		};
		private readonly List<uint?> scopeBuildingIds = new();

		private VisualElement monitorRoot;
		private DropdownField buildingScopeField;
		private MetricsService metricsService;
		private TaskManager taskManager;
		private WorkerManager workerManager;
		private OrderManager orderManager;
		private BuildingManager buildingManager;
		private CapsuleDockService capsuleDockService;
		private GameTime gameTime;
		private uint? selectedBuildingId;
		private bool active;

		public bool BindView(VisualElement documentRoot)
		{
			UnbindView();
			if (documentRoot == null)
				return false;

			monitorRoot = documentRoot.Q<VisualElement>("workflow-monitor-tab");
			buildingScopeField = documentRoot.Q<DropdownField>("workflow-monitor-building-scope");
			for (int i = 0; i < RowDefinitions.Length; ++i)
			{
				string prefix = $"workflow-monitor-{RowDefinitions[i].ElementPrefix}";
				RowBinding row = rows[i];
				row.Demand = documentRoot.Q<Label>($"{prefix}-demand");
				row.Items = documentRoot.Q<Label>($"{prefix}-items");
				row.Waiting = documentRoot.Q<Label>($"{prefix}-waiting");
				row.Ready = documentRoot.Q<Label>($"{prefix}-ready");
				row.Returned = documentRoot.Q<Label>($"{prefix}-returned");
				row.Active = documentRoot.Q<Label>($"{prefix}-active");
				row.Blocked = documentRoot.Q<Label>($"{prefix}-blocked");
			}

			if (monitorRoot == null || buildingScopeField == null)
			{
				UnbindView();
				return false;
			}

			for (int i = 0; i < rows.Length; ++i)
			{
				if (rows[i].IsValid)
					continue;

				UnbindView();
				return false;
			}

			buildingScopeField.RegisterValueChangedCallback(OnBuildingScopeChanged);
			RefreshBuildingScopeField();
			ConfigureTooltips(documentRoot);
			return true;
		}

		public void UnbindView()
		{
			buildingScopeField?.UnregisterValueChangedCallback(OnBuildingScopeChanged);
			buildingScopeField = null;
			scopeBuildingIds.Clear();
			monitorRoot = null;
			for (int i = 0; i < rows.Length; ++i)
			{
				rows[i].Demand = null;
				rows[i].Items = null;
				rows[i].Waiting = null;
				rows[i].Ready = null;
				rows[i].Returned = null;
				rows[i].Active = null;
				rows[i].Blocked = null;
			}
		}

		public void BindSources(
			MetricsService targetMetricsService,
			TaskManager targetTaskManager,
			WorkerManager targetWorkerManager,
			OrderManager targetOrderManager,
			BuildingManager targetBuildingManager,
			CapsuleDockService targetCapsuleDockService,
			GameTime targetGameTime)
		{
			UnbindSources();
			metricsService = targetMetricsService;
			taskManager = targetTaskManager;
			workerManager = targetWorkerManager;
			orderManager = targetOrderManager;
			buildingManager = targetBuildingManager;
			capsuleDockService = targetCapsuleDockService;
			gameTime = targetGameTime;

			if (taskManager != null)
				taskManager.OnTaskStateChanged += OnSourceChanged;
			if (workerManager != null)
				workerManager.OnWorkerChanged += OnWorkerChanged;
			if (orderManager != null)
				orderManager.OnOrdersChanged += OnSourceChanged;
			if (buildingManager != null)
				buildingManager.OnBuildingsChanged += OnBuildingsChanged;
			if (capsuleDockService != null)
			{
				capsuleDockService.OnCapsuleDocked += OnCapsuleDockChanged;
				capsuleDockService.OnCapsuleUndocked += OnCapsuleDockChanged;
				capsuleDockService.OnDockStateChanged += OnCapsuleDockChanged;
			}
			if (gameTime != null)
			{
				gameTime.OnSimulationTick += OnSimulationTick;
				gameTime.OnTimeScaleChanged += OnTimeScaleChanged;
			}

			RefreshBuildingScopeField();
			if (active)
				Refresh();
		}

		public void UnbindSources()
		{
			if (taskManager != null)
				taskManager.OnTaskStateChanged -= OnSourceChanged;
			if (workerManager != null)
				workerManager.OnWorkerChanged -= OnWorkerChanged;
			if (orderManager != null)
				orderManager.OnOrdersChanged -= OnSourceChanged;
			if (buildingManager != null)
				buildingManager.OnBuildingsChanged -= OnBuildingsChanged;
			if (capsuleDockService != null)
			{
				capsuleDockService.OnCapsuleDocked -= OnCapsuleDockChanged;
				capsuleDockService.OnCapsuleUndocked -= OnCapsuleDockChanged;
				capsuleDockService.OnDockStateChanged -= OnCapsuleDockChanged;
			}
			if (gameTime != null)
			{
				gameTime.OnSimulationTick -= OnSimulationTick;
				gameTime.OnTimeScaleChanged -= OnTimeScaleChanged;
			}

			metricsService = null;
			taskManager = null;
			workerManager = null;
			orderManager = null;
			buildingManager = null;
			capsuleDockService = null;
			gameTime = null;
		}

		public void SetActive(bool value)
		{
			if (active == value)
				return;

			active = value;
			if (active)
				Refresh();
		}

		public bool TrySelectBuildingScope(uint buildingId) =>
			TrySelectBuildingScope((uint?)buildingId);

		public bool TrySelectAllBuildingsScope() => TrySelectBuildingScope(null);

		private bool TrySelectBuildingScope(uint? buildingId)
		{
			if (buildingScopeField == null)
				return false;

			int index = scopeBuildingIds.IndexOf(buildingId);
			if (index < 0 || index >= buildingScopeField.choices.Count)
				return false;

			selectedBuildingId = buildingId;
			buildingScopeField.SetValueWithoutNotify(buildingScopeField.choices[index]);
			if (active)
				Refresh();
			return true;
		}

		public void Refresh()
		{
			if (metricsService == null || monitorRoot == null)
				return;

			Render(
				(category, buildingId) => buildingId.HasValue
					? metricsService.GetWorkDemandSnapshot(category, buildingId.Value)
					: metricsService.GetWorkDemandSnapshot(category),
				(category, buildingId) => buildingId.HasValue
					? metricsService.GetTaskCountSnapshot(category, buildingId.Value)
					: metricsService.GetTaskCountSnapshot(category));
		}

		public void Render(
			Func<LogisticsWorkCategory, WorkDemandSnapshot> demandResolver,
			Func<LogisticsWorkCategory, TaskCountSnapshot> taskResolver)
		{
			if (demandResolver == null || taskResolver == null)
				return;

			Render(
				(category, _) => demandResolver(category),
				(category, _) => taskResolver(category));
		}

		public void Render(
			Func<LogisticsWorkCategory, uint?, WorkDemandSnapshot> demandResolver,
			Func<LogisticsWorkCategory, uint?, TaskCountSnapshot> taskResolver)
		{
			if (monitorRoot == null || demandResolver == null || taskResolver == null)
				return;

			for (int i = 0; i < RowDefinitions.Length; ++i)
			{
				RowDefinition definition = RowDefinitions[i];
				RowBinding row = rows[i];
				WorkDemandSnapshot demand = demandResolver(definition.Category, selectedBuildingId);
				TaskCountSnapshot tasks = taskResolver(definition.Category, selectedBuildingId);

				row.Demand.text = FormatCount(demand.SourceCount);
				row.Items.text = definition.ShowsItemQuantity ? FormatCount(demand.ItemQuantity) : "—";
				row.Waiting.text = FormatCount(tasks.Waiting);
				row.Ready.text = FormatCount(tasks.Ready);
				row.Returned.text = FormatCount(tasks.Returned);
				row.Active.text = FormatCount(tasks.Active);
				row.Blocked.text = FormatCount(tasks.Blocked);
				row.Returned.EnableInClassList(ReturnedWarningClass, tasks.Returned > 0);
				row.Blocked.EnableInClassList(BlockedDangerClass, tasks.Blocked > 0);
			}
		}

		public void Dispose()
		{
			SetActive(false);
			UnbindSources();
			UnbindView();
		}

		private static string FormatCount(int value) => value.ToString("N0");

		private void RefreshBuildingScopeField()
		{
			if (buildingScopeField == null)
				return;

			List<string> choices = new() { AllBuildingsLabel, HubUnassignedLabel };
			scopeBuildingIds.Clear();
			scopeBuildingIds.Add(null);
			scopeBuildingIds.Add(0);

			int selectedIndex = selectedBuildingId.HasValue && selectedBuildingId.Value == 0 ? 1 : 0;
			bool selectedBuildingFound = selectedBuildingId.HasValue == false || selectedBuildingId.Value == 0;
			IReadOnlyList<Building> buildings = buildingManager?.RegisteredBuildings;
			if (buildings != null)
			{
				for (int i = 0; i < buildings.Count; ++i)
				{
					Building building = buildings[i];
					if (building == null || building.RuntimeBuildingId == 0)
						continue;

					if (selectedBuildingId.HasValue && building.RuntimeBuildingId == selectedBuildingId.Value)
					{
						selectedIndex = scopeBuildingIds.Count;
						selectedBuildingFound = true;
					}

					scopeBuildingIds.Add(building.RuntimeBuildingId);
					choices.Add($"{building.DisplayName} · #{building.RuntimeBuildingId}");
				}

				if (selectedBuildingFound == false)
				{
					selectedBuildingId = null;
					selectedIndex = 0;
				}
			}

			buildingScopeField.choices = choices;
			buildingScopeField.SetValueWithoutNotify(choices[selectedIndex]);
		}

		private static void ConfigureTooltips(VisualElement root)
		{
			SetTooltip(root, "workflow-monitor-demand-header", "Demand",
				"Owner-defined workload sources. This is not a projected Task count.");
			SetTooltip(root, "workflow-monitor-items-header", "Items",
				"Remaining item quantity in the reported demand.");
			SetTooltip(root, "workflow-monitor-waiting-header", "Waiting",
				"All waiting Tasks: Ready plus Returned.");
			SetTooltip(root, "workflow-monitor-ready-header", "Ready",
				"Tasks ready for assignment.");
			SetTooltip(root, "workflow-monitor-returned-header", "Returned",
				"Tasks returned for retry.");
			SetTooltip(root, "workflow-monitor-active-header", "Active",
				"Tasks currently assigned to workers.");
			SetTooltip(root, "workflow-monitor-blocked-header", "Blocked",
				"Active tasks whose worker is waiting or blocked. This is a subset of Active.");
			SetTooltip(root, "workflow-monitor-capsule-relocate-items", "Capsule Items",
				"Capsule relocation demand is counted as requests, not item quantity.");
		}

		private static void SetTooltip(VisualElement root, string elementName, string title, string description)
		{
			VisualElement element = root?.Q<VisualElement>(elementName);
			if (element != null)
				element.SetTooltip(UITooltipContent.DescriptionOnly(title, description));
		}

		private void OnSourceChanged()
		{
			if (active)
				Refresh();
		}

		private void OnBuildingScopeChanged(ChangeEvent<string> _)
		{
			if (buildingScopeField == null || buildingScopeField.index < 0 ||
				buildingScopeField.index >= scopeBuildingIds.Count)
				return;

			TrySelectBuildingScope(scopeBuildingIds[buildingScopeField.index]);
		}

		private void OnBuildingsChanged()
		{
			RefreshBuildingScopeField();
			OnSourceChanged();
		}

		private void OnWorkerChanged(AIWorker worker) => OnSourceChanged();
		private void OnCapsuleDockChanged(uint buildingId, CapsuleDock dock) => OnSourceChanged();
		private void OnSimulationTick(SimulationTickContext context) => OnSourceChanged();
		private void OnTimeScaleChanged(float timeScale) => OnSourceChanged();
	}
}
