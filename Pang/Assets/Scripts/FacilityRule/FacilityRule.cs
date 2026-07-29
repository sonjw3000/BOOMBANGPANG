using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class FacilityItemRule
{
	[SerializeField] private ItemTag requiredItemTags = ItemTag.None;
	[SerializeField] private ItemTag forbiddenItemTags = ItemTag.None;
	[SerializeField] private ItemStatus requiredItemStatus = ItemStatus.None;
	[SerializeField] private List<ItemDefinition> whiteList = new();
	[SerializeField] private List<ItemDefinition> blackList = new();

	public FacilityItemRule()
	{
	}

	public FacilityItemRule(FacilityItemRule source)
	{
		if (source == null)
			return;

		requiredItemTags = source.requiredItemTags;
		forbiddenItemTags = source.forbiddenItemTags;
		requiredItemStatus = source.requiredItemStatus;
		whiteList = source.whiteList != null ? new List<ItemDefinition>(source.whiteList) : new List<ItemDefinition>();
		blackList = source.blackList != null ? new List<ItemDefinition>(source.blackList) : new List<ItemDefinition>();
	}

	public ItemTag RequiredItemTags => requiredItemTags;
	public ItemTag ForbiddenItemTags => forbiddenItemTags;
	public ItemStatus RequiredItemStatus => requiredItemStatus;
	public IReadOnlyList<ItemDefinition> WhiteList => whiteList;
	public IReadOnlyList<ItemDefinition> BlackList => blackList;

	public bool IsEmpty =>
		requiredItemTags == ItemTag.None &&
		forbiddenItemTags == ItemTag.None &&
		requiredItemStatus == ItemStatus.None &&
		(whiteList == null || whiteList.Count == 0) &&
		(blackList == null || blackList.Count == 0);

	public void SetRequiredItemTags(ItemTag tags) => requiredItemTags = tags;
	public void SetForbiddenItemTags(ItemTag tags) => forbiddenItemTags = tags;
	public void SetRequiredItemStatus(ItemStatus status) => requiredItemStatus = status;

	public void SetWhiteList(IEnumerable<ItemDefinition> items)
	{
		whiteList = items != null ? new List<ItemDefinition>(items) : new List<ItemDefinition>();
	}

	public void SetBlackList(IEnumerable<ItemDefinition> items)
	{
		blackList = items != null ? new List<ItemDefinition>(items) : new List<ItemDefinition>();
	}

	public void Clear()
	{
		requiredItemTags = ItemTag.None;
		forbiddenItemTags = ItemTag.None;
		requiredItemStatus = ItemStatus.None;
		whiteList?.Clear();
		blackList?.Clear();
	}

	public bool IsItemCapable(FacilityItemFilter filter)
	{
		if (IsEmpty || filter == null)
			return true;

		if (whiteList != null && whiteList.Count != 0 && filter.ItemSet != null)
		{
			foreach (ItemDefinition item in filter.ItemSet)
			{
				if (item != null && whiteList.Contains(item) == false)
					return false;
			}
		}

		if (blackList != null && blackList.Count != 0 && filter.ItemSet != null)
		{
			foreach (ItemDefinition item in filter.ItemSet)
			{
				if (item != null && blackList.Contains(item))
					return false;
			}
		}

		if ((filter.TagFilter & requiredItemTags) != requiredItemTags)
			return false;

		if ((filter.TagFilter & forbiddenItemTags) != ItemTag.None)
			return false;

		if (requiredItemStatus != ItemStatus.None && filter.ContainsStatus(requiredItemStatus) == false)
			return false;

		return true;
	}

	public bool IsItemDefinitionCapable(ItemDefinition item)
	{
		// Definition-level diagnostics intentionally exclude runtime ItemStatus conditions.
		if (item == null)
			return false;

		if (whiteList != null && whiteList.Count != 0 && whiteList.Contains(item) == false)
			return false;

		if (blackList != null && blackList.Contains(item))
			return false;

		if ((item.Tag & requiredItemTags) != requiredItemTags)
			return false;

		if ((item.Tag & forbiddenItemTags) != ItemTag.None)
			return false;

		return true;
	}
}

[Serializable]
public sealed class FacilityWorkerRule
{
	[SerializeField] private WorkerKind requiredWorkerKind = WorkerKind.None;
	[SerializeField] private List<HumanType> requiredHumanTypes = new();
	[SerializeField] private List<HumanType> forbiddenHumanTypes = new();
	[SerializeField] private List<RobotType> requiredRobotTypes = new();
	[SerializeField] private List<RobotType> forbiddenRobotTypes = new();
	[SerializeField] private WorkerAbility requiredWorkerAbility = WorkerAbility.None;

	public FacilityWorkerRule()
	{
	}

	public FacilityWorkerRule(FacilityWorkerRule source)
	{
		if (source == null)
			return;

		requiredWorkerKind = source.requiredWorkerKind;
		requiredHumanTypes = source.requiredHumanTypes != null ? new List<HumanType>(source.requiredHumanTypes) : new List<HumanType>();
		forbiddenHumanTypes = source.forbiddenHumanTypes != null ? new List<HumanType>(source.forbiddenHumanTypes) : new List<HumanType>();
		requiredRobotTypes = source.requiredRobotTypes != null ? new List<RobotType>(source.requiredRobotTypes) : new List<RobotType>();
		forbiddenRobotTypes = source.forbiddenRobotTypes != null ? new List<RobotType>(source.forbiddenRobotTypes) : new List<RobotType>();
		requiredWorkerAbility = source.requiredWorkerAbility;
	}

	public WorkerKind RequiredWorkerKind => requiredWorkerKind;
	public IReadOnlyList<HumanType> RequiredHumanTypes => requiredHumanTypes;
	public IReadOnlyList<HumanType> ForbiddenHumanTypes => forbiddenHumanTypes;
	public IReadOnlyList<RobotType> RequiredRobotTypes => requiredRobotTypes;
	public IReadOnlyList<RobotType> ForbiddenRobotTypes => forbiddenRobotTypes;
	public WorkerAbility RequiredWorkerAbility => requiredWorkerAbility;

	public bool IsEmpty =>
		requiredWorkerKind == WorkerKind.None &&
		requiredWorkerAbility == WorkerAbility.None &&
		(requiredHumanTypes == null || requiredHumanTypes.Count == 0) &&
		(forbiddenHumanTypes == null || forbiddenHumanTypes.Count == 0) &&
		(requiredRobotTypes == null || requiredRobotTypes.Count == 0) &&
		(forbiddenRobotTypes == null || forbiddenRobotTypes.Count == 0);

	public void SetRequiredWorkerKind(WorkerKind workerKind) => requiredWorkerKind = workerKind;
	public void SetRequiredWorkerAbility(WorkerAbility workerAbility) => requiredWorkerAbility = workerAbility;

	public void SetRequiredHumanTypes(IEnumerable<HumanType> humanTypes)
	{
		requiredHumanTypes = humanTypes != null ? new List<HumanType>(humanTypes) : new List<HumanType>();
	}

	public void SetForbiddenHumanTypes(IEnumerable<HumanType> humanTypes)
	{
		forbiddenHumanTypes = humanTypes != null ? new List<HumanType>(humanTypes) : new List<HumanType>();
	}

	public void SetRequiredRobotTypes(IEnumerable<RobotType> robotTypes)
	{
		requiredRobotTypes = robotTypes != null ? new List<RobotType>(robotTypes) : new List<RobotType>();
	}

	public void SetForbiddenRobotTypes(IEnumerable<RobotType> robotTypes)
	{
		forbiddenRobotTypes = robotTypes != null ? new List<RobotType>(robotTypes) : new List<RobotType>();
	}

	public void Clear()
	{
		requiredWorkerKind = WorkerKind.None;
		requiredWorkerAbility = WorkerAbility.None;
		requiredHumanTypes?.Clear();
		forbiddenHumanTypes?.Clear();
		requiredRobotTypes?.Clear();
		forbiddenRobotTypes?.Clear();
	}

	public bool IsWorkerCapable(FacilityWorkerFilter filter)
	{
		if (IsEmpty || filter == null || filter.Worker == null)
			return true;

		AIWorker worker = filter.Worker;
		if (requiredWorkerKind != WorkerKind.None && worker.WorkerKind != requiredWorkerKind)
			return false;

		if (requiredWorkerAbility != WorkerAbility.None && worker.HasAbility(requiredWorkerAbility) == false)
			return false;

		if (worker.WorkerKind == WorkerKind.Human)
		{
			if (requiredHumanTypes != null && requiredHumanTypes.Count != 0 && requiredHumanTypes.Contains(worker.HumanType) == false)
				return false;

			if (forbiddenHumanTypes != null && forbiddenHumanTypes.Contains(worker.HumanType))
				return false;

			return true;
		}

		if (worker.WorkerKind == WorkerKind.Robot)
		{
			if (requiredRobotTypes != null && requiredRobotTypes.Count != 0 && requiredRobotTypes.Contains(worker.RobotType) == false)
				return false;

			if (forbiddenRobotTypes != null && forbiddenRobotTypes.Contains(worker.RobotType))
				return false;
		}

		return true;
	}
}

[Serializable]
public sealed class FacilityManifestRule
{
	[SerializeField] private List<OrderDestination> requiredDestinations = new();

	public FacilityManifestRule()
	{
	}

	public FacilityManifestRule(FacilityManifestRule source)
	{
		if (source == null)
			return;

		requiredDestinations = source.requiredDestinations != null
			? new List<OrderDestination>(source.requiredDestinations)
			: new List<OrderDestination>();
	}

	public IReadOnlyList<OrderDestination> RequiredDestinations => requiredDestinations;

	public bool IsEmpty => requiredDestinations == null || requiredDestinations.Count == 0;

	public void SetRequiredDestinations(IEnumerable<OrderDestination> destinations)
	{
		requiredDestinations = destinations != null
			? new List<OrderDestination>(destinations)
			: new List<OrderDestination>();
	}

	public void Clear()
	{
		requiredDestinations?.Clear();
	}

	public bool IsManifestCapable(FacilityManifestFilter filter)
	{
		if (IsEmpty || filter == null)
			return true;

		if (filter.Destinations == null || filter.Destinations.Count == 0)
			return false;

		for (int i = 0; i < requiredDestinations.Count; ++i)
		{
			if (filter.ContainsDestination(requiredDestinations[i]))
				return true;
		}

		return false;
	}
}

[Serializable]
public sealed class FacilityRule
{
	[SerializeField] private int priority;
	[SerializeField] private FacilityItemRule itemRule = new();
	[SerializeField] private FacilityWorkerRule workerRule = new();
	[SerializeField] private FacilityManifestRule manifestRule = new();

	public FacilityRule()
	{
	}

	public FacilityRule(FacilityRule source)
	{
		if (source == null)
			return;

		priority = source.priority;
		itemRule = source.itemRule != null ? new FacilityItemRule(source.itemRule) : new FacilityItemRule();
		workerRule = source.workerRule != null ? new FacilityWorkerRule(source.workerRule) : new FacilityWorkerRule();
		manifestRule = source.manifestRule != null ? new FacilityManifestRule(source.manifestRule) : new FacilityManifestRule();
	}

	public int Priority => priority;
	public FacilityItemRule ItemRule => itemRule;
	public FacilityWorkerRule WorkerRule => workerRule;
	public FacilityManifestRule ManifestRule => manifestRule;

	public bool IsEmpty =>
		(itemRule == null || itemRule.IsEmpty) &&
		(workerRule == null || workerRule.IsEmpty) &&
		(manifestRule == null || manifestRule.IsEmpty);

	public void SetPriority(int newPriority) => priority = newPriority;

	public void SetItemRule(FacilityItemRule rule)
	{
		itemRule = rule != null ? new FacilityItemRule(rule) : new FacilityItemRule();
	}

	public void SetWorkerRule(FacilityWorkerRule rule)
	{
		workerRule = rule != null ? new FacilityWorkerRule(rule) : new FacilityWorkerRule();
	}

	public void SetManifestRule(FacilityManifestRule rule)
	{
		manifestRule = rule != null ? new FacilityManifestRule(rule) : new FacilityManifestRule();
	}

	public void Clear()
	{
		priority = 0;
		itemRule ??= new FacilityItemRule();
		workerRule ??= new FacilityWorkerRule();
		manifestRule ??= new FacilityManifestRule();
		itemRule.Clear();
		workerRule.Clear();
		manifestRule.Clear();
	}

	public bool IsFilterCapable(in FacilityFilter filter)
	{
		if (itemRule != null && itemRule.IsItemCapable(filter.ItemFilter) == false)
			return false;

		if (workerRule != null && workerRule.IsWorkerCapable(filter.WorkerFilter) == false)
			return false;

		if (manifestRule != null && manifestRule.IsManifestCapable(filter.ManifestFilter) == false)
			return false;

		return true;
	}

	public bool IsItemDefinitionCapable(ItemDefinition item)
	{
		return itemRule == null || itemRule.IsItemDefinitionCapable(item);
	}
}
