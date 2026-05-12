using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Worker/RobotMarket")]
public class RobotMarketData : WorkforceMarketData_SO
{
	[SerializeField] private List<WorkerArchetype> archetypeList;

	public override int GetMaxCount() { return archetypeList.Count; }
	protected override void OnValidation()
	{
		foreach (var archetype in archetypeList)
		{
			archetype.WorkerBaseStat.baseMoveSpeedMultiplier = archetype.WorkerBaseStat.minimumMoveSpeedMultiplier;
			archetype.WorkerBaseStat.baseWorkSpeedMultiplier = archetype.WorkerBaseStat.minimumWorkSpeedMultiplier;
		}
	}

	public override void FillWorkerArchetype(WorkerArchetype target, System.Random rng, int page, int count)
	{
		int i = page * count + count;

		archetypeList[i].Duplicate(target);
	}
}
