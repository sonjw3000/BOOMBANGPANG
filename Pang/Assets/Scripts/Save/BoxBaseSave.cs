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

		if (this is CargoCapsule capsule)
			data.CapsuleLogisticsState = capsule.LogisticsState;

		foreach (var stack in stacks)
		{
			data.Stacks.Add(new ItemStackSaveData
			{
				ItemId = stack.ItemID,
				Quantity = stack.Quantity,
				Freshness = stack.Freshness,
				Damage = stack.Damage,
				Status = stack.Status,
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

		if (this is CargoCapsule capsule)
			capsule.SetLogisticsState(data.CapsuleLogisticsState);

		foreach (var stackData in data.Stacks)
		{
			ItemStack stack = ItemStack.Rent(stackData.ItemId, stackData.Freshness, stackData.Damage, stackData.Status, stackData.OutboundStage);
			stack.AddItem(stackData.Quantity);
			AddStack(stack);
			if (stack.Quantity <= 0)
				stack.Recycle();
		}
	}
}
