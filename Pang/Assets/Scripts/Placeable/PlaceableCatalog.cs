using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Placeable/Placeable Catalog")]
public class PlaceableCatalog : ScriptableObject
{
	public List<PlaceableDefinition> placeables = new();
}
