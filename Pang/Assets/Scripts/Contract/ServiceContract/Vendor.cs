using UnityEngine;


[System.Serializable]
public abstract class Vendor : ScriptableObject
{
	[SerializeField] private string vendorName;
	[SerializeField] private uint vendorId;
	[SerializeField] private int serviceInterval;

	public string VendorName => vendorName;
	public uint VendorId => vendorId;
	public virtual int ServiceInterval => serviceInterval;
	
	public abstract VendorType Type { get; }
}
