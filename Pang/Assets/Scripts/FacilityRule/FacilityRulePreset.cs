using System;
using UnityEngine;

[Serializable]
public sealed class FacilityRulePreset
{
	[SerializeField] private uint id;
	[SerializeField] private string displayName;
	[SerializeField] private Color color = Color.white;
	[SerializeField] private FacilityRule rule = new();

	public FacilityRulePreset()
	{
	}

	public FacilityRulePreset(uint id, string displayName, Color color, FacilityRule rule)
	{
		this.id = id;
		this.displayName = displayName;
		this.color = color;
		this.rule = rule != null ? new FacilityRule(rule) : new FacilityRule();
	}

	public uint Id => id;
	public string DisplayName => displayName;
	public Color Color => color;
	public FacilityRule Rule => rule;

	public void AssignId(uint newId) => id = newId;
	public void Rename(string newDisplayName) => displayName = newDisplayName;
	public void SetColor(Color newColor) => color = newColor;
	public void SetRule(FacilityRule newRule)
	{
		rule = newRule != null ? new FacilityRule(newRule) : new FacilityRule();
	}
}
