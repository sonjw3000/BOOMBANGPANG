using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Assets.Scripts.Contract.ItemContract;

namespace Assets.Scripts.UI
{
	public class ContractMarketWindow : MonoBehaviour
	{
		private const float MinimumWindowWidth = 600f;
		private const float MinimumDetailWidth = 320f;

		[SerializeField] private UIWindow window;
		
		[Header("Left List")]
		[SerializeField] private ContractMarketListButton listButtonPrefab;
		[SerializeField] private Transform listRoot;

		[Header("Right Detail")]
		[SerializeField] private ContractMarketItemView detailView;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Contract Market";
		[SerializeField] private Sprite icon;

		private ContractService contractService => GameContext.Instance.ContractMgr;
		private VendorService vendorService => GameContext.Instance.VendorService;
		private GameObjectPool itemPool;
		private bool initialized;
		private MarketMode currentMode = MarketMode.Item;
		private VendorType currentVendorType;

		private enum MarketMode
		{
			Item,
			Vendor
		}

		private void Awake()
		{
			EnsureInitialized();
		}

		public void Open()
		{
			OpenItem();
		}

		public void OpenItem()
		{
			currentMode = MarketMode.Item;
			title = "Item Contract Market";
			OpenCurrentMode();
		}

		public void OpenVendor(VendorType vendorType)
		{
			currentMode = MarketMode.Vendor;
			currentVendorType = vendorType;
			title = $"{vendorType} Vendor Market";
			OpenCurrentMode();
		}

		private void OpenCurrentMode()
		{
			gameObject.SetActive(true);
			EnsureInitialized();
			if (window == null)
				return;

			if (listRoot == null)
				listRoot = FindListRoot();

			window.SetTitle(title);
			EnsureLayout();
			window.Open();
			RefreshList();
		}

		public void Close()
		{
			EnsureInitialized();
			if (window == null)
				return;

			window.Close();
			gameObject.SetActive(false);
		}

		private void RefreshList()
		{
			EnsureInitialized();
			if (currentMode == MarketMode.Item && contractService == null) return;
			if (currentMode == MarketMode.Vendor && vendorService == null) return;

			if (listRoot == null)
				listRoot = FindListRoot();

			if (itemPool == null)
			{
				if (listButtonPrefab != null && listRoot != null)
				{
					itemPool = new GameObjectPool(10, () => Instantiate(listButtonPrefab.gameObject, listRoot));
				}
				else
				{
					return;
				}
			}

			itemPool.ReleaseAll();
			if (currentMode == MarketMode.Vendor)
			{
				RefreshVendorList();
				return;
			}

			var definitions = contractService.ContractDefinitions;

			for (int i = 0; i < definitions.Count; i++)
			{
				var item = itemPool.Get().GetComponent<ContractMarketListButton>();
				item.Setup(i, definitions[i], OnContractSelected);
			}

			if (definitions.Count > 0)
			{
				OnContractSelected(0, definitions[0]);
			}
			else if (detailView != null)
			{
				detailView.gameObject.SetActive(false);
			}
		}

		private void RefreshVendorList()
		{
			var vendors = vendorService.GetCatalog(currentVendorType);

			for (int i = 0; i < vendors.Count; i++)
			{
				var item = itemPool.Get().GetComponent<ContractMarketListButton>();
				item.Setup(i, vendors[i], OnVendorSelected);
			}

			if (vendors.Count > 0)
			{
				OnVendorSelected(0, vendors[0]);
			}
			else if (detailView != null)
			{
				detailView.gameObject.SetActive(false);
			}
		}

		private void OnContractSelected(int index, ContractDefinition def)
		{
			if (detailView != null)
			{
				detailView.gameObject.SetActive(true);
				detailView.Setup(index, def);
			}
		}

		private void OnVendorSelected(int index, Vendor vendor)
		{
			if (detailView != null)
			{
				detailView.gameObject.SetActive(true);
				detailView.Setup(index, vendor);
			}
		}

		private void EnsureInitialized()
		{
			window ??= GetComponentInChildren<UIWindow>(true);
			detailView ??= GetComponentInChildren<ContractMarketItemView>(true);
			if (listRoot == null)
				listRoot = FindListRoot();

			EnsureLayout();

			if (initialized == false && window != null)
			{
				window.SetTitle(title);
				window.SetIcon(icon);
				window.Close();
				initialized = true;
			}

			if (itemPool == null && listButtonPrefab != null && listRoot != null)
			{
				itemPool = new GameObjectPool(10, () => Instantiate(listButtonPrefab.gameObject, listRoot));
			}
		}

		private Transform FindListRoot()
		{
			Transform exactPath = transform.Find("WindowBase/ContentRoot/LeftPanel/ListRoot");
			if (exactPath != null)
				return exactPath;

			return GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(child => child.name == "ListRoot" && child.parent != null && child.parent.name == "LeftPanel");
		}

		private void EnsureLayout()
		{
			RectTransform windowRect = GetComponent<RectTransform>();
			if (windowRect != null && windowRect.sizeDelta.x < MinimumWindowWidth)
			{
				windowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MinimumWindowWidth);
			}

			if (detailView == null)
				return;

			LayoutElement detailLayout = detailView.GetComponent<LayoutElement>();
			if (detailLayout != null)
			{
				if (detailLayout.minWidth < MinimumDetailWidth)
					detailLayout.minWidth = MinimumDetailWidth;

				if (detailLayout.preferredWidth < MinimumDetailWidth)
					detailLayout.preferredWidth = MinimumDetailWidth;

				if (detailLayout.flexibleWidth < 1f)
					detailLayout.flexibleWidth = 1f;
			}

			RectTransform detailRect = detailView.GetComponent<RectTransform>();
			if (detailRect != null && detailRect.sizeDelta.x < MinimumDetailWidth)
			{
				detailRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MinimumDetailWidth);
			}
		}
	}
}
