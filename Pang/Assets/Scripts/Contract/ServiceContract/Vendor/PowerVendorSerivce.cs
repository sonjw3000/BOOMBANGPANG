public class PowerVendorService : VendorProcessor
{
	public override VendorType Type => VendorType.Power;

	public override void ProcessVendor(VendorRuntime vendor)
	{
		if (vendor?.Vendor is not PowerVendor powerVendor)
			return;

		if (GameContext.HasInstance)
			GameContext.Instance.PowerSvc?.ProcessWeeklyVendor(powerVendor);
	}
}
