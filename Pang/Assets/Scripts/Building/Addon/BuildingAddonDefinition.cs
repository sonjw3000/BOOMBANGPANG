using System.Collections.Generic;
using UnityEngine;

public enum BuildingAddonType
{
	None,
	OxygenSupply,
}

[CreateAssetMenu(menuName = "Building/Addon Definition")]
public sealed class BuildingAddonDefinition : ScriptableObject
{
	[SerializeField] private string addonId = string.Empty;
	[SerializeField] private string displayName = string.Empty;
	[SerializeField] private Sprite icon;
	[SerializeField] private BuildingAddonType addonType;
	[SerializeField, Min(0)] private int cost;
	[SerializeField, Min(0)] private int powerConsumption;
	[SerializeField, Min(0.0f)] private float oxygenSupplyPerTick;
	[SerializeField] private List<BuildingType> allowedBuildingTypes = new()
	{
		BuildingType.Generic,
		BuildingType.Staging,
		BuildingType.Storage,
		BuildingType.Packing,
		BuildingType.Launch,
	};

	public string AddonId => addonId;
	public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
	public Sprite Icon => icon;
	public BuildingAddonType AddonType => addonType;
	public int Cost => Mathf.Max(0, cost);
	public int PowerConsumption => Mathf.Max(0, powerConsumption);
	public float OxygenSupplyPerTick => Mathf.Max(0.0f, oxygenSupplyPerTick);
	public IReadOnlyList<BuildingType> AllowedBuildingTypes => allowedBuildingTypes;

	public bool IsAllowedFor(BuildingType buildingType)
	{
		return allowedBuildingTypes != null && allowedBuildingTypes.Contains(buildingType);
	}
}
