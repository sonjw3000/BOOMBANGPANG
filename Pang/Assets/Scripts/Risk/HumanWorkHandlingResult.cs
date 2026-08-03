public readonly struct HumanWorkHandlingResult
{
	public readonly uint ItemId;
	public readonly int Quantity;
	public readonly float HandlingWeightKg;
	public readonly ItemTag ItemTags;
	public readonly IItemContainer Destination;

	public bool HasHandling => Quantity > 0 || HandlingWeightKg > 0.0f;
	public bool IsFragile => (ItemTags & ItemTag.Fragile) != 0;
	public bool IsDangerous => (ItemTags & ItemTag.Danger) != 0;

	public HumanWorkHandlingResult(
		uint itemId,
		int quantity,
		float handlingWeightKg,
		ItemTag itemTags,
		IItemContainer destination)
	{
		ItemId = itemId;
		Quantity = quantity;
		HandlingWeightKg = handlingWeightKg;
		ItemTags = itemTags;
		Destination = destination;
	}
}
