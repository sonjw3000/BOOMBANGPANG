using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(menuName = "Contract/Vendor Catalog")]
public class VendorCatalog : ScriptableObject
{
	[SerializeField] private string catalogName;
	[SerializeField] private VendorType vendorType;
	[SerializeField] private List<Vendor> vendors;

	public string CatalogName => catalogName;
	public VendorType VendorType => vendorType;
	public IReadOnlyList<Vendor> Vendors => vendors;

	private void OnValidate()
	{
		foreach (Vendor v in vendors)
		{
			if (v == null  || v.Type != vendorType)
			{
				Debug.LogError($"Vendor {v?.VendorName ?? "null"} is not of type {vendorType} in catalog {catalogName}");
			}
		}
	}
}
