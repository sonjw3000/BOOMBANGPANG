using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Building/Addon Catalog")]
public sealed class BuildingAddonCatalog : ScriptableObject
{
	[SerializeField] private List<BuildingAddonDefinition> definitions = new();

	public IReadOnlyList<BuildingAddonDefinition> Definitions => definitions;

	public BuildingAddonDefinition FindById(string addonId)
	{
		if (string.IsNullOrWhiteSpace(addonId) || definitions == null)
			return null;

		for (int i = 0; i < definitions.Count; ++i)
		{
			BuildingAddonDefinition definition = definitions[i];
			if (definition != null && definition.AddonId == addonId)
				return definition;
		}

		return null;
	}

	public void ValidateKeys()
	{
		if (definitions == null)
			return;

		HashSet<string> ids = new();
		for (int i = 0; i < definitions.Count; ++i)
		{
			BuildingAddonDefinition definition = definitions[i];
			if (definition == null)
				continue;

			if (string.IsNullOrWhiteSpace(definition.AddonId))
			{
				Debug.LogError($"Building addon definition {definition.name} is missing an addon ID.", definition);
				continue;
			}

			if (ids.Add(definition.AddonId) == false)
				Debug.LogError($"Duplicate building addon ID: {definition.AddonId}", this);
		}
	}

	private void OnValidate()
	{
		ValidateKeys();
	}
}
