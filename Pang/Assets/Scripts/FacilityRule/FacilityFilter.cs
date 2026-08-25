using System;
using System.Collections.Generic;

public sealed class FacilityItemFilter
{
	private readonly HashSet<ItemDefinition> itemSet;
	private readonly HashSet<ItemStatus> statusSet;

	public ItemTag TagFilter { get; }
	public IReadOnlyCollection<ItemDefinition> ItemSet => itemSet;
	public IReadOnlyCollection<ItemStatus> StatusSet => statusSet;

	public FacilityItemFilter(
		ItemTag tagFilter,
		IEnumerable<ItemDefinition> itemSet = null,
		IEnumerable<ItemStatus> statusSet = null)
	{
		TagFilter = tagFilter;
		this.itemSet = itemSet != null ? new HashSet<ItemDefinition>(itemSet) : null;
		this.statusSet = statusSet != null ? new HashSet<ItemStatus>(statusSet) : null;
	}

	public bool ContainsStatus(ItemStatus status)
	{
		return statusSet != null && statusSet.Contains(status);
	}
}

public sealed class FacilityWorkerFilter
{
	public AIWorker Worker { get; }

	public FacilityWorkerFilter(AIWorker worker)
	{
		Worker = worker;
	}
}

public sealed class FacilityManifestFilter
{
	private readonly HashSet<OrderDestination> destinations;

	public IReadOnlyCollection<OrderDestination> Destinations => destinations;

	public FacilityManifestFilter(IEnumerable<OrderDestination> destinations = null)
	{
		this.destinations = destinations != null ? new HashSet<OrderDestination>(destinations) : null;
	}

	public bool ContainsDestination(OrderDestination destination)
	{
		return destinations != null && destinations.Contains(destination);
	}

	public static FacilityManifestFilter FromOrderLine(OrderLine orderLine)
	{
		return new FacilityManifestFilter(new[]
		{
			ResolveDestination(orderLine),
		});
	}

	public static FacilityManifestFilter FromManifest(PickingManifest manifest, uint itemId = 0)
	{
		HashSet<OrderDestination> resolvedDestinations = null;
		IReadOnlyList<PickingManifestLine> lines = manifest?.Lines;
		if (lines != null)
		{
			for (int i = 0; i < lines.Count; ++i)
			{
				PickingManifestLine line = lines[i];
				if (line == null || line.PickedQuantity <= 0)
					continue;

				if (itemId != 0 && line.ItemId != itemId)
					continue;

				resolvedDestinations ??= new HashSet<OrderDestination>();
				resolvedDestinations.Add(ResolveDestination(line.OrderLine));
			}
		}

		return new FacilityManifestFilter(resolvedDestinations ?? new HashSet<OrderDestination>
		{
			OrderDestination.None,
		});
	}

	private static OrderDestination ResolveDestination(OrderLine orderLine)
	{
		return orderLine?.ParentOrder != null
			? orderLine.ParentOrder.Destination
			: OrderDestination.None;
	}
}

public readonly struct FacilityFilter
{
	public static FacilityFilter None => default;

	public FacilityItemFilter ItemFilter { get; }
	public FacilityWorkerFilter WorkerFilter { get; }
	public FacilityManifestFilter ManifestFilter { get; }
	public ItemProcessStage ItemProcessStage { get; }
	public FacilityContentState ContentState { get; }

	public FacilityFilter(
		FacilityItemFilter itemFilter = null,
		FacilityWorkerFilter workerFilter = null,
		FacilityManifestFilter manifestFilter = null,
		ItemProcessStage itemProcessStage = ItemProcessStage.Any,
		FacilityContentState contentState = FacilityContentState.Any)
	{
		ItemFilter = itemFilter;
		WorkerFilter = workerFilter;
		ManifestFilter = manifestFilter;
		ItemProcessStage = ItemProcessStageUtility.IsDefined(itemProcessStage)
			? itemProcessStage
			: ItemProcessStage.Any;
		ContentState = contentState is FacilityContentState.Any or
			FacilityContentState.HasItems or
			FacilityContentState.Empty
			? contentState
			: FacilityContentState.Any;
	}

	public bool Matches(FacilityRuleManager ruleManager, IFacility facility)
	{
		if (facility == null)
			return false;

		if (facility.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId)
			return true;

		return ruleManager != null && ruleManager.IsFacilityAllowed(facility, this);
	}

	public bool MatchesCurrentRules(IFacility facility)
	{
		if (facility == null)
			return false;

		if (facility.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId)
			return true;

		FacilityRuleManager ruleManager = GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;
		return Matches(ruleManager, facility);
	}

	public static FacilityFilter ForWorker(AIWorker worker)
	{
		return worker == null
			? None
			: new FacilityFilter(workerFilter: new FacilityWorkerFilter(worker));
	}

	public static FacilityFilter ForContainer(IItemContainer container, AIWorker worker = null)
	{
		return TryForContainer(
			container,
			manifest: null,
			launchReady: false,
			out FacilityFilter filter,
			worker)
			? filter
			: None;
	}

	public static bool TryForContainer(
		IItemContainer container,
		PickingManifest manifest,
		bool launchReady,
		out FacilityFilter filter,
		AIWorker worker = null)
	{
		filter = None;
		if (container == null)
			return false;

		FacilityContentState contentState = HasCargo(container)
			? FacilityContentState.HasItems
			: FacilityContentState.Empty;
		if (contentState == FacilityContentState.Empty)
		{
			filter = new FacilityFilter(
				workerFilter: worker != null ? new FacilityWorkerFilter(worker) : null,
				contentState: contentState);
			return true;
		}

		ItemProcessStage stage = ItemProcessStage.Any;
		ItemProcessStageEvaluator.TryEvaluate(
			container,
			manifest,
			launchReady,
			out stage);

		FacilityItemFilter itemFilter = null;
		if (TryBuildItemFilter(container, out FacilityItemFilter builtFilter))
			itemFilter = builtFilter;

		filter = new FacilityFilter(
			itemFilter,
			worker != null ? new FacilityWorkerFilter(worker) : null,
			FacilityManifestFilter.FromManifest(manifest),
			stage,
			contentState);
		return true;
	}

	public static bool TryForCapsule(
		CargoCapsule capsule,
		bool evaluateLaunchReadiness,
		out FacilityFilter filter,
		AIWorker worker = null)
	{
		filter = None;
		if (capsule == null)
			return false;

		OutboundWorkflowService outbound = GameContext.HasInstance
			? GameContext.Instance.OBWorkflowSvc
			: null;
		bool launchReady = evaluateLaunchReadiness &&
			ItemProcessStageEvaluator.IsLaunchReady(capsule, outbound);
		PickingManifest manifest = null;
		outbound?.TryGetPickingManifest(capsule, out manifest);
		return TryForContainer(capsule, manifest, launchReady, out filter, worker);
	}

	public static FacilityFilter ForTransfer(
		IItemContainer source,
		uint itemId,
		int quantity,
		Predicate<ItemStack> stackPredicate = null,
		AIWorker worker = null)
	{
		FacilityItemFilter itemFilter = null;
		if (TryBuildTransferItemFilter(source, itemId, quantity, stackPredicate, out FacilityItemFilter builtFilter))
			itemFilter = builtFilter;

		return new FacilityFilter(
			itemFilter,
			worker != null ? new FacilityWorkerFilter(worker) : null);
	}

	public static FacilityFilter ForManifestTransfer(
		IItemContainer source,
		PickingManifest manifest,
		uint itemId,
		int quantity,
		Predicate<ItemStack> stackPredicate = null,
		AIWorker worker = null)
	{
		return WithManifest(
			ForTransfer(source, itemId, quantity, stackPredicate, worker),
			FacilityManifestFilter.FromManifest(manifest, itemId));
	}

	public static FacilityFilter WithManifest(
		FacilityFilter source,
		FacilityManifestFilter manifestFilter)
	{
		return new FacilityFilter(
			source.ItemFilter,
			source.WorkerFilter,
			manifestFilter,
			source.ItemProcessStage,
			source.ContentState);
	}

	public static FacilityFilter WithItemProcessStage(
		FacilityFilter source,
		ItemProcessStage itemProcessStage)
	{
		return new FacilityFilter(
			source.ItemFilter,
			source.WorkerFilter,
			source.ManifestFilter,
			itemProcessStage,
			source.ContentState);
	}

	public static FacilityFilter WithContentState(
		FacilityFilter source,
		FacilityContentState contentState)
	{
		return new FacilityFilter(
			source.ItemFilter,
			source.WorkerFilter,
			source.ManifestFilter,
			source.ItemProcessStage,
			contentState);
	}

	private static bool HasCargo(IItemContainer container)
	{
		if (container?.Stacks == null)
			return false;

		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			if (container.Stacks[i]?.Quantity > 0)
				return true;
		}

		return false;
	}

	private static bool TryBuildItemFilter(IItemContainer container, out FacilityItemFilter itemFilter)
	{
		itemFilter = null;
		if (container == null || GameContext.HasInstance == false || GameContext.Instance.ItemDB == null)
			return false;

		HashSet<ItemDefinition> itemSet = null;
		HashSet<ItemStatus> statusSet = null;
		bool hasItems = false;
		foreach (var itemTotal in container.ItemTotals)
		{
			if (itemTotal.Value <= 0)
				continue;

			hasItems = true;
			if (GameContext.Instance.ItemDB.GetItemData(itemTotal.Key, out ItemDefinition itemDefinition) == false || itemDefinition == null)
				continue;

			itemSet ??= new HashSet<ItemDefinition>();
			itemSet.Add(itemDefinition);
		}

		if (container.Stacks != null)
		{
			for (int i = 0; i < container.Stacks.Count; ++i)
			{
				ItemStack stack = container.Stacks[i];
				if (stack == null || stack.Quantity <= 0)
					continue;

				hasItems = true;
				statusSet ??= new HashSet<ItemStatus>();
				statusSet.Add(stack.Status);
			}
		}

		if (hasItems == false)
			return false;

		itemFilter = new FacilityItemFilter(container.ItemTags, itemSet, statusSet);
		return true;
	}

	private static bool TryBuildTransferItemFilter(
		IItemContainer source,
		uint itemId,
		int quantity,
		Predicate<ItemStack> stackPredicate,
		out FacilityItemFilter itemFilter)
	{
		itemFilter = null;
		if (source == null || source.Stacks == null || itemId == 0 || quantity <= 0 ||
			GameContext.HasInstance == false || GameContext.Instance.ItemDB == null)
		{
			return false;
		}

		HashSet<ItemDefinition> itemSet = null;
		HashSet<ItemStatus> statusSet = null;
		ItemTag tagFilter = ItemTag.None;
		bool hasItems = false;
		int remaining = quantity;

		for (int i = source.Stacks.Count - 1; i >= 0 && remaining > 0; --i)
		{
			ItemStack stack = source.Stacks[i];
			if (stack == null ||
				stack.Quantity <= 0 ||
				stack.HasItemID(itemId) == false ||
				(stackPredicate != null && stackPredicate(stack) == false))
			{
				continue;
			}

			hasItems = true;
			remaining -= Math.Min(stack.Quantity, remaining);

			statusSet ??= new HashSet<ItemStatus>();
			statusSet.Add(stack.Status);

			if (GameContext.Instance.ItemDB.GetItemData(stack.ItemID, out ItemDefinition itemDefinition) == false || itemDefinition == null)
				continue;

			itemSet ??= new HashSet<ItemDefinition>();
			itemSet.Add(itemDefinition);
			tagFilter |= itemDefinition.Tag;
		}

		if (hasItems == false)
			return false;

		itemFilter = new FacilityItemFilter(tagFilter, itemSet, statusSet);
		return true;
	}
}
