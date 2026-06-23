using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Worker/HumanMarket")]
public class HumanMarketData : WorkforceMarketData_SO
{
	[Header("Worker Visuals")]
	[SerializeField] protected List<WorkerNameDefinition> workerNames;
	[SerializeField] protected List<WorkerVisualDefinition> workerVisuals;

	// ability range
	[Header("Worker Ability")]
	[FormerlySerializedAs("workerType")]
	[SerializeField] private HumanType humanType;

	[Header("Abilities that this worker insure this ability")]
	[SerializeField] private WorkerAbility minimumAbility;

	[Header("Abilities that worker can have more, robot should not have this")]
	[SerializeField] private List<WorkerAbility> additionalAbilities;

	[Header("Cost of Buy")]
	[SerializeField] private int installCost;

	[Header("Monthly Pay")]
	[SerializeField] private int minimumMonthlyCost;
	[SerializeField] private int maximumMonthlyCost;

	// base stat range
	[Header("Base Stat - Move Speed Range")]
	[Range(0.01f, 1.5f)][SerializeField] private float minimumMoveSpeedRange;
	[Range(0.01f, 1.5f)][SerializeField] private float maximumMoveSpeedRange;

	[Header("Base Stat - Work Speed Range")]
	[Range(0.01f, 1.5f)][SerializeField] private float minimumWorkSpeedRange;
	[Range(0.01f, 1.5f)][SerializeField] private float maximumWorkSpeedRange;

	private float MoveSpdCenter => (minimumMoveSpeedRange + maximumMoveSpeedRange) / 2.0f;
	private float WorkSpdCenter => (minimumWorkSpeedRange + maximumMoveSpeedRange) / 2.0f;


	private static float GetRandomFloat(System.Random rng, float min, float max)
	{
		return (float)rng.NextDouble() * (max - min) + min;
	}

	protected override void OnValidation()
	{
		ClampMinimum(ref minimumMonthlyCost, ref maximumMonthlyCost);
		ClampMinimum(ref minimumMoveSpeedRange, ref maximumMoveSpeedRange);
		ClampMinimum(ref minimumWorkSpeedRange, ref maximumWorkSpeedRange);
	}

	public override void FillWorkerArchetype(WorkerArchetype target, System.Random rng, int page, int count)
	{
		int firstNameIdx = rng.Next(workerNames.Count);
		int lastNameIdx = rng.Next(workerNames.Count);
		int visualIdx = rng.Next(workerVisuals.Count);
		int additionalAbilIdx = rng.Next(additionalAbilities.Count);

		WorkerAbility additionalAbility = WorkerAbility.None;
		if (additionalAbilities.Count > 0)
			additionalAbility = additionalAbilities[additionalAbilIdx];

		int monthlyCst = rng.Next(minimumMonthlyCost, maximumMonthlyCost);

		float minMove = GetRandomFloat(rng, minimumMoveSpeedRange, MoveSpdCenter);
		float maxMove = GetRandomFloat(rng, MoveSpdCenter, maximumMoveSpeedRange);
		float minWork = GetRandomFloat(rng, minimumWorkSpeedRange, WorkSpdCenter);
		float maxWork = GetRandomFloat(rng, WorkSpdCenter, maximumWorkSpeedRange);

		// name
		target.WorkerNameDefinition.WorkerFirstName = workerNames[firstNameIdx].WorkerFirstName;
		target.WorkerNameDefinition.WorkerLastName = workerNames[lastNameIdx].WorkerLastName;

		// visual
		target.WorkerVisualDefinition = workerVisuals[visualIdx];

		// ability
		target.AbilityDefinition.SetHumanIdentity(humanType);
		target.AbilityDefinition.abilities = minimumAbility | additionalAbility;
		target.AbilityDefinition.monthlyCost = monthlyCst;
		target.AbilityDefinition.installCost = installCost;

		// base stat
		target.WorkerBaseStat.minimumMoveSpeedMultiplier = minMove;
		target.WorkerBaseStat.baseMoveSpeedMultiplier = maxMove;
		target.WorkerBaseStat.minimumWorkSpeedMultiplier = minWork;
		target.WorkerBaseStat.baseWorkSpeedMultiplier = maxWork;
	}
}
