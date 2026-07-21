using System.Collections.Generic;
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
	public PlacementEnvironmentRequirement placementEnvironment = PlacementEnvironmentRequirement.Indoor | PlacementEnvironmentRequirement.Outdoor;

	[Header("Research Requirement")]
	[SerializeField] private string requiredResearchUid = string.Empty;

	[Header("Damage Response")]
	[SerializeField, Range(0.0f, 1.0f)] private float flammability = 0.1f;
	[SerializeField] private List<DamageIncidentDefinition> damageIncidents = new();

	public int Cost = 10;

	public string RequiredResearchUid => requiredResearchUid;
	public bool RequiresResearch => string.IsNullOrWhiteSpace(requiredResearchUid) == false;
	public float Flammability => Mathf.Clamp01(flammability);
	public IReadOnlyList<DamageIncidentDefinition> DamageIncidents => damageIncidents;
}
