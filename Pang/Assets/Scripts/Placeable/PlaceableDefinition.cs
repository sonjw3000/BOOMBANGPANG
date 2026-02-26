using UnityEngine;

[CreateAssetMenu(menuName = "Placeable/Placeable Definition")]
public class PlaceableDefinition : ScriptableObject
{
	[Header("Identity")]
	public string placeableID = "";
	public string displayName = "";
	public Sprite icon;

	[Header("Prefab")]
	public GameObject prefab;
	public GridFootprint gridFootprint;

	[Header("Placeable Definition Type")]
	public PlaceableDefinitionType definitionType = PlaceableDefinitionType.Other;

	public int Cost = 10;
}

