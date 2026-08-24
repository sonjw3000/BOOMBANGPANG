using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class LogisticsWorkHudPresenter : IDisposable
	{
		private const float PeriodicRefreshIntervalSeconds = 1f;
		private const string HiddenClass = "logistics-work-hud--hidden";
		private const string BlockedRowClass = "logistics-work-hud__row--blocked";
		private const string UnservedRowClass = "logistics-work-hud__row--unserved";
		private const string BlockedTotalClass = "logistics-work-hud__total--blocked";
		private const string BottleneckHiddenClass = "logistics-work-hud__bottleneck--hidden";
		private const string BottleneckDangerClass = "logistics-work-hud__bottleneck--danger";
		private const string BottleneckWarningClass = "logistics-work-hud__bottleneck--warning";

		private enum BottleneckKind
		{
			None,
			Waiting,
			Unserved,
			Returned,
			Blocked,
		}

		private readonly struct BuildingBottleneck
		{
			public readonly uint BuildingId;
			public readonly string BuildingName;
			public readonly BottleneckKind Kind;
			public readonly int Demand;
			public readonly int UnservedDemand;
			public readonly int Waiting;
			public readonly int Returned;
			public readonly int Active;
			public readonly int Blocked;

			public BuildingBottleneck(
				uint buildingId,
				string buildingName,
				BottleneckKind kind,
				int demand,
				int unservedDemand,
				int waiting,
				int returned,
				int active,
				int blocked)
			{
				BuildingId = buildingId;
				BuildingName = buildingName;
				Kind = kind;
				Demand = demand;
				UnservedDemand = unservedDemand;
				Waiting = waiting;
				Returned = returned;
				Active = active;
				Blocked = blocked;
			}
		}

		private readonly struct RowDefinition
		{
			public readonly string ElementPrefix;
			public readonly LogisticsWorkCategory[] Categories;

			public RowDefinition(string elementPrefix, params LogisticsWorkCategory[] categories)
			{
				ElementPrefix = elementPrefix;
				Categories = categories;
			}
		}

		private sealed class RowBinding
		{
			public VisualElement Root;
			public Label Demand;
			public Label Waiting;
			public Label Active;
			public Label Blocked;

			public bool IsValid =>
				Root != null && Demand != null && Waiting != null && Active != null && Blocked != null;
		}

		private static readonly RowDefinition[] RowDefinitions =
		{
			new("picking", LogisticsWorkCategory.Picking),
			new("storing", LogisticsWorkCategory.Storing),
			new("packing",
				LogisticsWorkCategory.PackingInput,
				LogisticsWorkCategory.Packing,
				LogisticsWorkCategory.PackingOutput),
			new("capsule-relocate", LogisticsWorkCategory.CapsuleRelocate),
		};
		private static readonly LogisticsWorkCategory[] BottleneckCategories =
		{
			LogisticsWorkCategory.Picking,
			LogisticsWorkCategory.Storing,
			LogisticsWorkCategory.PackingInput,
			LogisticsWorkCategory.Packing,
			LogisticsWorkCategory.PackingOutput,
			LogisticsWorkCategory.CapsuleRelocate,
		};

		private readonly RowBinding[] rows =
		{
			new(), new(), new(), new(),
		};

		private VisualElement hudRoot;
		private Button headerButton;
		private Label totalWaiting;
		private Label totalActive;
		private Label totalBlocked;
		private Button bottleneckButton;
		private Label bottleneckBuilding;
		private Label bottleneckStatus;
		private MetricsService metricsService;
		private TaskManager taskManager;
		private WorkerManager workerManager;
		private OrderManager orderManager;
		private BuildingManager buildingManager;
		private CapsuleDockService capsuleDockService;
		private GameTime gameTime;
		private Action openMonitor;
		private Action<uint> openBuildingMonitor;
		private uint? bottleneckBuildingId;
		private float nextPeriodicRefreshTime;

		public void ConfigureNavigation(Action targetOpenMonitor, Action<uint> targetOpenBuildingMonitor = null)
		{
			openMonitor = targetOpenMonitor;
			openBuildingMonitor = targetOpenBuildingMonitor;
		}

		public bool BindView(VisualElement documentRoot)
		{
			UnbindView();
			if (documentRoot == null)
				return false;

			hudRoot = documentRoot.Q<VisualElement>("logistics-work-hud");
			headerButton = documentRoot.Q<Button>("logistics-work-hud-header");
			totalWaiting = documentRoot.Q<Label>("logistics-work-hud-total-waiting");
			totalActive = documentRoot.Q<Label>("logistics-work-hud-total-active");
			totalBlocked = documentRoot.Q<Label>("logistics-work-hud-total-blocked");
			bottleneckButton = documentRoot.Q<Button>("logistics-work-hud-bottleneck");
			bottleneckBuilding = documentRoot.Q<Label>("logistics-work-hud-bottleneck-building");
			bottleneckStatus = documentRoot.Q<Label>("logistics-work-hud-bottleneck-status");

			for (int i = 0; i < RowDefinitions.Length; ++i)
			{
				string prefix = $"logistics-work-hud-{RowDefinitions[i].ElementPrefix}";
				RowBinding row = rows[i];
				row.Root = documentRoot.Q<VisualElement>($"{prefix}-row");
				row.Demand = documentRoot.Q<Label>($"{prefix}-demand");
				row.Waiting = documentRoot.Q<Label>($"{prefix}-waiting");
				row.Active = documentRoot.Q<Label>($"{prefix}-active");
				row.Blocked = documentRoot.Q<Label>($"{prefix}-blocked");
			}

			bool valid = hudRoot != null && headerButton != null && totalWaiting != null &&
				totalActive != null && totalBlocked != null && bottleneckButton != null &&
				bottleneckBuilding != null && bottleneckStatus != null;
			for (int i = 0; valid && i < rows.Length; ++i)
				valid = rows[i].IsValid;

			if (valid == false)
			{
				UnbindView();
				return false;
			}

			BindClickHandler();
			ConfigureTooltips(documentRoot);
			ClearBuildingBottleneck();
			SetRootVisible(false);
			return true;
		}

		public void UnbindView()
		{
			UnbindClickHandler();
			SetRootVisible(false);
			hudRoot = null;
			headerButton = null;
			totalWaiting = null;
			totalActive = null;
			totalBlocked = null;
			bottleneckButton = null;
			bottleneckBuilding = null;
			bottleneckStatus = null;
			bottleneckBuildingId = null;

			for (int i = 0; i < rows.Length; ++i)
			{
				rows[i].Root = null;
				rows[i].Demand = null;
				rows[i].Waiting = null;
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
			nextPeriodicRefreshTime = 0f;

			if (taskManager != null)
				taskManager.OnTaskStateChanged += OnSourceChanged;
			if (workerManager != null)
				workerManager.OnWorkerChanged += OnWorkerChanged;
			if (orderManager != null)
				orderManager.OnOrdersChanged += OnSourceChanged;
			if (buildingManager != null)
				buildingManager.OnBuildingsChanged += OnSourceChanged;
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
				buildingManager.OnBuildingsChanged -= OnSourceChanged;
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
			nextPeriodicRefreshTime = 0f;
			SetRootVisible(false);
		}

		public void Refresh()
		{
			if (metricsService == null || hudRoot == null)
			{
				SetRootVisible(false);
				return;
			}

			Render(metricsService.GetWorkDemandSnapshot, metricsService.GetTaskCountSnapshot);
			RenderBuildingBottleneck(
				buildingManager?.RegisteredBuildings,
				metricsService.GetWorkDemandSnapshot,
				metricsService.GetTaskCountSnapshot);
		}

		public void Render(
			Func<LogisticsWorkCategory, WorkDemandSnapshot> demandResolver,
			Func<LogisticsWorkCategory, TaskCountSnapshot> taskResolver)
		{
			if (hudRoot == null || demandResolver == null || taskResolver == null)
				return;

			int waitingTotal = 0;
			int activeTotal = 0;
			int blockedTotal = 0;
			for (int i = 0; i < RowDefinitions.Length; ++i)
			{
				RowDefinition definition = RowDefinitions[i];
				RowBinding row = rows[i];
				int demand = 0;
				int waiting = 0;
				int active = 0;
				int blocked = 0;
				for (int categoryIndex = 0; categoryIndex < definition.Categories.Length; ++categoryIndex)
				{
					LogisticsWorkCategory category = definition.Categories[categoryIndex];
					demand += demandResolver(category).SourceCount;
					TaskCountSnapshot tasks = taskResolver(category);
					waiting += tasks.Waiting;
					active += tasks.Active;
					blocked += tasks.Blocked;
				}

				row.Demand.text = FormatCount(demand);
				row.Waiting.text = FormatCount(waiting);
				row.Active.text = FormatCount(active);
				row.Blocked.text = FormatCount(blocked);
				row.Root.EnableInClassList(BlockedRowClass, blocked > 0);
				row.Root.EnableInClassList(UnservedRowClass, demand > 0 && waiting == 0 && active == 0);

				waitingTotal += waiting;
				activeTotal += active;
				blockedTotal += blocked;
			}

			totalWaiting.text = $"{FormatCount(waitingTotal)} WAIT";
			totalActive.text = $"{FormatCount(activeTotal)} ACTIVE";
			totalBlocked.text = $"{FormatCount(blockedTotal)} BLOCK";
			totalBlocked.EnableInClassList(BlockedTotalClass, blockedTotal > 0);
			SetRootVisible(true);
		}

		public void RenderBuildingBottleneck(
			IReadOnlyList<Building> buildings,
			Func<LogisticsWorkCategory, uint, WorkDemandSnapshot> demandResolver,
			Func<LogisticsWorkCategory, uint, TaskCountSnapshot> taskResolver)
		{
			if (bottleneckButton == null || bottleneckBuilding == null || bottleneckStatus == null ||
				buildings == null || demandResolver == null || taskResolver == null)
			{
				ClearBuildingBottleneck();
				return;
			}

			bool hasBottleneck = false;
			BuildingBottleneck selected = default;
			for (int buildingIndex = 0; buildingIndex < buildings.Count; ++buildingIndex)
			{
				Building building = buildings[buildingIndex];
				if (building == null || building.RuntimeBuildingId == 0)
					continue;

				int demand = 0;
				int unservedDemand = 0;
				int waiting = 0;
				int returned = 0;
				int active = 0;
				int blocked = 0;
				for (int categoryIndex = 0; categoryIndex < BottleneckCategories.Length; ++categoryIndex)
				{
					LogisticsWorkCategory category = BottleneckCategories[categoryIndex];
					WorkDemandSnapshot categoryDemand = demandResolver(category, building.RuntimeBuildingId);
					TaskCountSnapshot tasks = taskResolver(category, building.RuntimeBuildingId);
					demand += categoryDemand.SourceCount;
					if (categoryDemand.SourceCount > 0 && tasks.Total == 0)
						unservedDemand += categoryDemand.SourceCount;
					waiting += tasks.Waiting;
					returned += tasks.Returned;
					active += tasks.Active;
					blocked += tasks.Blocked;
				}

				BottleneckKind kind = ResolveBottleneckKind(unservedDemand, waiting, returned, blocked);
				if (kind == BottleneckKind.None)
					continue;

				BuildingBottleneck candidate = new(
					building.RuntimeBuildingId,
					building.DisplayName,
					kind,
					demand,
					unservedDemand,
					waiting,
					returned,
					active,
					blocked);
				if (hasBottleneck == false || IsHigherPriority(candidate, selected))
				{
					selected = candidate;
					hasBottleneck = true;
				}
			}

			if (hasBottleneck)
				RenderBuildingBottleneck(selected);
			else
				ClearBuildingBottleneck();
		}

		public void Dispose()
		{
			UnbindSources();
			UnbindView();
			openMonitor = null;
			openBuildingMonitor = null;
		}

		private static string FormatCount(int value) => value.ToString("N0");

		private static BottleneckKind ResolveBottleneckKind(
			int unservedDemand,
			int waiting,
			int returned,
			int blocked)
		{
			if (blocked > 0)
				return BottleneckKind.Blocked;
			if (returned > 0)
				return BottleneckKind.Returned;
			if (unservedDemand > 0)
				return BottleneckKind.Unserved;
			if (waiting > 0)
				return BottleneckKind.Waiting;
			return BottleneckKind.None;
		}

		private static bool IsHigherPriority(BuildingBottleneck candidate, BuildingBottleneck current)
		{
			if (candidate.Kind != current.Kind)
				return candidate.Kind > current.Kind;

			int candidateSignal = GetSignalCount(candidate);
			int currentSignal = GetSignalCount(current);
			if (candidateSignal != currentSignal)
				return candidateSignal > currentSignal;
			if (candidate.Waiting != current.Waiting)
				return candidate.Waiting > current.Waiting;
			if (candidate.Demand != current.Demand)
				return candidate.Demand > current.Demand;
			return candidate.BuildingId < current.BuildingId;
		}

		private static int GetSignalCount(BuildingBottleneck bottleneck)
		{
			return bottleneck.Kind switch
			{
				BottleneckKind.Blocked => bottleneck.Blocked,
				BottleneckKind.Returned => bottleneck.Returned,
				BottleneckKind.Unserved => bottleneck.UnservedDemand,
				BottleneckKind.Waiting => bottleneck.Waiting,
				_ => 0,
			};
		}

		private void RenderBuildingBottleneck(BuildingBottleneck bottleneck)
		{
			bottleneckBuildingId = bottleneck.BuildingId;
			bottleneckBuilding.text = $"{bottleneck.BuildingName} · #{bottleneck.BuildingId}";
			bottleneckStatus.text = bottleneck.Kind switch
			{
				BottleneckKind.Blocked => $"{FormatCount(bottleneck.Blocked)} BLOCK",
				BottleneckKind.Returned => $"{FormatCount(bottleneck.Returned)} RETURN",
				BottleneckKind.Unserved => $"{FormatCount(bottleneck.UnservedDemand)} NEED",
				BottleneckKind.Waiting => $"{FormatCount(bottleneck.Waiting)} WAIT",
				_ => string.Empty,
			};
			bottleneckButton.EnableInClassList(
				BottleneckDangerClass,
				bottleneck.Kind == BottleneckKind.Blocked);
			bottleneckButton.EnableInClassList(
				BottleneckWarningClass,
				bottleneck.Kind == BottleneckKind.Returned || bottleneck.Kind == BottleneckKind.Unserved);
			bottleneckButton.SetEnabled(true);
			bottleneckButton.SetTooltip(UITooltipContent.DescriptionOnly(
				$"Building Bottleneck · {bottleneck.BuildingName} #{bottleneck.BuildingId}",
				$"Demand sources {bottleneck.Demand} (unserved {bottleneck.UnservedDemand}) · " +
				$"Waiting {bottleneck.Waiting} " +
				$"(Returned {bottleneck.Returned}) · Active {bottleneck.Active} · " +
				$"Blocked {bottleneck.Blocked}. Open this building in Workflow Monitor."));
			SetBottleneckVisible(true);
		}

		private void ClearBuildingBottleneck()
		{
			bottleneckBuildingId = null;
			if (bottleneckButton == null)
				return;

			bottleneckBuilding.text = string.Empty;
			bottleneckStatus.text = string.Empty;
			bottleneckButton.EnableInClassList(BottleneckDangerClass, false);
			bottleneckButton.EnableInClassList(BottleneckWarningClass, false);
			bottleneckButton.SetEnabled(false);
			SetBottleneckVisible(false);
		}

		private static void ConfigureTooltips(VisualElement root)
		{
			SetTooltip(root, "logistics-work-hud-header", "Logistics Work",
				"Open the detailed Workflow Monitor.");
			SetTooltip(root, "logistics-work-hud-demand-header", "Need",
				"Owner-reported workload sources. This is separate from generated Tasks.");
			SetTooltip(root, "logistics-work-hud-waiting-header", "Waiting",
				"Ready plus Returned Tasks waiting for work.");
			SetTooltip(root, "logistics-work-hud-active-header", "Running",
				"Tasks currently assigned to workers.");
			SetTooltip(root, "logistics-work-hud-blocked-header", "Blocked",
				"Running Tasks whose worker is waiting or blocked. This is part of Running.");
		}

		private static void SetTooltip(VisualElement root, string elementName, string title, string description)
		{
			VisualElement element = root?.Q<VisualElement>(elementName);
			if (element != null)
				element.SetTooltip(UITooltipContent.DescriptionOnly(title, description));
		}

		private void BindClickHandler()
		{
			if (headerButton != null)
				headerButton.clicked += OpenMonitor;
			if (bottleneckButton != null)
				bottleneckButton.clicked += OpenBuildingMonitor;
		}

		private void UnbindClickHandler()
		{
			if (headerButton != null)
				headerButton.clicked -= OpenMonitor;
			if (bottleneckButton != null)
				bottleneckButton.clicked -= OpenBuildingMonitor;
		}

		private void OpenMonitor()
		{
			openMonitor?.Invoke();
		}

		private void OpenBuildingMonitor()
		{
			if (bottleneckBuildingId.HasValue)
				openBuildingMonitor?.Invoke(bottleneckBuildingId.Value);
		}

		private void SetRootVisible(bool visible)
		{
			if (hudRoot != null)
				hudRoot.EnableInClassList(HiddenClass, visible == false);
		}

		private void SetBottleneckVisible(bool visible)
		{
			if (bottleneckButton != null)
				bottleneckButton.EnableInClassList(BottleneckHiddenClass, visible == false);
		}

		private void OnSourceChanged()
		{
			Refresh();
		}

		private void OnWorkerChanged(AIWorker worker) => OnSourceChanged();
		private void OnCapsuleDockChanged(uint buildingId, CapsuleDock dock) => OnSourceChanged();

		private void OnSimulationTick(SimulationTickContext context)
		{
			float now = Time.unscaledTime;
			if (now < nextPeriodicRefreshTime)
				return;

			nextPeriodicRefreshTime = now + PeriodicRefreshIntervalSeconds;
			Refresh();
		}

		private void OnTimeScaleChanged(float timeScale) => OnSourceChanged();
	}
}
