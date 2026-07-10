using UnityEngine;


[System.Serializable]
[CreateAssetMenu(menuName = "Contract/Service Contract/Power Vendor")]
public class PowerVendor : Vendor
{
	[SerializeField] private int powerCapacity;
	[SerializeField] private float powerCostPerMonth;

	public override VendorType Type => VendorType.Power;

	public float PowerCost => powerCostPerMonth;
	public int PowerCapacity => powerCapacity;
}
