using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum VendorType
{
	Launch,
	Power,
	Maintenance,
	Medical,
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

	public VendorRuntime(Vendor vendor, int weeksSinceLastAction)
	{
		Vendor = vendor;
		this.weeksSinceLastAction = Mathf.Max(0, weeksSinceLastAction);
	}

	public bool OnWeekPassed()
	{
		++weeksSinceLastAction;

		if (weeksSinceLastAction >= Vendor.ServiceInterval)
		{
			weeksSinceLastAction = 0;
			// Trigger service event or logic here
			Debug.Log($"{Name} is ready for service.");
			return true;
		}

		return false;
	}
}

public partial class VendorService : MonoBehaviour
{
	[SerializedDictionary("VendorType", "VendorCatalog")]
	[SerializeField] private SerializedDictionary<VendorType,VendorCatalog> catalogs;
	
	private readonly Dictionary<VendorType, List<VendorRuntime>> activeVendors = new();
	private readonly Dictionary<VendorType, VendorProcessor> vendorProcessor = new()
	{
		{ VendorType.Launch, new LaunchVendorPickupService() },
		{ VendorType.Power, new PowerVendorService() },
		{ VendorType.Maintenance, new MaintenanceVendorService() },
		{ VendorType.Medical, new MedicalVendorService() },
	};

	public event Action OnVendorsChanged;

	public IReadOnlyList<VendorRuntime> GetActiveVendors(VendorType vendorType)
	{
		EnsureRuntimeCollections();
		return activeVendors.TryGetValue(vendorType, out List<VendorRuntime> vendors)
			? vendors
			: Array.Empty<VendorRuntime>();
	}

	public bool TryGetActiveVendor(VendorType vendorType, out VendorRuntime vendor)
	{
		IReadOnlyList<VendorRuntime> vendors = GetActiveVendors(vendorType);
		if (vendors.Count > 0)
		{
			vendor = vendors[0];
			return vendor?.Vendor != null;
		}

		vendor = null;
		return false;
	}

	public IReadOnlyList<Vendor> GetCatalog(VendorType vendorType)
	{
		if (catalogs != null && catalogs.TryGetValue(vendorType, out VendorCatalog catalog) && catalog != null)
			return catalog.Vendors;

		return Array.Empty<Vendor>();
	}

	public bool TryGetVendor(VendorType vendorType, uint vendorId, out Vendor vendor)
	{
		vendor = null;

		if (catalogs == null || catalogs.TryGetValue(vendorType, out VendorCatalog catalog) == false || catalog == null)
			return false;

		foreach (Vendor candidate in catalog.Vendors)
		{
			if (candidate == null || candidate.VendorId != vendorId)
				continue;

			vendor = candidate;
			return true;
		}

		return false;
	}

	public bool TryActivateVendor(Vendor vendor)
	{
		if (vendor == null)
			return false;

		EnsureRuntimeCollections();
		List<VendorRuntime> vendorList = activeVendors[vendor.Type];
		if (vendorList.Exists(runtime => runtime.Vendor == vendor))
			return false;

		if (IsExclusiveOnDemandType(vendor.Type))
			vendorList.Clear();

		vendorList.Add(new VendorRuntime(vendor));
		OnVendorsChanged?.Invoke();
		return true;
	}

	private static bool IsExclusiveOnDemandType(VendorType type)
		=> type == VendorType.Medical || type == VendorType.Maintenance;

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

	private void Awake()
	{
		EnsureRuntimeCollections();
	}

	private void EnsureRuntimeCollections()
	{
		foreach (VendorType vendorType in Enum.GetValues(typeof(VendorType)))
		{
			if (activeVendors.ContainsKey(vendorType) == false)
				activeVendors[vendorType] = new List<VendorRuntime>();
		}
	}

	public void OnWeekPass()
	{
		EnsureRuntimeCollections();
		foreach (var vendorList in activeVendors.Values)
		{
			foreach (var vendor in vendorList)
			{
				if (vendor.OnWeekPassed())
				{
					if (vendorProcessor.TryGetValue(vendor.Vendor.Type, out VendorProcessor processor) && processor != null)
					{
						processor.ProcessVendor(vendor);
					}
					else
					{
						Debug.LogWarning($"[VendorService] No processor registered for vendor type: {vendor.Vendor.Type}");
					}
				}
			}
		}

		OnVendorsChanged?.Invoke();
	}
}
