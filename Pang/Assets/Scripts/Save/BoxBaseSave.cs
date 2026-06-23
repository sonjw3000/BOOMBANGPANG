using System.Collections.Generic;

public abstract partial class BoxBase
{
	public virtual BoxSaveData CaptureState(System.Func<OrderLine, int> registerOrderLine)
	{
		BoxSaveData data = new BoxSaveData
		{
			BoxId = boxId,
			BoxType = boxType,
			ConcreteType = GetType().Name,
		};

		foreach (var stack in stacks)
		{
			data.Stacks.Add(new ItemStackSaveData
			{
				ItemId = stack.ItemID,
				Quantity = stack.Quantity,
				Freshness = stack.Freshness,
				Damage = stack.Damage,
				Status = stack.Status,
				RelatedOrderLineId = registerOrderLine != null && stack.RelatedOrderLine != null ? registerOrderLine(stack.RelatedOrderLine) : -1,
				OutboundStage = stack.OutboundStage,
			});
		}

		return data;
	}

	public virtual void RestoreState(BoxSaveData data, IReadOnlyDictionary<int, OrderLine> orderLines)
	{
		ResetContainer();
		if (data == null)
			return;

		foreach (var stackData in data.Stacks)
		{
			OrderLine line = null;
			if (orderLines != null && stackData.RelatedOrderLineId >= 0)
				orderLines.TryGetValue(stackData.RelatedOrderLineId, out line);

			ItemStack stack = ItemStack.Rent(stackData.ItemId, stackData.Freshness, stackData.Damage, stackData.Status, line, stackData.OutboundStage);
			stack.AddItem(stackData.Quantity);
			AddStack(stack);
			if (stack.Quantity <= 0)
				stack.Recycle();
		}
	}
}
