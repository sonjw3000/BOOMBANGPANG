using System.Collections.Generic;
using UnityEngine;

public enum BuildingAddonType
{
	None,
	OxygenSupply,
	TemperatureControl,
}

public enum BuildingAddonCategory
{
	LifeSupport,
	ClimateControl,
}

[CreateAssetMenu(menuName = "Building/Addon Definition")]
public sealed class BuildingAddonDefinition : ScriptableObject
{
	[SerializeField] private string addonId = string.Empty;
	[SerializeField] private string displayName = string.Empty;
	[SerializeField] private Sprite icon;
	[SerializeField] private BuildingAddonType addonType;
	[SerializeField] private BuildingAddonCategory category;
	[SerializeField, Min(0)] private int cost;
	[SerializeField, Min(0)] private int powerConsumption;
	[SerializeField, Min(0.0f)] private float oxygenSupplyPerTick;
	[SerializeField] private float minimumTargetTemperatureCelsius;
	[SerializeField] private float maximumTargetTemperatureCelsius = GridCell.DefaultTemperatureCelsius;
	[SerializeField] private bool canCool;
	[SerializeField] private bool canHeat;
	[SerializeField, Min(0.0f)] private float temperatureControlDegreesPerQuarterWeek;
	[SerializeField] private string requiredResearchUid = string.Empty;
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
	public BuildingAddonCategory Category => category;
	public int Cost => Mathf.Max(0, cost);
	public int PowerConsumption => Mathf.Max(0, powerConsumption);
	public float OxygenSupplyPerTick => Mathf.Max(0.0f, oxygenSupplyPerTick);
	public float MinimumTargetTemperatureCelsius =>
		Mathf.Min(minimumTargetTemperatureCelsius, maximumTargetTemperatureCelsius);
	public float MaximumTargetTemperatureCelsius =>
		Mathf.Max(minimumTargetTemperatureCelsius, maximumTargetTemperatureCelsius);
	public bool CanCool => canCool;
	public bool CanHeat => canHeat;
	public float TemperatureControlDegreesPerQuarterWeek =>
		Mathf.Max(0.0f, temperatureControlDegreesPerQuarterWeek);
	public string RequiredResearchUid => requiredResearchUid;
	public bool RequiresResearch => string.IsNullOrWhiteSpace(requiredResearchUid) == false;
	public IReadOnlyList<BuildingType> AllowedBuildingTypes => allowedBuildingTypes;

	public bool IsAllowedFor(BuildingType buildingType)
	{
		return allowedBuildingTypes != null && allowedBuildingTypes.Contains(buildingType);
	}
}
