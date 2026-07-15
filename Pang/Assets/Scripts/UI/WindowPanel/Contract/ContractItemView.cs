using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Contract;

namespace Assets.Scripts.UI
{
	public class ContractItemView : MonoBehaviour
	{
		[Header("Left Side")]
		[SerializeField] private TMP_Text itemNameText;
		[SerializeField] private Button iconButton; // Shell only

		[Header("Right Side")]
		[SerializeField] private TMP_Text typeText;
		[SerializeField] private TMP_Text remainText;
		[SerializeField] private TMP_Text deliveryIntervalText;
		[SerializeField] private TMP_Text amountText;

		private ContractRuntime currentContract;
		private VendorRuntime currentVendor;

		public void Setup(ContractRuntime contract)
		{
			currentContract = contract;
			currentVendor = null;
			var def = contract.Definition;

			if (itemNameText != null) itemNameText.text = def.ItemToHandle != null ? def.ItemToHandle.name : "Unknown Item";
			
			// Icon button is just a shell for now
			// iconButton.image.sprite = def.ItemToHandle?.Icon; 

			if (typeText != null) typeText.text = $"Type: {contract.Type}";
			
			if (remainText != null) remainText.text = $"Remain: {contract.RemainingDuration} / {contract.TotalDuration}";

			// Delivery Interval: {배송까지 남은 weeks / 배달 간격 weeks}
			if (deliveryIntervalText != null) deliveryIntervalText.text = $"Delivery Interval: {contract.DeliveryDelta} / {contract.DeliveryInterval}";
			if (amountText != null) amountText.text = $"AmountPerDelivery: {def.ItemCountsPerDelivery}";
}

		public void Setup(VendorRuntime vendorRuntime)
		{
			currentVendor = vendorRuntime;
			currentContract = null;
			Vendor vendor = vendorRuntime?.Vendor;

			if (itemNameText != null) itemNameText.text = vendor != null ? vendor.VendorName : "Unknown Vendor";
			if (typeText != null) typeText.text = vendor != null ? $"Type: {vendor.Type}" : "Type: Unknown";
			if (remainText != null) remainText.text = vendor != null ? $"Interval: {vendor.ServiceInterval} weeks" : "Interval: -";
			if (deliveryIntervalText != null) deliveryIntervalText.text = vendorRuntime != null ? $"Elapsed: {vendorRuntime.WeeksSinceLastAction} weeks" : "Elapsed: -";
			if (amountText != null) amountText.text = FormatVendorSummary(vendor);
		}

		private static string FormatVendorSummary(Vendor vendor)
		{
			if (vendor is LaunchServiceVendor launchVendor)
				return $"Capacity: {launchVendor.CapsuleCapacity} Capsules / Fee: {launchVendor.LaunchCost:0.##}%";
			if (vendor is PowerVendor powerVendor)
				return $"Capacity: {powerVendor.PowerCapacity} / Fee: {powerVendor.WeeklyPowerCost}/week";
			if (vendor is MedicalVendor medicalVendor)
				return $"Subscription: {medicalVendor.SubscriptionFee} / Call: {medicalVendor.ServiceFee}";
			if (vendor is MaintenanceVendor maintenanceVendor)
				return $"Subscription: {maintenanceVendor.SubscriptionFee} / Call: {maintenanceVendor.ServiceFee}";

			return "Service Terms: -";
		}
	}
}
