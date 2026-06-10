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

	public List<WorkerType> requiredWorkerTypes;
	public List<WorkerType> forbiddenWorkerTypes;

	public List<WorkerAbility> requiredWorkerAbilities;

	public bool IsItemCapable(ItemDefinition item)
	{
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
		// todo
		// worker의 타입과 능력을 보고 판단하자

		return true;
	}
}
