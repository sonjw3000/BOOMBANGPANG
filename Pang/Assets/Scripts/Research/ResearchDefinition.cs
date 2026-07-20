using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ResearchDefinition
{
	[SerializeField] private string uid;
	[SerializeField] private string categoryId;
	[SerializeField] private string displayName;
	[SerializeField, TextArea] private string description;
	[SerializeField, Min(0)] private int cost;
	[SerializeField, Min(1)] private int durationWeeks = 1;
	[SerializeField] private List<string> prerequisiteUids = new();

	public string Uid => uid;
	public string CategoryId => categoryId;
	public string DisplayName => displayName;
	public string Description => description;
	public int Cost => cost;
	public int DurationWeeks => durationWeeks;
	public IReadOnlyList<string> PrerequisiteUids => prerequisiteUids != null
		? prerequisiteUids
		: Array.Empty<string>();
}
