using System.Collections.Generic;

// 현재는 그냥 아이디어가 떠올라서 개념만 잡아둔 상태임
// todo
// 이를 실제로 활용하여보자

public class ZoneRule
{
	// 판단을 위한 우선순위
	public int priority;

	// 제약조건
	public List<ItemTag> requiredTags;
	public List<ItemTag> forbiddenTags;

	public List<WorkerKind> requiredWorkerKinds;
	public List<WorkerKind> forbiddenWorkerKinds;
	public List<HumanType> requiredHumanTypes;
	public List<HumanType> forbiddenHumanTypes;
	public List<RobotType> requiredRobotTypes;
	public List<RobotType> forbiddenRobotTypes;

	public List<WorkerAbility> requiredWorkerAbilities;
	public List<WorkerAbility> forbiddenWorkerAbilities;

	public bool IsItemCapable(ItemDefinition item)
	{
		if (item == null)
			return false;

		requiredTags ??= new List<ItemTag>();
		forbiddenTags ??= new List<ItemTag>();

		foreach (var tag in requiredTags)
		{
			if (item.Tag.HasFlag(tag) == false)
				return false;
		}

		foreach (var tag in forbiddenTags)
		{
			if (item.Tag.HasFlag(tag))
				return false;
		}

		return true;
	}

	public bool IsWorkerCapable(AIWorker worker)
	{
		if (worker == null)
			return false;

		requiredWorkerKinds ??= new List<WorkerKind>();
		forbiddenWorkerKinds ??= new List<WorkerKind>();
		requiredHumanTypes ??= new List<HumanType>();
		forbiddenHumanTypes ??= new List<HumanType>();
		requiredRobotTypes ??= new List<RobotType>();
		forbiddenRobotTypes ??= new List<RobotType>();
		requiredWorkerAbilities ??= new List<WorkerAbility>();
		forbiddenWorkerAbilities ??= new List<WorkerAbility>();

		foreach (var workerKind in requiredWorkerKinds)
		{
			if (worker.WorkerKind != workerKind)
				return false;
		}

		foreach (var workerKind in forbiddenWorkerKinds)
		{
			if (worker.WorkerKind == workerKind)
				return false;
		}

		if (worker.WorkerKind == WorkerKind.Human)
		{
			foreach (var humanType in requiredHumanTypes)
			{
				if (worker.HumanType != humanType)
					return false;
			}

			foreach (var humanType in forbiddenHumanTypes)
			{
				if (worker.HumanType == humanType)
					return false;
			}
		}
		else
		{
			foreach (var robotType in requiredRobotTypes)
			{
				if (worker.RobotType != robotType)
					return false;
			}

			foreach (var robotType in forbiddenRobotTypes)
			{
				if (worker.RobotType == robotType)
					return false;
			}
		}

		foreach (var ability in requiredWorkerAbilities)
		{
			if (worker.HasAbility(ability) == false)
				return false;
		}

		foreach (var ability in forbiddenWorkerAbilities)
		{
			if (worker.HasAbility(ability))
				return false;
		}

		return true;
	}
}
