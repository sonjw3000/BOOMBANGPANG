using System.Collections.Generic;
using UnityEngine;

public class PowerVendorService : VendorProcessor
{
	public override VendorType Type => VendorType.Power;

	public override void ProcessVendor(VendorRuntime vendor)
	{
		if (vendor?.Vendor is not PowerVendor powerVendor)
			return;

		// Implementation for processing power vendor
        
	}
}
