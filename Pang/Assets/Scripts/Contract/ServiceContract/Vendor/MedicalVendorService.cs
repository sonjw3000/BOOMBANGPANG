public sealed class MedicalVendorService : VendorProcessor
{
	public override VendorType Type => VendorType.Medical;

	public override void ProcessVendor(VendorRuntime vendor)
	{
		if (vendor?.Vendor is not MedicalVendor medicalVendor || GameContext.HasInstance == false)
			return;

		GameContext.Instance.MedicalSvc?.ProcessSubscription(medicalVendor);
	}
}
