using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Placeable/Placeable Catalog")]
public class PlaceableCatalog : ScriptableObject
{
	public List<PlaceableDefinition> placeables = new();

	public PlaceableDefinition FindById(string placeableID)
	{
		foreach (var def in placeables)
		{
			if (def.placeableID == placeableID)
				return def;
		}
		return null;
	}
}
