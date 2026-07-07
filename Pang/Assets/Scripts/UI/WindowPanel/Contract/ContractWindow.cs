using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Contract;

namespace Assets.Scripts.UI
{
	public class ContractWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;
		[SerializeField] private ContractItemView itemPrefab;
		[SerializeField] private Transform listRoot;
		[SerializeField] private UnityEngine.UI.Button openMarketButton;
		[SerializeField] private UnityEngine.UI.Button historyButton; // Placeholder for now
		[SerializeField] private ContractMarketWindow marketWindow;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Contract Management";
		[SerializeField] private Sprite icon;

		private ContractService contractService => GameContext.Instance.ContractMgr;
		private VendorService vendorService => GameContext.Instance.VendorService;
		private GameObjectPool itemPool;
		private readonly List<ContractWindowTabEntry> tabs = new();
		private ContractWindowTabEntry currentTab = new(ContractWindowTabKind.Item, default, "Item");

		private readonly struct ContractWindowTabEntry
		{
			public readonly ContractWindowTabKind Kind;
			public readonly VendorType VendorType;
			public readonly string Label;

			public bool IsItem => Kind == ContractWindowTabKind.Item;

			public ContractWindowTabEntry(ContractWindowTabKind kind, VendorType vendorType, string label)
			{
				Kind = kind;
				VendorType = vendorType;
				Label = label;
			}
		}

		private enum ContractWindowTabKind
		{
			Item,
			Vendor
		}

		private void Awake()
		{
			if (window == null) window = GetComponentInChildren<UIWindow>(true);

			window.SetTitle(title);
			window.SetIcon(icon);
			
			if (itemPrefab != null && listRoot != null)
			{
				itemPool = new GameObjectPool(10, () => Instantiate(itemPrefab.gameObject, listRoot));
			}

			if (openMarketButton != null)
			{
				openMarketButton.onClick.AddListener(OpenMarket);
			}

			BuildTabs();
			SelectTab(0);
			gameObject.SetActive(false);
		}

		private void OpenMarket()
		{
			if (marketWindow == null)
				return;

			if (currentTab.IsItem)
				marketWindow.OpenItem();
			else
				marketWindow.OpenVendor(currentTab.VendorType);
		}

		private void OnEnable()
		{
			RefreshList();
		}

		private void Update()
		{
			if (Time.frameCount % 60 == 0) // Refresh every 60 frames
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

		private void RefreshList()
		{
			if (contractService == null || itemPool == null) return;

			itemPool.ReleaseAll();
			if (currentTab.IsItem == false)
			{
				RefreshVendorList();
				return;
			}

			var contracts = contractService.ActiveContracts;

			foreach (var contract in contracts)
			{
				var item = itemPool.Get().GetComponent<ContractItemView>();
				item.Setup(contract);
			}
		}

		private void RefreshVendorList()
		{
			if (vendorService == null || itemPool == null)
				return;

			var vendors = vendorService.GetActiveVendors(currentTab.VendorType);
			foreach (var vendor in vendors)
			{
				var item = itemPool.Get().GetComponent<ContractItemView>();
				item.Setup(vendor);
			}
		}

		private void BuildTabs()
		{
			tabs.Clear();
			window.ClearTabs();

			tabs.Add(new ContractWindowTabEntry(ContractWindowTabKind.Item, default, "Item"));
			foreach (VendorType vendorType in System.Enum.GetValues(typeof(VendorType)))
			{
				tabs.Add(new ContractWindowTabEntry(ContractWindowTabKind.Vendor, vendorType, vendorType.ToString()));
			}

			for (int i = 0; i < tabs.Count; i++)
			{
				window.AddTab(tabs[i].Label, SelectTab);
			}
		}

		private void SelectTab(int index)
		{
			if (index < 0 || index >= tabs.Count)
				return;

			currentTab = tabs[index];
			window.UpdateTabVisuals(index);
			UpdateMarketButtonLabel();
			RefreshList();
		}

		private void UpdateMarketButtonLabel()
		{
			if (openMarketButton == null)
				return;

			var btnText = openMarketButton.GetComponentInChildren<TMP_Text>();
			if (btnText == null)
				return;

			btnText.text = currentTab.IsItem
				? "Sign Item Contract"
				: $"Sign {currentTab.VendorType} Vendor";
		}
	}
}
