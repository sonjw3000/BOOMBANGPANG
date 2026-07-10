using UnityEngine;
using UnityEngine.Serialization;


[System.Serializable]
[CreateAssetMenu(menuName = "Contract/Service Contract/Power Vendor")]
public class PowerVendor : Vendor
{
	[SerializeField] private int powerCapacity;
	[FormerlySerializedAs("powerCostPerMonth")]
	[SerializeField] private int weeklyPowerCost;

	public override VendorType Type => VendorType.Power;
	public override int ServiceInterval => 1;

	public int WeeklyPowerCost => weeklyPowerCost;
	public int PowerCapacity => powerCapacity;
}
