using System;
using System.Collections.Generic;
using UnityEngine;

public enum LicenseConditionScope
{
	Company = 0,
	AnyBuilding = 1,
}

public enum LicenseConditionMatchMode
{
	All = 0,
	Any = 1,
}

public enum LicenseConditionMetric
{
	AverageTemperatureCelsius = 0,
	PowerSupplyRatio = 1,
}

public enum LicenseNumericComparison
{
	Equal = 0,
	LessThan = 1,
	LessThanOrEqual = 2,
	GreaterThan = 3,
	GreaterThanOrEqual = 4,
}

[Serializable]
public sealed class LicenseCondition
{
	[SerializeField] private LicenseConditionMetric metric = LicenseConditionMetric.AverageTemperatureCelsius;
	[SerializeField] private LicenseNumericComparison comparison = LicenseNumericComparison.Equal;
	[SerializeField] private float targetValue = 0.0f;

	public LicenseConditionMetric Metric => metric;
	public LicenseNumericComparison Comparison => comparison;
	public float TargetValue => targetValue;
}

[Serializable]
public sealed class LicenseConditionGroup
{
	[SerializeField] private LicenseConditionScope scope = LicenseConditionScope.AnyBuilding;
	[SerializeField] private LicenseConditionMatchMode matchMode = LicenseConditionMatchMode.All;
	[SerializeField] private bool requireActiveBuilding = true;
	[SerializeField] private List<LicenseCondition> conditions = new();

	public LicenseConditionScope Scope => scope;
	public LicenseConditionMatchMode MatchMode => matchMode;
	public bool RequireActiveBuilding => requireActiveBuilding;
	public IReadOnlyList<LicenseCondition> Conditions => conditions;
}
