using System;
using System.Collections.Generic;

public enum FacilityContentState
{
	Any = 0,
	HasItems = 1,
	Empty = 2,
}

public enum ItemProcessStage
{
	Any = 0,
	Unlabeled = 1,
	Labeled = 2,
	Picked = 3,
	Packed = 4,
	LaunchReady = 5,
}

[Flags]
public enum ItemProcessStageMask
{
	None = 0,
	Unlabeled = 1 << 0,
	Labeled = 1 << 1,
	Picked = 1 << 2,
	Packed = 1 << 3,
	LaunchReady = 1 << 4,
	All = Unlabeled | Labeled | Picked | Packed | LaunchReady,
}

public static class ItemProcessStageUtility
{
	public static bool IsDefined(ItemProcessStageMask stages)
	{
		return (stages & ~ItemProcessStageMask.All) == 0;
	}

	public static bool IsDefined(ItemProcessStage stage)
	{
		return stage is ItemProcessStage.Any or
			ItemProcessStage.Unlabeled or
			ItemProcessStage.Labeled or
			ItemProcessStage.Picked or
			ItemProcessStage.Packed or
			ItemProcessStage.LaunchReady;
	}

	public static ItemProcessStageMask ToMask(ItemProcessStage stage)
	{
		return stage switch
		{
			ItemProcessStage.Unlabeled => ItemProcessStageMask.Unlabeled,
			ItemProcessStage.Labeled => ItemProcessStageMask.Labeled,
			ItemProcessStage.Picked => ItemProcessStageMask.Picked,
			ItemProcessStage.Packed => ItemProcessStageMask.Packed,
			ItemProcessStage.LaunchReady => ItemProcessStageMask.LaunchReady,
			_ => ItemProcessStageMask.None,
		};
	}

	public static bool Contains(ItemProcessStageMask stages, ItemProcessStage stage)
	{
		ItemProcessStageMask stageMask = ToMask(stage);
		return stageMask != ItemProcessStageMask.None && (stages & stageMask) != 0;
	}

	public static string ToDisplayString(ItemProcessStageMask stages)
	{
		if (stages == ItemProcessStageMask.None)
			return "Any";

		List<string> names = new();
		AppendStageName(names, stages, ItemProcessStageMask.Unlabeled, ItemProcessStage.Unlabeled);
		AppendStageName(names, stages, ItemProcessStageMask.Labeled, ItemProcessStage.Labeled);
		AppendStageName(names, stages, ItemProcessStageMask.Picked, ItemProcessStage.Picked);
		AppendStageName(names, stages, ItemProcessStageMask.Packed, ItemProcessStage.Packed);
		AppendStageName(names, stages, ItemProcessStageMask.LaunchReady, ItemProcessStage.LaunchReady);
		return string.Join(", ", names);
	}

	public static string ToDisplayString(ItemProcessStage stage)
	{
		return stage switch
		{
			ItemProcessStage.Any => "Any",
			ItemProcessStage.Unlabeled => "Unlabeled",
			ItemProcessStage.Labeled => "Labeled",
			ItemProcessStage.Picked => "Picked",
			ItemProcessStage.Packed => "Packed",
			ItemProcessStage.LaunchReady => "Launch Ready",
			_ => stage.ToString(),
		};
	}

	private static void AppendStageName(
		List<string> names,
		ItemProcessStageMask stages,
		ItemProcessStageMask stageMask,
		ItemProcessStage stage)
	{
		if ((stages & stageMask) != 0)
			names.Add(ToDisplayString(stage));
	}
}

public static class ItemProcessStageEvaluator
{
	public static bool TryEvaluate(
		CargoCapsule capsule,
		OutboundWorkflowService outboundWorkflow,
		bool launchReady,
		out ItemProcessStage stage)
	{
		stage = ItemProcessStage.Any;
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
		out ItemProcessStage stage)
	{
		return TryEvaluate(container?.Stacks, manifest, launchReady, out stage);
	}

	public static bool TryEvaluate(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest,
		bool launchReady,
		out ItemProcessStage stage)
	{
		stage = ItemProcessStage.Any;
		if (TryGetUniformStatus(stacks, out ItemStatus status) == false)
			return false;

		switch (status)
		{
			case ItemStatus.None:
				if (manifest != null && manifest.IsEmpty == false)
					return false;

				stage = ItemProcessStage.Unlabeled;
				return true;

			case ItemStatus.Labeled:
				if (manifest == null || manifest.IsEmpty)
				{
					stage = ItemProcessStage.Labeled;
					return true;
				}

				if (HasCompletePickedManifest(stacks, manifest) == false)
					return false;

				stage = ItemProcessStage.Picked;
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
					? ItemProcessStage.LaunchReady
					: ItemProcessStage.Packed;
				return true;

			default:
				return false;
		}
	}

	public static bool Matches(
		IReadOnlyList<ItemStack> stacks,
		PickingManifest manifest,
		bool launchReady,
		ItemProcessStage requiredStage)
	{
		return requiredStage == ItemProcessStage.Any ||
			(TryEvaluate(stacks, manifest, launchReady, out ItemProcessStage actualStage) &&
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
