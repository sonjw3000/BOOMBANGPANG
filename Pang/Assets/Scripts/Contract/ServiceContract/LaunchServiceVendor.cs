using UnityEngine;


[System.Serializable]
[CreateAssetMenu(menuName = "Contract/Service Contract/Launch Service Vendor")]
public class LaunchServiceVendor : Vendor
{
	[SerializeField] private int capsuleCapacity;
	[SerializeField] private float launchCost;

	public override VendorType Type => VendorType.Launch;

	public float LaunchCost => launchCost;
	public int CapsuleCapacity => capsuleCapacity; public int Capacity => capsuleCapacity;
}
