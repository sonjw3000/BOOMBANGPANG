using UnityEngine;


[System.Serializable]
public abstract class Vendor : ScriptableObject
{
	[SerializeField] private string vendorName;
	[SerializeField] private int serviceInterval;

	public string VendorName => vendorName;
	public int ServiceInterval => serviceInterval;
	
	public abstract VendorType Type { get; }
}
