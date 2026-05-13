using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class BuildPlaceableSection
{
	public string sectionId = "default";
	public string displayName = "Buildable";
	public List<PlaceableDefinition> placeables = new();
}

[CreateAssetMenu(menuName = "Placeable/Build Placeable Catalog")]
public class BuildPlaceableCatalog : ScriptableObject
{
	[SerializeField] private List<BuildPlaceableSection> sections = new();

	public IReadOnlyList<BuildPlaceableSection> Sections => sections;

	public IEnumerable<PlaceableDefinition> EnumerateDefinitions()
	{
		HashSet<string> emitted = new();

		foreach (var section in sections)
		{
			if (section == null || section.placeables == null)
				continue;

			foreach (var definition in section.placeables)
			{
				if (definition == null)
					continue;

				string id = definition.placeableID;
				if (string.IsNullOrWhiteSpace(id))
					continue;

				if (emitted.Add(id))
					yield return definition;
			}
		}
	}

	public PlaceableDefinition FindById(string placeableId)
	{
		if (string.IsNullOrWhiteSpace(placeableId))
			return null;

		foreach (var definition in EnumerateDefinitions())
		{
			if (definition.placeableID == placeableId)
				return definition;
		}

		return null;
	}
}
