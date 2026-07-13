using System;
using System.Collections.Generic;

public readonly struct LicenseConditionEvaluation
{
	public LicenseCondition Condition { get; }
	public float ObservedValue { get; }
	public bool IsSatisfied { get; }

	public LicenseConditionEvaluation(LicenseCondition condition, float observedValue, bool isSatisfied)
	{
		Condition = condition;
		ObservedValue = observedValue;
		IsSatisfied = isSatisfied;
	}
}

public sealed class LicenseConditionGroupEvaluation
{
	public LicenseConditionGroup Group { get; }
	public uint BuildingId { get; }
	public bool IsSatisfied { get; }
	public IReadOnlyList<LicenseConditionEvaluation> Conditions { get; }

	public LicenseConditionGroupEvaluation(
		LicenseConditionGroup group,
		uint buildingId,
		bool isSatisfied,
		IReadOnlyList<LicenseConditionEvaluation> conditions)
	{
		Group = group;
		BuildingId = buildingId;
		IsSatisfied = isSatisfied;
		Conditions = conditions ?? Array.Empty<LicenseConditionEvaluation>();
	}
}

public sealed class LicenseEvaluationResult
{
	public LicenseDefinition Definition { get; }
	public LicenseGrade Grade { get; }
	public bool IsSatisfied { get; }
	public IReadOnlyList<LicenseConditionGroupEvaluation> Groups { get; }

	public LicenseEvaluationResult(
		LicenseDefinition definition,
		LicenseGrade grade,
		bool isSatisfied,
		IReadOnlyList<LicenseConditionGroupEvaluation> groups)
	{
		Definition = definition;
		Grade = grade;
		IsSatisfied = isSatisfied;
		Groups = groups ?? Array.Empty<LicenseConditionGroupEvaluation>();
	}
}

public static class LicenseConditionEvaluator
{
	public static LicenseEvaluationResult Evaluate(
		LicenseDefinition definition,
		LicenseGrade grade,
		CompanyStateSnapshot snapshot)
	{
		if (definition == null || definition.TryGetGradeDefinition(grade, out LicenseGradeDefinition gradeDefinition) == false)
			return new LicenseEvaluationResult(definition, grade, false, Array.Empty<LicenseConditionGroupEvaluation>());

		snapshot ??= CompanyStateSnapshot.Empty;
		IReadOnlyList<LicenseConditionGroup> requiredGroups = gradeDefinition.RequiredConditionGroups;
		if (requiredGroups == null || requiredGroups.Count == 0)
			return new LicenseEvaluationResult(definition, grade, true, Array.Empty<LicenseConditionGroupEvaluation>());

		List<LicenseConditionGroupEvaluation> groupResults = new(requiredGroups.Count);
		bool allGroupsSatisfied = true;
		foreach (LicenseConditionGroup group in requiredGroups)
		{
			LicenseConditionGroupEvaluation groupResult = EvaluateGroup(group, snapshot);
			groupResults.Add(groupResult);
			allGroupsSatisfied &= groupResult.IsSatisfied;
		}

		return new LicenseEvaluationResult(definition, grade, allGroupsSatisfied, groupResults);
	}

	private static LicenseConditionGroupEvaluation EvaluateGroup(
		LicenseConditionGroup group,
		CompanyStateSnapshot snapshot)
	{
		if (group == null || group.Scope != LicenseConditionScope.AnyBuilding)
			return new LicenseConditionGroupEvaluation(group, 0, false, Array.Empty<LicenseConditionEvaluation>());

		IReadOnlyList<CompanyBuildingStateSnapshot> buildings = snapshot.Buildings;
		List<LicenseConditionEvaluation> bestConditions = new();
		uint bestBuildingId = 0;
		int bestSatisfiedCount = -1;

		foreach (CompanyBuildingStateSnapshot building in buildings)
		{
			if (group.RequireActiveBuilding && building.State != BuildingState.Active)
				continue;

			List<LicenseConditionEvaluation> conditionResults = EvaluateConditions(group, building);
			int satisfiedCount = CountSatisfied(conditionResults);
			bool groupSatisfied = IsGroupSatisfied(group, conditionResults, satisfiedCount);

			if (groupSatisfied)
				return new LicenseConditionGroupEvaluation(group, building.BuildingId, true, conditionResults);

			if (satisfiedCount <= bestSatisfiedCount)
				continue;

			bestSatisfiedCount = satisfiedCount;
			bestBuildingId = building.BuildingId;
			bestConditions = conditionResults;
		}

		return new LicenseConditionGroupEvaluation(group, bestBuildingId, false, bestConditions);
	}

	private static List<LicenseConditionEvaluation> EvaluateConditions(
		LicenseConditionGroup group,
		CompanyBuildingStateSnapshot building)
	{
		IReadOnlyList<LicenseCondition> conditions = group.Conditions;
		List<LicenseConditionEvaluation> results = new(conditions?.Count ?? 0);
		if (conditions == null)
			return results;

		foreach (LicenseCondition condition in conditions)
		{
			if (condition == null)
				continue;

			float observedValue = ReadMetric(condition.Metric, building);
			bool isSatisfied = Compare(observedValue, condition.TargetValue, condition.Comparison);
			results.Add(new LicenseConditionEvaluation(condition, observedValue, isSatisfied));
		}

		return results;
	}

	private static float ReadMetric(LicenseConditionMetric metric, CompanyBuildingStateSnapshot building)
	{
		return metric switch
		{
			LicenseConditionMetric.AverageTemperatureCelsius => building.AverageTemperatureCelsius,
			LicenseConditionMetric.PowerSupplyRatio => building.PowerSupplyRatio,
			_ => 0.0f,
		};
	}

	private static bool Compare(float observed, float target, LicenseNumericComparison comparison)
	{
		return comparison switch
		{
			LicenseNumericComparison.Equal => Math.Abs(observed - target) <= 0.0001f,
			LicenseNumericComparison.LessThan => observed < target,
			LicenseNumericComparison.LessThanOrEqual => observed <= target,
			LicenseNumericComparison.GreaterThan => observed > target,
			LicenseNumericComparison.GreaterThanOrEqual => observed >= target,
			_ => false,
		};
	}

	private static int CountSatisfied(IReadOnlyList<LicenseConditionEvaluation> conditions)
	{
		int count = 0;
		for (int i = 0; i < conditions.Count; ++i)
		{
			if (conditions[i].IsSatisfied)
				++count;
		}

		return count;
	}

	private static bool IsGroupSatisfied(
		LicenseConditionGroup group,
		IReadOnlyList<LicenseConditionEvaluation> conditions,
		int satisfiedCount)
	{
		if (conditions.Count == 0)
			return true;

		return group.MatchMode == LicenseConditionMatchMode.All
			? satisfiedCount == conditions.Count
			: satisfiedCount > 0;
	}
}
