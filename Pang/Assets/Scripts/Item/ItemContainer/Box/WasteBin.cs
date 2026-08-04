public sealed class WasteBin : CargoCapsule
{
	public override CargoRouteKind RouteKind => CargoRouteKind.Waste;
	public bool IsFull => MaxSize > 0.0f && TotalSize >= MaxSize;

	public override int GetAcceptableQuantity(uint itemId, int requested)
	{
		// Item identity alone cannot prove that the incoming stack is waste.
		// Waste transfers use CanAcceptStack, which receives the full identity.
		return base.GetAcceptableQuantity(itemId, requested);
	}

	public override bool CanAcceptStack(ItemStack stack)
	{
		return stack?.HasQuality(ItemQuality.Waste) == true && base.CanAcceptStack(stack);
	}

	public override int AddItem(uint itemId, int quantity)
	{
		// Prevent callers from creating a default, non-waste stack in a waste bin.
		return 0;
	}
}
