using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Contract/Service Contract/Maintenance Vendor")]
public sealed class MaintenanceVendor : Vendor
{
	[SerializeField, Min(0)] private int subscriptionFee;
	[SerializeField, Min(0)] private int serviceFee;

	public override VendorType Type => VendorType.Maintenance;
	public int SubscriptionFee => subscriptionFee;
	public int ServiceFee => serviceFee;
}
