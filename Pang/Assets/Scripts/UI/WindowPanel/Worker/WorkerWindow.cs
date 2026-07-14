using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.UI
{
	public class WorkerWindow : MonoBehaviour
	{
		private const int UnassignedTabIndex = 0;
		private const int AllTabIndex = 1;
		private const int TaskTabStartIndex = 2;

		[SerializeField] private UIWindow window;
		[SerializeField] private WorkerItemView itemPrefab;
		[SerializeField] private Transform listRoot;
		[SerializeField] private UnityEngine.UI.Button openMarketButton;
		[SerializeField] private UnityEngine.UI.Button openSpawnAreaButton;
		[SerializeField] private WorkforceMarketWindow marketWindow;
		[SerializeField] private AreaControlWindow areaControlWindow;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Worker Management";
		[SerializeField] private Sprite icon;

		private WorkerManager workerMgr => GameContext.Instance.WorkerMgr;
		private int currentTabIndex = AllTabIndex;
		private readonly List<WorkerTask.TaskType> taskTabs = new();
		private List<WorkerItemView> activeItems = new List<WorkerItemView>();
		private bool tabsInitialized = false;

		private void Awake()
		{
			if (window == null) window = GetComponentInChildren<UIWindow>(true);

			window.SetTitle(title);
			window.SetIcon(icon);
			SetupTabs();
            
			if (openMarketButton != null)
			{
				openMarketButton.onClick.AddListener(OpenMarket);
			}
			if (openSpawnAreaButton != null)
				openSpawnAreaButton.onClick.AddListener(OpenSpawnAreas);

			gameObject.SetActive(false);
		}

		private void OpenMarket()
		{
			if (marketWindow != null)
			{
				marketWindow.Open();
			}
		}

		private void OpenSpawnAreas()
		{
			areaControlWindow ??= FindFirstObjectByType<AreaControlWindow>(FindObjectsInactive.Include);
			areaControlWindow?.OpenForAreaType(AreaType.WorkerSpawn);
		}

		private void OnDestroy()
		{
			openMarketButton?.onClick.RemoveListener(OpenMarket);
			openSpawnAreaButton?.onClick.RemoveListener(OpenSpawnAreas);
		}

		private void SetupTabs()
		{
			if (tabsInitialized) return;
			window.ClearTabs();
			taskTabs.Clear();

			window.AddTab("Unassigned", SetTab);
			window.AddTab("All", SetTab);

			foreach (WorkerTask.TaskType taskType in Enum.GetValues(typeof(WorkerTask.TaskType)))
			{
				if (IsTaskTabType(taskType) == false)
					continue;

				taskTabs.Add(taskType);
				window.AddTab(taskType.ToString(), SetTab);
			}

			window.UpdateTabVisuals(currentTabIndex);
			tabsInitialized = true;
		}

		private void OnEnable()
		{
			SetupTabs();
			RefreshList();
		}

		private void Update()
		{
			if (Time.frameCount % 30 == 0)
			{
				RefreshList();
			}
		}

		public void Open()
		{
			gameObject.SetActive(true);
			window.Open();
		}

		public void Close()
		{
			window.Close();
			gameObject.SetActive(false);
		}

		public void SetTab(int tabIndex)
		{
			currentTabIndex = tabIndex;
			window.UpdateTabVisuals(tabIndex);
			RefreshList();
		}

		private void RefreshList()
		{
			if (workerMgr == null) return;

			var workers = workerMgr.Workers;
			var filteredWorkers = workers.Where(ShouldShowInTab).ToList();

			// Simple management: if counts differ, clear and rebuild. 
			// Better would be updating existing items.
			if (activeItems.Count != filteredWorkers.Count)
			{
				foreach (var item in activeItems)
				{
					if (item != null) Destroy(item.gameObject);
				}
				activeItems.Clear();

				foreach (var worker in filteredWorkers)
				{
					var item = Instantiate(itemPrefab, listRoot);
					item.Setup(worker);
					activeItems.Add(item);
				}
			}
			else
			{
				for (int i = 0; i < filteredWorkers.Count; i++)
				{
					activeItems[i].Setup(filteredWorkers[i]);
				}
			}
		}

		private bool ShouldShowInTab(AIWorker worker)
		{
			if (currentTabIndex == AllTabIndex)
				return true;

			if (currentTabIndex == UnassignedTabIndex)
				return worker.TaskType == WorkerTask.TaskType.Undefined;

			int taskTabIndex = currentTabIndex - TaskTabStartIndex;
			return taskTabIndex >= 0 &&
				taskTabIndex < taskTabs.Count &&
				worker.TaskType == taskTabs[taskTabIndex];
		}

		private static bool IsTaskTabType(WorkerTask.TaskType taskType)
		{
			return taskType < WorkerTask.TaskType.Undefined;
		}
	}
}
