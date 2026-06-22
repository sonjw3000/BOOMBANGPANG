using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.UI
{
	public class WorkerWindow : MonoBehaviour
	{
		public enum TabType
		{
			Unassigned,
			All,
			Unloading,
			IB,
			OB,
			CargoTransfer,
			Loading,
			Water
		}

		[SerializeField] private UIWindow window;
		[SerializeField] private WorkerItemView itemPrefab;
		[SerializeField] private Transform listRoot;
		[SerializeField] private UnityEngine.UI.Button openMarketButton;
		[SerializeField] private WorkforceMarketWindow marketWindow;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Worker Management";
		[SerializeField] private Sprite icon;

		private WorkerManager workerMgr => GameContext.Instance.WorkerMgr;
		private TabType currentTab = TabType.All;
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

			gameObject.SetActive(false);
		}

		private void OpenMarket()
		{
			if (marketWindow != null)
			{
				marketWindow.Open();
			}
		}

		private void SetupTabs()
{
			if (tabsInitialized) return;
			window.ClearTabs();

			// Order as requested: Unassigned, All, TaskTypes
			window.AddTab("Unassigned", SetTab);
			window.AddTab("All", SetTab);
			window.AddTab("Unloading", SetTab);
			window.AddTab("IB", SetTab);
			window.AddTab("OB", SetTab);
			window.AddTab("CargoTransfer", SetTab);
			window.AddTab("Loading", SetTab);
			window.AddTab("Water", SetTab);

			window.UpdateTabVisuals((int)currentTab);
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
			currentTab = (TabType)tabIndex;
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
			if (currentTab == TabType.All) return true;
			if (currentTab == TabType.Unassigned) return worker.TaskType == WorkerTask.TaskType.Undefined;

			switch (currentTab)
			{
				case TabType.Unloading: return worker.TaskType == WorkerTask.TaskType.Unloading;
				case TabType.IB: return worker.TaskType == WorkerTask.TaskType.IB;
				case TabType.OB: return worker.TaskType == WorkerTask.TaskType.OB;
				case TabType.CargoTransfer: return worker.TaskType == WorkerTask.TaskType.CargoTransfer;
				case TabType.Loading: return worker.TaskType == WorkerTask.TaskType.Loading;
				case TabType.Water: return worker.TaskType == WorkerTask.TaskType.Water;
			}
			return false;
		}
	}
}
