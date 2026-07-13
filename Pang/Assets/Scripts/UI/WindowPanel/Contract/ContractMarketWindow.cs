using Assets.Scripts.Contract.ItemContract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
	public sealed class ContractMarketWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;

		[Header("Catalog List")]
		[SerializeField] private Transform categoryListRoot;
		[SerializeField] private GameObject categoryItemPrefab;

		[Header("Item List")]
		[SerializeField] private Transform itemListRoot;
		[SerializeField] private ContractMarketItemView itemPrefab;

		[Header("Window MetaData")]
		[SerializeField] private string title = "Contract Market";
		[SerializeField] private Sprite icon;

		private MarketMode currentMode = MarketMode.Item;
		private VendorType currentVendorType;
		private ContractCatalog selectedCatalog;

		private ContractService ContractService => GameContext.HasInstance ? GameContext.Instance.ContractMgr : null;
		private VendorService VendorService => GameContext.HasInstance ? GameContext.Instance.VendorService : null;
		private LicenseService LicenseService => GameContext.HasInstance ? GameContext.Instance.LicenseService : null;

		private enum MarketMode
		{
			Item,
			Vendor,
		}

		private void Awake()
		{
			window ??= GetComponentInChildren<UIWindow>(true);
			if (window != null)
			{
				window.SetTitle(title);
				window.SetIcon(icon);
				window.Close();
			}
		}

		private void OnEnable()
		{
			if (LicenseService != null)
				LicenseService.OnLicensesChanged += HandleLicensesChanged;
		}

		private void OnDisable()
		{
			if (GameContext.HasInstance && GameContext.Instance.LicenseService != null)
				GameContext.Instance.LicenseService.OnLicensesChanged -= HandleLicensesChanged;
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

		public void Close()
		{
			window?.Close();
			gameObject.SetActive(false);
		}

		private void OpenCurrentMode()
		{
			gameObject.SetActive(true);
			window ??= GetComponentInChildren<UIWindow>(true);
			window?.SetTitle(title);
			window?.Open();
			RefreshMarket();
		}

		private void RefreshMarket()
		{
			ClearChildren(categoryListRoot);
			ClearChildren(itemListRoot);

			if (currentMode == MarketMode.Vendor)
			{
				RefreshVendorMarket();
				return;
			}

			RefreshContractCatalogs();
		}

		private void RefreshContractCatalogs()
		{
			ContractService service = ContractService;
			if (service == null)
				return;

			foreach (ContractCatalog catalog in service.ContractCatalogs)
			{
				if (catalog == null)
					continue;

				bool unlocked = service.IsCatalogUnlocked(catalog);
				string label = unlocked ? catalog.DisplayName : $"{catalog.DisplayName} [LOCKED]";
				CreateCategoryButton(label, () => SelectCatalog(catalog));
			}

			if (selectedCatalog == null || ContainsCatalog(service, selectedCatalog) == false)
			{
				foreach (ContractCatalog catalog in service.ContractCatalogs)
				{
					if (catalog == null)
						continue;

					selectedCatalog = catalog;
					break;
				}
			}

			DisplayCatalog(selectedCatalog);
		}

		private void SelectCatalog(ContractCatalog catalog)
		{
			selectedCatalog = catalog;
			DisplayCatalog(catalog);
		}

		private void DisplayCatalog(ContractCatalog catalog)
		{
			ClearChildren(itemListRoot);
			ContractService service = ContractService;
			if (catalog == null || service == null)
				return;

			if (service.IsCatalogUnlocked(catalog) == false)
			{
				CreateInformationRow($"{catalog.DisplayName} is locked.", true);
				foreach (ContractLicenseRequirement requirement in catalog.RequiredLicenses)
				{
					if (requirement?.License == null)
						continue;

					string currentGrade = LicenseService != null &&
						LicenseService.TryGetAcquiredGrade(requirement.LicenseId, out LicenseGrade grade)
						? grade.ToString()
						: "None";
					CreateInformationRow(
						$"Requires {requirement.License.DisplayName} Grade {requirement.MinimumGrade} / Current {currentGrade}",
						true);
				}
				return;
			}

			if (catalog.Contracts == null || itemPrefab == null || itemListRoot == null)
				return;

			for (int i = 0; i < catalog.Contracts.Length; ++i)
			{
				ContractDefinition definition = catalog.Contracts[i];
				if (definition == null)
					continue;

				ContractMarketItemView item = Instantiate(itemPrefab, itemListRoot);
				item.Setup(i, definition);
			}
		}

		private void RefreshVendorMarket()
		{
			VendorService service = VendorService;
			if (service == null)
				return;

			CreateCategoryButton($"{currentVendorType} Vendors", RefreshVendorItems);
			RefreshVendorItems();
		}

		private void RefreshVendorItems()
		{
			ClearChildren(itemListRoot);
			if (VendorService == null || itemPrefab == null || itemListRoot == null)
				return;

			var vendors = VendorService.GetCatalog(currentVendorType);
			for (int i = 0; i < vendors.Count; ++i)
			{
				ContractMarketItemView item = Instantiate(itemPrefab, itemListRoot);
				item.Setup(i, vendors[i]);
			}
		}

		private void CreateCategoryButton(string label, UnityEngine.Events.UnityAction onClick)
		{
			if (categoryListRoot == null || categoryItemPrefab == null)
				return;

			GameObject item = Instantiate(categoryItemPrefab, categoryListRoot);
			TMP_Text text = item.GetComponentInChildren<TMP_Text>(true);
			if (text != null)
				text.text = label;

			Button button = item.GetComponent<Button>();
			if (button == null)
				return;

			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(onClick);
		}

		private void CreateInformationRow(string label, bool warning)
		{
			if (itemListRoot == null || categoryItemPrefab == null)
				return;

			GameObject item = Instantiate(categoryItemPrefab, itemListRoot);
			TMP_Text text = item.GetComponentInChildren<TMP_Text>(true);
			if (text != null)
			{
				text.text = label;
				text.color = warning ? new Color(1.0f, 0.45f, 0.25f) : Color.white;
			}

			Button button = item.GetComponent<Button>();
			if (button != null)
				button.interactable = false;
		}

		private void HandleLicensesChanged()
		{
			if (isActiveAndEnabled && currentMode == MarketMode.Item)
				RefreshMarket();
		}

		private static bool ContainsCatalog(ContractService service, ContractCatalog target)
		{
			foreach (ContractCatalog catalog in service.ContractCatalogs)
			{
				if (catalog == target)
					return true;
			}

			return false;
		}

		private static void ClearChildren(Transform root)
		{
			if (root == null)
				return;

			foreach (Transform child in root)
				Destroy(child.gameObject);
		}
	}
}
