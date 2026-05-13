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

		public void Setup(ContractRuntime contract)
		{
			currentContract = contract;
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
	}
}
