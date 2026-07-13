using System;
using System.Collections.Generic;
using UnityEngine;

public enum LicenseGrade
{
	A = 0,
	B = 1,
	C = 2,
	None = 3,
}

[Serializable]
public sealed class LicenseGradeDefinition
{
	[SerializeField] private LicenseGrade grade = LicenseGrade.A;
	[Tooltip("Every condition group in this list is required for the grade.")]
	[SerializeField] private List<LicenseConditionGroup> requiredConditionGroups = new();

	public LicenseGrade Grade => grade;
	public IReadOnlyList<LicenseConditionGroup> RequiredConditionGroups => requiredConditionGroups;
}

[CreateAssetMenu(fileName = "LicenseDefinition", menuName = "License/License Definition")]
public sealed class LicenseDefinition : ScriptableObject
{
	[SerializeField] private string licenseId = string.Empty;
	[SerializeField] private string displayName = string.Empty;
	[SerializeField] private List<LicenseGradeDefinition> grades = new();

	public string LicenseId => licenseId;
	public string DisplayName => displayName;
	public IReadOnlyList<LicenseGradeDefinition> Grades => grades;
}
