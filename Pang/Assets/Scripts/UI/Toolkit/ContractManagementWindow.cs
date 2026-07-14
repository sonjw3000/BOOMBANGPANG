using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Contract;
using Assets.Scripts.Contract.ItemContract;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class ContractManagementWindow : MonoBehaviour
	{
		private const string SelectedSectionClass = "contract-section-button--selected";
		private const string SelectedCatalogClass = "contract-catalog-button--selected";
		private static readonly List<string> ContractTypes = new() { "Standard", "Express" };
		private static readonly List<string> ContractDurations = new() { "12 months", "24 months" };

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset activeRowTemplate;
		private VisualTreeAsset marketRowTemplate;
		private VisualTreeAsset vendorRowTemplate;
		private Button activeSectionButton;
		private Button marketSectionButton;
		private Button vendorsSectionButton;
		private VisualElement activeSection;
		private VisualElement marketSection;
		private VisualElement vendorsSection;
		private ScrollView activeContractList;
		private Label activeContractEmpty;
		private ScrollView catalogList;
		private ScrollView marketList;
		private Label marketTitle;
		private Label marketMessage;
		private DropdownField vendorTypeField;
		private ScrollView activeVendorList;
		private ScrollView vendorMarketList;
		private Label activeVendorEmpty;
		private ContractService contractService;
		private VendorService vendorService;
		private LicenseService licenseService;
		private ContractCatalog selectedCatalog;
		private VendorType selectedVendorType;
		private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetActiveRowTemplate, VisualTreeAsset targetMarketRowTemplate,
			VisualTreeAsset targetVendorRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			activeRowTemplate = targetActiveRowTemplate;
			marketRowTemplate = targetMarketRowTemplate;
			vendorRowTemplate = targetVendorRowTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started)
				BindServices();
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
			if (InitializeView() == false)
				return;

			if (contractService == null || vendorService == null)
				BindServices();

			RefreshAll();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || activeRowTemplate == null ||
				marketRowTemplate == null || vendorRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[ContractManagementWindow] Window or VisualTreeAsset references are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			activeSectionButton = content.Q<Button>("contract-active-button");
			marketSectionButton = content.Q<Button>("contract-market-button");
			vendorsSectionButton = content.Q<Button>("contract-vendors-button");
			activeSection = content.Q<VisualElement>("contract-active-tab");
			marketSection = content.Q<VisualElement>("contract-market-tab");
			vendorsSection = content.Q<VisualElement>("contract-vendors-tab");
			activeContractList = content.Q<ScrollView>("active-contract-list");
			activeContractEmpty = content.Q<Label>("active-contract-empty");
			catalogList = content.Q<ScrollView>("contract-catalog-list");
			marketList = content.Q<ScrollView>("contract-market-list");
			marketTitle = content.Q<Label>("contract-market-title");
			marketMessage = content.Q<Label>("contract-market-message");
			vendorTypeField = content.Q<DropdownField>("vendor-type-field");
			activeVendorList = content.Q<ScrollView>("active-vendor-list");
			vendorMarketList = content.Q<ScrollView>("vendor-market-list");
			activeVendorEmpty = content.Q<Label>("active-vendor-empty");

			if (activeSectionButton == null || marketSectionButton == null || vendorsSectionButton == null ||
				activeSection == null || marketSection == null || vendorsSection == null || activeContractList == null ||
				activeContractEmpty == null || catalogList == null || marketList == null || marketTitle == null ||
				marketMessage == null || vendorTypeField == null || activeVendorList == null ||
				vendorMarketList == null || activeVendorEmpty == null)
			{
				Debug.LogError("[ContractManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Contract Management");
			window.SetContent(content);
			activeSectionButton.clicked += OpenActiveSection;
			marketSectionButton.clicked += OpenMarketSection;
			vendorsSectionButton.clicked += OpenVendorsSection;
			vendorTypeField.choices = new List<string>(Enum.GetNames(typeof(VendorType)));
			vendorTypeField.SetValueWithoutNotify(selectedVendorType.ToString());
			vendorTypeField.RegisterValueChangedCallback(OnVendorTypeChanged);
			initialized = true;
			SelectSection(0);
			return true;
		}

		private void UnbindControls()
		{
			if (activeSectionButton != null)
				activeSectionButton.clicked -= OpenActiveSection;
			if (marketSectionButton != null)
				marketSectionButton.clicked -= OpenMarketSection;
			if (vendorsSectionButton != null)
				vendorsSectionButton.clicked -= OpenVendorsSection;
			vendorTypeField?.UnregisterValueChangedCallback(OnVendorTypeChanged);
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			contractService = GameContext.Instance.ContractMgr;
			vendorService = GameContext.Instance.VendorService;
			licenseService = GameContext.Instance.LicenseService;
			if (contractService != null)
				contractService.OnContractsChanged += OnContractsChanged;
			if (vendorService != null)
				vendorService.OnVendorsChanged += OnVendorsChanged;
			if (licenseService != null)
				licenseService.OnLicensesChanged += OnLicensesChanged;
		}

		private void UnbindServices()
		{
			if (contractService != null)
				contractService.OnContractsChanged -= OnContractsChanged;
			if (vendorService != null)
				vendorService.OnVendorsChanged -= OnVendorsChanged;
			if (licenseService != null)
				licenseService.OnLicensesChanged -= OnLicensesChanged;
			contractService = null;
			vendorService = null;
			licenseService = null;
		}

		private void OpenActiveSection() => SelectSection(0);
		private void OpenMarketSection() => SelectSection(1);
		private void OpenVendorsSection() => SelectSection(2);

		private void SelectSection(int index)
		{
			bool active = index == 0;
			bool market = index == 1;
			activeSection.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
			marketSection.style.display = market ? DisplayStyle.Flex : DisplayStyle.None;
			vendorsSection.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
			activeSectionButton.EnableInClassList(SelectedSectionClass, active);
			marketSectionButton.EnableInClassList(SelectedSectionClass, market);
			vendorsSectionButton.EnableInClassList(SelectedSectionClass, index == 2);
		}

		private void RefreshAll()
		{
			RefreshActiveContracts();
			RefreshCatalogs();
			RefreshVendors();
		}

		private void RefreshActiveContracts()
		{
			if (activeContractList == null)
				return;

			activeContractList.Clear();
			IReadOnlyList<ContractRuntime> contracts = contractService?.ActiveContracts;
			int count = contracts?.Count ?? 0;
			for (int i = 0; i < count; ++i)
				activeContractList.Add(CreateActiveContractRow(contracts[i]));
			activeContractEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private VisualElement CreateActiveContractRow(ContractRuntime contract)
		{
			TemplateContainer row = activeRowTemplate.CloneTree();
			ContractDefinition definition = contract.Definition;
			row.Q<Label>("contract-name").text = GetContractName(definition);
			row.Q<Label>("contract-item").text = definition.ItemToHandle != null ? definition.ItemToHandle.name : "Unknown item";
			row.Q<Label>("contract-type").text = contract.Type.ToString();
			row.Q<Label>("contract-duration").text = $"{contract.RemainingDuration} / {contract.TotalDuration} weeks";
			row.Q<Label>("contract-delivery").text = $"{Mathf.Max(0, contract.DeliveryDelta)} / {contract.DeliveryInterval} weeks";
			row.Q<Label>("contract-quantity").text = $"{definition.ItemCountsPerDelivery:N0} items";
			return row;
		}

		private void RefreshCatalogs()
		{
			if (catalogList == null || contractService == null)
				return;

			catalogList.Clear();
			IReadOnlyList<ContractCatalog> catalogs = contractService.ContractCatalogs;
			if (selectedCatalog == null || ContainsCatalog(catalogs, selectedCatalog) == false)
				selectedCatalog = FindFirstCatalog(catalogs);

			foreach (ContractCatalog catalog in catalogs)
			{
				if (catalog == null)
					continue;
				ContractCatalog capturedCatalog = catalog;
				bool unlocked = contractService.IsCatalogUnlocked(catalog);
				Button button = new(() => SelectCatalog(capturedCatalog)) { text = catalog.DisplayName };
				button.AddToClassList("contract-catalog-button");
				button.EnableInClassList("contract-catalog-button--locked", unlocked == false);
				button.EnableInClassList(SelectedCatalogClass, catalog == selectedCatalog);
				catalogList.Add(button);
			}

			DisplaySelectedCatalog();
		}

		private void SelectCatalog(ContractCatalog catalog)
		{
			selectedCatalog = catalog;
			RefreshCatalogs();
		}

		private void DisplaySelectedCatalog()
		{
			marketList.Clear();
			if (selectedCatalog == null)
			{
				marketTitle.text = "Contract offers";
				marketMessage.text = "No contract catalogs are configured.";
				return;
			}

			marketTitle.text = selectedCatalog.DisplayName;
			if (contractService.IsCatalogUnlocked(selectedCatalog) == false)
			{
				marketMessage.text = BuildLicenseRequirementMessage(selectedCatalog);
				return;
			}

			marketMessage.text = "Choose terms, then sign the selected offer.";
			if (selectedCatalog.Contracts == null)
				return;
			foreach (ContractDefinition definition in selectedCatalog.Contracts)
			{
				if (definition != null)
					marketList.Add(CreateMarketRow(definition));
			}
		}

		private VisualElement CreateMarketRow(ContractDefinition definition)
		{
			TemplateContainer row = marketRowTemplate.CloneTree();
			Label name = row.Q<Label>("offer-name");
			Label terms = row.Q<Label>("offer-terms");
			Label reward = row.Q<Label>("offer-reward");
			DropdownField type = row.Q<DropdownField>("offer-type");
			DropdownField duration = row.Q<DropdownField>("offer-duration");
			Button sign = row.Q<Button>("offer-sign-button");
			name.text = GetContractName(definition);
			terms.text = $"Every {definition.DeliveryIntervalWeek} weeks  ·  {definition.ItemCountsPerDelivery:N0} items";
			type.choices = ContractTypes;
			type.SetValueWithoutNotify(ContractTypes[0]);
			duration.choices = ContractDurations;
			duration.SetValueWithoutNotify(definition.ContractDuration >= 24 ? ContractDurations[1] : ContractDurations[0]);
			void RefreshReward() => reward.text = $"Reward {(type.index == 1 ? definition.ExpressSpec : definition.StandardSpec).BaseReward:N0}";
			type.RegisterValueChangedCallback(_ => RefreshReward());
			sign.clicked += () => contractService?.TryAddContract(definition, duration.index == 1 ? 24 : 12,
				type.index == 1 ? ContractType.Express : ContractType.Standard);
			RefreshReward();
			return row;
		}

		private void OnVendorTypeChanged(ChangeEvent<string> evt)
		{
			if (Enum.TryParse(evt.newValue, out VendorType vendorType))
				selectedVendorType = vendorType;
			RefreshVendors();
		}

		private void RefreshVendors()
		{
			if (activeVendorList == null || vendorMarketList == null || vendorService == null)
				return;

			activeVendorList.Clear();
			vendorMarketList.Clear();
			IReadOnlyList<VendorRuntime> activeVendors = vendorService.GetActiveVendors(selectedVendorType);
			foreach (VendorRuntime runtime in activeVendors)
				activeVendorList.Add(CreateVendorRow(runtime.Vendor, runtime, false));
			activeVendorEmpty.style.display = activeVendors.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

			IReadOnlyList<Vendor> catalog = vendorService.GetCatalog(selectedVendorType);
			foreach (Vendor vendor in catalog)
			{
				if (vendor != null && IsActiveVendor(activeVendors, vendor) == false)
					vendorMarketList.Add(CreateVendorRow(vendor, null, true));
			}
		}

		private VisualElement CreateVendorRow(Vendor vendor, VendorRuntime runtime, bool canActivate)
		{
			TemplateContainer row = vendorRowTemplate.CloneTree();
			row.Q<Label>("vendor-name").text = vendor != null ? vendor.VendorName : "Unknown vendor";
			row.Q<Label>("vendor-terms").text = FormatVendorTerms(vendor, runtime);
			Button action = row.Q<Button>("vendor-sign-button");
			action.style.display = canActivate ? DisplayStyle.Flex : DisplayStyle.None;
			if (canActivate)
				action.clicked += () => vendorService?.TryActivateVendor(vendor);
			return row;
		}

		private string BuildLicenseRequirementMessage(ContractCatalog catalog)
		{
			StringBuilder message = new($"{catalog.DisplayName} is locked.");
			foreach (ContractLicenseRequirement requirement in catalog.RequiredLicenses)
			{
				if (requirement?.License == null)
					continue;
				string current = licenseService != null &&
					licenseService.TryGetAcquiredGrade(requirement.LicenseId, out LicenseGrade grade)
					? grade.ToString()
					: "None";
				message.Append($"  Requires {requirement.License.DisplayName} {requirement.MinimumGrade} / Current {current}.");
			}
			return message.ToString();
		}

		private void OnContractsChanged()
		{
			if (window != null && window.IsOpen)
				RefreshActiveContracts();
		}

		private void OnVendorsChanged()
		{
			if (window != null && window.IsOpen)
				RefreshVendors();
		}

		private void OnLicensesChanged()
		{
			if (window != null && window.IsOpen)
				RefreshCatalogs();
		}

		private static string GetContractName(ContractDefinition definition)
		{
			if (string.IsNullOrWhiteSpace(definition.ContractName) == false)
				return definition.ContractName;
			return definition.ItemToHandle != null ? definition.ItemToHandle.name : "Unnamed contract";
		}

		private static string FormatVendorTerms(Vendor vendor, VendorRuntime runtime)
		{
			string elapsed = runtime != null ? $" · elapsed {runtime.WeeksSinceLastAction} weeks" : string.Empty;
			if (vendor is LaunchServiceVendor launch)
				return $"{launch.CapsuleCapacity} capsules · {launch.LaunchCost:0.##}% fee · every {launch.ServiceInterval} weeks{elapsed}";
			if (vendor is PowerVendor power)
				return $"{power.PowerCapacity} power · {power.WeeklyPowerCost}/week · every {power.ServiceInterval} weeks{elapsed}";
			return vendor != null ? $"Every {vendor.ServiceInterval} weeks{elapsed}" : "Service terms unavailable";
		}

		private static bool ContainsCatalog(IReadOnlyList<ContractCatalog> catalogs, ContractCatalog target)
		{
			foreach (ContractCatalog catalog in catalogs)
			{
				if (catalog == target)
					return true;
			}
			return false;
		}

		private static ContractCatalog FindFirstCatalog(IReadOnlyList<ContractCatalog> catalogs)
		{
			foreach (ContractCatalog catalog in catalogs)
			{
				if (catalog != null)
					return catalog;
			}
			return null;
		}

		private static bool IsActiveVendor(IReadOnlyList<VendorRuntime> activeVendors, Vendor vendor)
		{
			foreach (VendorRuntime runtime in activeVendors)
			{
				if (runtime.Vendor == vendor)
					return true;
			}
			return false;
		}
	}
}
