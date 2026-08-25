using System.Collections.Generic;

public enum CargoProcessStage
{
	None = 0,
	Unlabeled = 1,
	Labeled = 2,
	Picked = 3,
	Packed = 4,
	LaunchReady = 5,
}

public static class CargoProcessStageUtility
{
	public static bool IsDefined(CargoProcessStage stage)
	{
		return stage is CargoProcessStage.None or
			CargoProcessStage.Unlabeled or
			CargoProcessStage.Labeled or
			CargoProcessStage.Picked or
			CargoProcessStage.Packed or
			CargoProcessStage.LaunchReady;
	}

	public static string ToDisplayString(CargoProcessStage stage)
	{
		return stage switch
		{
			CargoProcessStage.None => "None",
			CargoProcessStage.Unlabeled => "Unlabeled",
			CargoProcessStage.Labeled => "Labeled",
			CargoProcessStage.Picked => "Picked",
			CargoProcessStage.Packed => "Packed",
			CargoProcessStage.LaunchReady => "Launch Ready",
			_ => stage.ToString(),
		};
	}
}

public static class CargoProcessStageEvaluator
{
	public static bool TryEvaluate(
		CargoCapsule capsule,
		OutboundWorkflowService outboundWorkflow,
		bool launchReady,
		out CargoProcessStage stage)
	{
		stage = CargoProcessStage.None;
		if (capsule == null)
			return false;

		PickingManifest manifest = null;
		outboundWorkflow?.TryGetPickingManifest(capsule, out manifest);
		return TryEvaluate(capsule.Stacks, manifest, launchReady, out stage);
	}

	public static bool TryEvaluate(
		IItemContainer container,
		PickingManifest manifest,
		bool launchReady,
		out CargoProcessStage stage)
	{
		return TryEvaluate(container?.Stacks, manifest, launchReady, out stage);
	}

	public static bool TryEvaluate(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest,
		bool launchReady,
		out CargoProcessStage stage)
	{
		stage = CargoProcessStage.None;
		if (TryGetUniformStatus(stacks, out ItemStatus status) == false)
			return false;

		switch (status)
		{
			case ItemStatus.None:
				if (manifest != null && manifest.IsEmpty == false)
					return false;

				stage = CargoProcessStage.Unlabeled;
				return true;

			case ItemStatus.Labeled:
				if (manifest == null || manifest.IsEmpty)
				{
					stage = CargoProcessStage.Labeled;
					return true;
				}

				if (HasCompletePickedManifest(stacks, manifest) == false)
					return false;

				stage = CargoProcessStage.Picked;
				return true;

			case ItemStatus.Packed:
				bool hasManifest = manifest != null && manifest.IsEmpty == false;
				bool hasCompletePackedManifest =
					hasManifest && HasCompletePackedManifest(stacks, manifest);
				if (hasManifest && hasCompletePackedManifest == false)
					return false;
				if (launchReady && hasCompletePackedManifest == false)
					return false;

				stage = launchReady
					? CargoProcessStage.LaunchReady
					: CargoProcessStage.Packed;
				return true;

			default:
				return false;
		}
	}

	public static bool Matches(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest,
		bool launchReady,
		CargoProcessStage requiredStage)
	{
		return requiredStage == CargoProcessStage.None ||
			(TryEvaluate(stacks, manifest, launchReady, out CargoProcessStage actualStage) &&
			 actualStage == requiredStage);
	}

	public static bool IsLaunchReady(
		CargoCapsule capsule,
		OutboundWorkflowService outboundWorkflow)
	{
		return capsule != null &&
			outboundWorkflow != null &&
			outboundWorkflow.HasDispatchBlockingCargo(capsule) == false &&
			outboundWorkflow.TryGetPickingManifest(capsule, out PickingManifest manifest) &&
			HasCompletePackedManifest(capsule.Stacks, manifest);
	}

	public static bool HasCompletePickedManifest(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest)
	{
		return HasCompleteManifest(stacks, manifest, ItemStatus.Labeled, requirePackedQuantity: false);
	}

	public static bool HasCompletePackedManifest(
		IItemContainer container,
		PickingManifest manifest)
	{
		return HasCompletePackedManifest(container?.Stacks, manifest);
	}

	public static bool HasCompletePackedManifest(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest)
	{
		return HasCompleteManifest(stacks, manifest, ItemStatus.Packed, requirePackedQuantity: true);
	}

	private static bool TryGetUniformStatus(
		IReadOnlyList<ItemStack> stacks,
		out ItemStatus status)
	{
		status = ItemStatus.NotDefined;
		if (stacks == null)
			return false;

		bool found = false;
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (stack.ItemID == 0 || stack.HasQuality(ItemQuality.Waste))
				return false;

			if (found && stack.Status != status)
				return false;

			status = stack.Status;
			found = true;
		}

		return found;
	}

	private static bool HasCompleteManifest(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest,
		ItemStatus requiredStatus,
		bool requirePackedQuantity)
	{
		if (stacks == null || manifest == null || manifest.IsEmpty)
			return false;

		Dictionary<uint, int> physicalQuantityByItemId = new();
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (stack.ItemID == 0 ||
				stack.Status != requiredStatus ||
				stack.HasQuality(ItemQuality.Waste))
			{
				return false;
			}

			physicalQuantityByItemId[stack.ItemID] =
				physicalQuantityByItemId.GetValueOrDefault(stack.ItemID) + stack.Quantity;
		}

		if (physicalQuantityByItemId.Count <= 0)
			return false;

		Dictionary<uint, int> manifestQuantityByItemId = new();
		for (int i = 0; i < manifest.Lines.Count; ++i)
		{
			PickingManifestLine line = manifest.Lines[i];
			if (line?.OrderLine == null ||
				line.ItemId == 0 ||
				line.OrderLine.ItemID != line.ItemId ||
				line.PickedQuantity <= 0)
			{
				return false;
			}

			int manifestQuantity;
			if (requirePackedQuantity)
			{
				if (line.PackedQuantity <= 0 || line.PickedQuantity != line.PackedQuantity)
					return false;

				manifestQuantity = line.PackedQuantity;
			}
			else
			{
				if (line.PackedQuantity != 0)
					return false;

				manifestQuantity = line.PickedQuantity;
			}

			manifestQuantityByItemId[line.ItemId] =
				manifestQuantityByItemId.GetValueOrDefault(line.ItemId) + manifestQuantity;
		}

		if (manifestQuantityByItemId.Count != physicalQuantityByItemId.Count)
			return false;

		foreach (var entry in physicalQuantityByItemId)
		{
			if (manifestQuantityByItemId.TryGetValue(entry.Key, out int manifestQuantity) == false ||
				manifestQuantity != entry.Value)
			{
				return false;
			}
		}

		return true;
	}
}
