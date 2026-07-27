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
			FireIntensity = FireIntensity,
			HasTemperatureState = true,
			CurrentTemperatureCelsius = CurrentTemperatureCelsius,
		};

		if (this is CargoCapsule capsule)
			data.CapsuleLogisticsState = capsule.LogisticsState;

		foreach (var stack in stacks)
		{
			data.Stacks.Add(new ItemStackSaveData
			{
				ItemId = stack.ItemID,
				Quantity = stack.Quantity,
				CurrentFreshness = stack.CurrentFreshness,
				CurrentIntegrity = stack.CurrentIntegrity,
				HasTemperatureState = true,
				CurrentTemperatureCelsius = stack.CurrentTemperatureCelsius,
				Status = stack.Status,
				OutboundStage = stack.OutboundStage,
				Quality = stack.Quality,
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
		SetFireIntensity(data.FireIntensity);
		SetCurrentTemperatureCelsius(
			data.HasTemperatureState
				? data.CurrentTemperatureCelsius
				: GridCell.DefaultTemperatureCelsius);

		foreach (var stackData in data.Stacks)
		{
			ItemStack stack = ItemStack.Rent(
				stackData.ItemId,
				stackData.CurrentFreshness,
				stackData.CurrentIntegrity,
				stackData.Status,
				stackData.OutboundStage,
				stackData.Quality,
				stackData.HasTemperatureState
					? stackData.CurrentTemperatureCelsius
					: GridCell.DefaultTemperatureCelsius);
			stack.AddItem(stackData.Quantity);
			AddStack(stack);
			if (stack.Quantity <= 0)
				stack.Recycle();
		}
	}
}
