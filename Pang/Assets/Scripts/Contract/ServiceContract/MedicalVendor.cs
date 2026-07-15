using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Contract/Service Contract/Medical Vendor")]
public sealed class MedicalVendor : Vendor
{
	[SerializeField, Min(0)] private int subscriptionFee;
	[SerializeField, Min(0)] private int serviceFee;

	public override VendorType Type => VendorType.Medical;
	public int SubscriptionFee => subscriptionFee;
	public int ServiceFee => serviceFee;
}
