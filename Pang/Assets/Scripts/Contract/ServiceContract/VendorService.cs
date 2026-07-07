using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum VendorType
{
	Launch,
	Power,
	Maintenance,

}

public class VendorRuntime
{
	public readonly Vendor Vendor;
	
	private int weeksSinceLastAction = 0;

	public string Name => Vendor.VendorName;
	public int WeeksSinceLastAction => weeksSinceLastAction;

	public VendorRuntime(Vendor vendor)
	{
		Vendor = vendor;
	}

	public void OnWeekPassed()
	{
		++weeksSinceLastAction;

		if (weeksSinceLastAction >= Vendor.ServiceInterval)
		{
			weeksSinceLastAction = 0;
			// Trigger service event or logic here
			Debug.Log($"{Name} is ready for service.");
			switch (Vendor.Type)
			{
				case VendorType.Launch:
					// Handle launch service logic
					break;
				case VendorType.Power:
					// Handle power service logic
					break;
				case VendorType.Maintenance:
					// Handle maintenance service logic
					break;
				default:
					Debug.LogWarning($"Unhandled vendor type: {Vendor.Type}");
					break;
			}
		}
	}
}

public class VendorService : MonoBehaviour
{
	[SerializedDictionary("VendorType", "VendorCatalog")]
	[SerializeField] private SerializedDictionary<VendorType,VendorCatalog> catalogs;
	private readonly Dictionary<VendorType, List<VendorRuntime>> activeVendors = new();

	public IReadOnlyList<VendorRuntime> GetActiveVendors(VendorType vendorType)
	{
		EnsureRuntimeLists();
		return activeVendors[vendorType];
	}

	public IReadOnlyList<Vendor> GetCatalog(VendorType vendorType)
	{
		if (catalogs != null && catalogs.TryGetValue(vendorType, out VendorCatalog catalog) && catalog != null)
			return catalog.Vendors;

		return Array.Empty<Vendor>();
	}

	public bool TryActivateVendor(Vendor vendor)
	{
		if (vendor == null)
			return false;

		EnsureRuntimeLists();
		List<VendorRuntime> vendorList = activeVendors[vendor.Type];
		if (vendorList.Exists(runtime => runtime.Vendor == vendor))
			return false;

		vendorList.Add(new VendorRuntime(vendor));
		return true;
	}

	private void OnValidate()
	{
		foreach (var kvp in catalogs)
		{
			VendorType vendorType = kvp.Key;
			VendorCatalog catalog = kvp.Value;
			if (catalog == null)
			{
				Debug.LogError($"Catalog for {vendorType} is null.");
				continue;
			}
			if (catalog.VendorType != vendorType)
			{
				Debug.LogError($"Catalog {catalog.CatalogName} has mismatched VendorType. Expected: {vendorType}, Found: {catalog.VendorType}");
			}
		}
	}

	private void Start()
	{
		EnsureRuntimeLists();
	}

	private void EnsureRuntimeLists()
	{
		foreach (VendorType vendorType in Enum.GetValues(typeof(VendorType)))
		{
			if (activeVendors.ContainsKey(vendorType) == false)
				activeVendors[vendorType] = new List<VendorRuntime>();
		}
	}

	public void OnWeekPass()
	{
		EnsureRuntimeLists();

		foreach (var vendorList in activeVendors.Values)
		{
			foreach (var vendor in vendorList)
			{
				vendor.OnWeekPassed();
			}
		}
	}
}
