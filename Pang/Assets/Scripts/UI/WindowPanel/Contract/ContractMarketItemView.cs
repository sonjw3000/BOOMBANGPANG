using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Assets.Scripts.Contract.ItemContract;

namespace Assets.Scripts.UI
{
	public class ContractMarketItemView : MonoBehaviour
	{
		private const int DefaultDurationMonths = 12;
		private const int ExtendedDurationMonths = 24;
		private const string DurationOption12Months = "12 month";
		private const string DurationOption24Months = "24 month";
		private const string TypeOptionStandard = "Standard";
		private const string TypeOptionExpress = "Express";

		[Header("Item")]
		[SerializeField] private TMP_Text itemNameText;
		[SerializeField] private Button itemButton;
		[SerializeField] private TMP_Text priceLabelText;

		[Header("Summary")]
		[SerializeField] private TMP_Text priceValueText;
		[SerializeField] private TMP_Text totalWeekText;
		[SerializeField] private TMP_Text deliveryIntervalText;
		[SerializeField] private TMP_Text amountText;

		[Header("Controls")]
		[SerializeField] private TMP_Dropdown durationDropdown;
		[SerializeField] private TMP_Dropdown typeDropdown;
		[SerializeField] private Button signButton;

		private ContractDefinition definition;
		private Vendor vendor;
		private int definitionIndex;
		private VendorType vendorType;
		private Sprite defaultItemButtonSprite;
		private Color defaultItemButtonColor = Color.white;
		private bool controlsConfigured;

		private void Awake()
		{
			AutoBindReferences();
			CacheItemButtonDefaults();
			ConfigureControls();
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (Application.isPlaying)
				return;

			AutoBindReferences();
		}
#endif

		public void Setup(int index, ContractDefinition def)
		{
			definitionIndex = index;
			definition = def;
			vendor = null;
			AutoBindReferences();
			ConfigureControls();
			SetItemControlsVisible(true);

			if (priceLabelText != null)
				priceLabelText.text = "Reward";

			if (itemNameText != null)
				itemNameText.text = def != null && def.ItemToHandle != null ? def.ItemToHandle.name : "Unknown Item";

			SetSelectedDuration(GetDefaultDuration(def));
			SetSelectedType(ContractType.Standard);
			RefreshSummary();
			RefreshItemButton();
		}

		public void Setup(int index, Vendor vendor)
		{
			definitionIndex = index;
			definition = null;
			this.vendor = vendor;
			vendorType = vendor != null ? vendor.Type : default;
			AutoBindReferences();
			ConfigureControls();
			SetItemControlsVisible(false);

			if (priceLabelText != null)
				priceLabelText.text = "Fee";

			if (itemNameText != null)
				itemNameText.text = vendor != null ? vendor.VendorName : "Unknown Vendor";

			RefreshVendorSummary();
			RefreshVendorButton();
		}

		private void SignContract()
		{
			if (vendor != null)
			{
				SignVendor();
				return;
			}

			if (definition == null || GameContext.Instance == null || GameContext.Instance.ContractMgr == null)
				return;

			ContractType type = GetSelectedType();
			int duration = GetSelectedDuration();
			GameContext.Instance.ContractMgr.TryAddContract(definition, duration, type);
		}

		private void SignVendor()
		{
			if (vendor == null || GameContext.Instance == null || GameContext.Instance.VendorService == null)
				return;

			GameContext.Instance.VendorService.TryActivateVendor(vendor);
		}

		private void OnDurationChanged(int _)
		{
			RefreshSummary();
		}

		private void OnTypeChanged(int _)
		{
			RefreshSummary();
		}

		private void RefreshSummary()
		{
			if (definition == null)
				return;

			ContractTypeSpec selectedSpec = GetSelectedTypeSpec();
			int durationMonths = GetSelectedDuration();

			if (priceValueText != null)
				priceValueText.text = selectedSpec.BaseReward.ToString();

			if (totalWeekText != null)
				totalWeekText.text = FormatWeeks(durationMonths * 4);

			if (deliveryIntervalText != null)
				deliveryIntervalText.text = FormatWeeks(definition.DeliveryIntervalWeek);

			if (amountText != null)
				amountText.text = definition.ItemCountsPerDelivery.ToString();
		}

		private void RefreshVendorSummary()
		{
			if (vendor == null)
				return;

			if (priceValueText != null)
				priceValueText.text = FormatVendorFee(vendor);

			if (totalWeekText != null)
				totalWeekText.text = FormatWeeks(vendor.ServiceInterval);

			if (deliveryIntervalText != null)
				deliveryIntervalText.text = vendor.Type.ToString();

			if (amountText != null)
				amountText.text = FormatVendorCapacity(vendor);
		}

		private void RefreshItemButton()
		{
			if (itemButton == null || itemButton.image == null)
				return;

			Sprite itemSprite = ResolveItemSprite(definition != null ? definition.ItemToHandle : null);
			itemButton.image.sprite = itemSprite != null ? itemSprite : defaultItemButtonSprite;
			itemButton.image.color = itemSprite != null ? Color.white : defaultItemButtonColor;
			itemButton.image.preserveAspect = itemSprite != null;
			itemButton.onClick.RemoveAllListeners();
		}

		private void RefreshVendorButton()
		{
			if (itemButton != null && itemButton.image != null)
			{
				itemButton.image.sprite = defaultItemButtonSprite;
				itemButton.image.color = defaultItemButtonColor;
				itemButton.image.preserveAspect = false;
				itemButton.onClick.RemoveAllListeners();
			}
		}

		private ContractType GetSelectedType()
		{
			return typeDropdown != null && typeDropdown.value == 1 ? ContractType.Express : ContractType.Standard;
		}

		private ContractTypeSpec GetSelectedTypeSpec()
		{
			if (definition == null)
				return new ContractTypeSpec();

			return GetSelectedType() == ContractType.Express ? definition.ExpressSpec : definition.StandardSpec;
		}

		private int GetSelectedDuration()
		{
			return durationDropdown != null && durationDropdown.value == 1 ? ExtendedDurationMonths : DefaultDurationMonths;
		}

		private int GetDefaultDuration(ContractDefinition def)
		{
			if (def == null)
				return DefaultDurationMonths;

			return def.ContractDuration >= ExtendedDurationMonths ? ExtendedDurationMonths : DefaultDurationMonths;
		}

		private void SetSelectedDuration(int months)
		{
			if (durationDropdown == null)
				return;

			durationDropdown.SetValueWithoutNotify(months >= ExtendedDurationMonths ? 1 : 0);
		}

		private void SetSelectedType(ContractType type)
		{
			if (typeDropdown == null)
				return;

			typeDropdown.SetValueWithoutNotify(type == ContractType.Express ? 1 : 0);
		}

		private void SetItemControlsVisible(bool visible)
		{
			if (durationDropdown != null)
				durationDropdown.gameObject.SetActive(visible);

			if (typeDropdown != null)
				typeDropdown.gameObject.SetActive(visible);
		}

		private void ConfigureControls()
		{
			if (controlsConfigured)
				return;

			if (durationDropdown != null)
			{
				durationDropdown.ClearOptions();
				durationDropdown.AddOptions(new List<string> { DurationOption12Months, DurationOption24Months });
				durationDropdown.onValueChanged.RemoveListener(OnDurationChanged);
				durationDropdown.onValueChanged.AddListener(OnDurationChanged);
			}

			if (typeDropdown != null)
			{
				typeDropdown.ClearOptions();
				typeDropdown.AddOptions(new List<string> { TypeOptionStandard, TypeOptionExpress });
				typeDropdown.onValueChanged.RemoveListener(OnTypeChanged);
				typeDropdown.onValueChanged.AddListener(OnTypeChanged);
			}

			if (signButton != null)
			{
				signButton.onClick.RemoveAllListeners();
				signButton.onClick.AddListener(SignContract);
			}

			controlsConfigured = true;
		}

		private void CacheItemButtonDefaults()
		{
			if (itemButton == null || itemButton.image == null)
				return;

			defaultItemButtonSprite = itemButton.image.sprite;
			defaultItemButtonColor = itemButton.image.color;
		}

		private void AutoBindReferences()
		{
			itemNameText ??= FindComponent<TMP_Text>("ItemSpace/Item");
			itemButton ??= FindComponent<Button>("ItemSpace/ItemImageButton");
			priceLabelText ??= FindComponent<TMP_Text>("ItemPrice/Price");
			priceValueText ??= FindComponent<TMP_Text>("ItemPrice/PriveValue");
			totalWeekText ??= FindComponent<TMP_Text>("TotalWeeks/TotalWeeksVal");
			deliveryIntervalText ??= FindComponent<TMP_Text>("DeliveryInterval/DeliveryIntervalVal");
			amountText ??= FindComponent<TMP_Text>("AmountPerDelivery/AmountPerDeliveryVal");
			durationDropdown ??= FindComponent<TMP_Dropdown>("Footer/Selectors/DurationArea/DurationDropdown");
			typeDropdown ??= FindComponent<TMP_Dropdown>("Footer/Selectors/TypeArea/TypeDropdown");
			signButton ??= FindComponent<Button>("ButtonSpace/SignButton");
		}

		private T FindComponent<T>(string path) where T : Component
		{
			Transform child = transform.Find(path);
			return child != null ? child.GetComponent<T>() : null;
		}

		private static Sprite ResolveItemSprite(ItemDefinition itemDefinition)
		{
			if (itemDefinition == null || itemDefinition.ItemPrefab == null)
				return null;

			Image image = itemDefinition.ItemPrefab.GetComponentInChildren<Image>(true);
			if (image != null && image.sprite != null)
				return image.sprite;

			SpriteRenderer spriteRenderer = itemDefinition.ItemPrefab.GetComponentInChildren<SpriteRenderer>(true);
			return spriteRenderer != null ? spriteRenderer.sprite : null;
		}

		private static string FormatWeeks(int weeks)
		{
			return weeks == 1 ? "1 week" : $"{weeks} weeks";
		}

		private static string FormatVendorFee(Vendor vendor)
		{
			if (vendor is LaunchServiceVendor launchVendor)
				return $"{launchVendor.LaunchCost:0.##}%";
			if (vendor is PowerVendor powerVendor)
				return $"{powerVendor.WeeklyPowerCost}/week";

			return "-";
		}

		private static string FormatVendorCapacity(Vendor vendor)
		{
			if (vendor is LaunchServiceVendor launchVendor)
				return $"{launchVendor.CapsuleCapacity} Capsules";
			if (vendor is PowerVendor powerVendor)
				return powerVendor.PowerCapacity.ToString();

			return "-";
		}
	}
}
