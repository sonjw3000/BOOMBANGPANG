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
	[SerializeField, Min(0.0f)] private float ignitionTemperatureCelsius;
	[SerializeField] private List<DamageIncidentDefinition> damageIncidents = new();

	public int Cost = 10;

	public string RequiredResearchUid => requiredResearchUid;
	public bool RequiresResearch => string.IsNullOrWhiteSpace(requiredResearchUid) == false;
	public float IgnitionTemperatureCelsius => ignitionTemperatureCelsius > 0.0f
		? ignitionTemperatureCelsius
		: float.PositiveInfinity;
	public IReadOnlyList<DamageIncidentDefinition> DamageIncidents => damageIncidents;
}
