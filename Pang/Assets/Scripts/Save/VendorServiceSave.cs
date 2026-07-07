using System.Collections.Generic;

public partial class VendorService
{
	public VendorServiceSaveData CaptureState()
	{
		VendorServiceSaveData data = new();

		foreach (KeyValuePair<VendorType, List<VendorRuntime>> entry in activeVendors)
		{
			foreach (VendorRuntime runtime in entry.Value)
			{
				if (runtime?.Vendor == null)
					continue;

				data.ActiveVendors.Add(new VendorRuntimeSaveData
				{
					VendorType = runtime.Vendor.Type,
					VendorId = runtime.Vendor.VendorId,
					WeeksSinceLastAction = runtime.WeeksSinceLastAction
				});
			}
		}

		return data;
	}

	public void RestoreState(VendorServiceSaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (VendorRuntimeSaveData vendorData in data.ActiveVendors)
		{
			if (TryGetVendor(vendorData.VendorType, vendorData.VendorId, out Vendor vendor) == false)
				continue;

			activeVendors[vendorData.VendorType].Add(new VendorRuntime(vendor, vendorData.WeeksSinceLastAction));
		}
	}

	public void ResetRuntimeState()
	{
		foreach (List<VendorRuntime> vendors in activeVendors.Values)
			vendors.Clear();
	}
}
