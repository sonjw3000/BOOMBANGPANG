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

public readonly struct FacilityFilter
{
	public static FacilityFilter None => default;

	public FacilityItemFilter ItemFilter { get; }
	public FacilityWorkerFilter WorkerFilter { get; }

	public FacilityFilter(FacilityItemFilter itemFilter = null, FacilityWorkerFilter workerFilter = null)
	{
		ItemFilter = itemFilter;
		WorkerFilter = workerFilter;
	}

	public bool Matches(FacilityRuleManager ruleManager, IFacility facility)
	{
		if (facility == null)
			return false;

		if (facility.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId)
			return true;

		return ruleManager != null && ruleManager.IsFacilityAllowed(facility, this);
	}

	public static FacilityFilter ForWorker(AIWorker worker)
	{
		return worker == null
			? None
			: new FacilityFilter(workerFilter: new FacilityWorkerFilter(worker));
	}

	public static FacilityFilter ForContainer(IItemContainer container, AIWorker worker = null)
	{
		FacilityItemFilter itemFilter = null;
		if (TryBuildItemFilter(container, out FacilityItemFilter builtFilter))
			itemFilter = builtFilter;

		return new FacilityFilter(
			itemFilter,
			worker != null ? new FacilityWorkerFilter(worker) : null);
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
}
