using System;
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

		private readonly RowBinding[] rows =
		{
			new(), new(), new(), new(),
		};

		private VisualElement hudRoot;
		private Button headerButton;
		private Label totalWaiting;
		private Label totalActive;
		private Label totalBlocked;
		private MetricsService metricsService;
		private TaskManager taskManager;
		private WorkerManager workerManager;
		private OrderManager orderManager;
		private BuildingManager buildingManager;
		private CapsuleDockService capsuleDockService;
		private GameTime gameTime;
		private Action openMonitor;
		private float nextPeriodicRefreshTime;

		public void ConfigureNavigation(Action targetOpenMonitor)
		{
			openMonitor = targetOpenMonitor;
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
				totalActive != null && totalBlocked != null;
			for (int i = 0; valid && i < rows.Length; ++i)
				valid = rows[i].IsValid;

			if (valid == false)
			{
				UnbindView();
				return false;
			}

			BindClickHandler();
			ConfigureTooltips(documentRoot);
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

		public void Dispose()
		{
			UnbindSources();
			UnbindView();
			openMonitor = null;
		}

		private static string FormatCount(int value) => value.ToString("N0");

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
		}

		private void UnbindClickHandler()
		{
			if (headerButton != null)
				headerButton.clicked -= OpenMonitor;
		}

		private void OpenMonitor()
		{
			openMonitor?.Invoke();
		}

		private void SetRootVisible(bool visible)
		{
			if (hudRoot != null)
				hudRoot.EnableInClassList(HiddenClass, visible == false);
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
