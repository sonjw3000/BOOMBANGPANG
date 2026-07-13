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

	public bool HasGrade(LicenseGrade grade)
	{
		if (grade == LicenseGrade.None || grades == null)
			return false;

		return grades.Exists(gradeDefinition =>
			gradeDefinition != null && gradeDefinition.Grade == grade);
	}

	private void OnValidate()
	{
		if (string.IsNullOrWhiteSpace(licenseId))
			Debug.LogError($"[LicenseDefinition] LicenseId is empty on {name}.", this);

		if (grades == null)
			return;

		HashSet<LicenseGrade> registeredGrades = new();
		foreach (LicenseGradeDefinition gradeDefinition in grades)
		{
			if (gradeDefinition == null)
			{
				Debug.LogError($"[LicenseDefinition] Null grade definition found on {name}.", this);
				continue;
			}

			if (gradeDefinition.Grade == LicenseGrade.None)
			{
				Debug.LogError($"[LicenseDefinition] None cannot be defined as a grade on {name}.", this);
				continue;
			}

			if (registeredGrades.Add(gradeDefinition.Grade) == false)
				Debug.LogError($"[LicenseDefinition] Duplicate grade {gradeDefinition.Grade} found on {name}.", this);
		}
	}
}
