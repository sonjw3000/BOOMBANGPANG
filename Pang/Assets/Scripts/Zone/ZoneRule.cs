using System.Collections.Generic;
using Unity.VisualScripting;


public class ZoneItemRule
{
	private ItemTag requiredItemTags;
	private ItemTag forbiddenItemTags;

	private HashSet<ItemDefinition> whiteList = new();
	private HashSet<ItemDefinition> blackList = new();

	public ItemTag RequiredItemTags => requiredItemTags;
	public ItemTag ForbiddenItemTags => forbiddenItemTags;

	public IReadOnlyCollection<ItemDefinition> WhiteList => whiteList;
	public IReadOnlyCollection<ItemDefinition> BlackList => blackList;


	public bool IsItemCapable(in ZoneItemFilter filter)
	{
		if (whiteList.Count != 0)
		{
			foreach (var item in filter.ItemSet)
			{
				if (whiteList.Contains(item) == false)
					return false;
			}
		}

		if (blackList.Count != 0)
		{
			foreach (var item in filter.ItemSet)
			{
				if (blackList.Contains(item))
					return false;
			}
		}

		if (filter.TagFilter.HasFlag(requiredItemTags) == false)
			return false;

		if (filter.TagFilter.HasFlag(forbiddenItemTags))
			return false;

		return true;
	}
}

public class ZoneWorkerRule
{
	public WorkerKind requiredWorkerKinds = WorkerKind.None;

	public HashSet<HumanType> requiredHumanTypes;
	public HashSet<HumanType> forbiddenHumanTypes;
	public HashSet<RobotType> requiredRobotTypes;
	public HashSet<RobotType> forbiddenRobotTypes;

	public WorkerAbility requiredWorkerAbility;

	public bool IsWorkerCapable(in ZoneWorkerFilter filter)
	{
		if (requiredWorkerKinds != WorkerKind.None && filter.Worker.WorkerKind != requiredWorkerKinds)
			return false;
		
		if (filter.Worker.WorkerKind == WorkerKind.Human)
		{
			if (requiredHumanTypes.Contains(filter.Worker.HumanType) == false)
				return false;
			if (forbiddenHumanTypes.Contains(filter.Worker.HumanType))
				return false;
		}
		else
		{
			if (requiredRobotTypes.Contains(filter.Worker.RobotType) == false)
				return false;
			if (forbiddenRobotTypes.Contains(filter.Worker.RobotType))
				return false;
		}

		return true;
	}
}

public class ZoneRule
{
	// 판단을 위한 우선순위
	public int priority;

	// 제약조건
	public ZoneItemRule itemRule;
	public ZoneWorkerRule workerRule;

	public bool IsFilterCapable(in ZoneFilter filter)
	{
		if (itemRule.IsItemCapable(filter.ItemFilter) == false)
			return false;
		
		if (workerRule.IsWorkerCapable(filter.WorkerFilter) == false)
			return false;

		return true;
	}
}
