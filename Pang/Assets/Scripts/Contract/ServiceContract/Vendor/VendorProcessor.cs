
public abstract class VendorProcessor
{
	public abstract VendorType Type { get; }
	public abstract void ProcessVendor(VendorRuntime vendor);
}
