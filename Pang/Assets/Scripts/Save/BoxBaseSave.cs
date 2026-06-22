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
			if (stack is ItemPackage pkg)
			{
				data.Stacks.Add(new ItemStackSaveData
				{
					ItemId = pkg.ItemID,
					Quantity = pkg.Quantity,
					IsPackage = true,
					RelatedOrderLineId = registerOrderLine != null ? registerOrderLine(pkg.RelatedOrderLine) : -1,
					OutboundStage = pkg.OutboundStage,
				});
			}
			else
			{
				data.Stacks.Add(new ItemStackSaveData
				{
					ItemId = stack.ItemID,
					Quantity = stack.Quantity,
				});
			}
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
			if (stackData.IsPackage &&
				orderLines != null &&
				orderLines.TryGetValue(stackData.RelatedOrderLineId, out var line))
			{
				AddStack(new ItemPackage(PackingType.Box, line, stackData.ItemId, stackData.Quantity, stackData.OutboundStage));
			}
			else
			{
				AddItem(stackData.ItemId, stackData.Quantity);
			}
		}
	}
}
