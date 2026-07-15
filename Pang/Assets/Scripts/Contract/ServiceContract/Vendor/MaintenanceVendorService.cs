public sealed class MaintenanceVendorService : VendorProcessor
{
	public override VendorType Type => VendorType.Maintenance;

	public override void ProcessVendor(VendorRuntime vendor)
	{
		if (vendor?.Vendor is not MaintenanceVendor maintenanceVendor || GameContext.HasInstance == false)
			return;

		GameContext.Instance.RobotFixSvc?.ProcessSubscription(maintenanceVendor);
	}
}
