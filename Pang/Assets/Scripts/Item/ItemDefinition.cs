using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum ItemTag
{
	None		= 0,
	Fragile		= 1 << 0,
	Food		= 1 << 1,
	Danger		= 1 << 2,
	Electric	= 1 << 3,
}

public enum ItemDamageIncidentType
{
	None,
	Fire,
	Explosion,
	Contamination,
	Corrosion,
	RadiationLeak,
}

public enum ThermalResponse
{
	Slow,
	Normal,
	Fast,
}

[Serializable]
public sealed class DamageIncidentDefinition
{
	[SerializeField, Range(1, 100)] private int triggerDamage = 100;
	[SerializeField] private ItemDamageIncidentType incidentType;
	[SerializeField, Min(0)] private int radius;
	[SerializeField, Range(0, 100)] private int severity = 100;
	[SerializeField, Range(0, 100)] private int edgeDamagePercent = 25;
	[SerializeField, Min(0)] private int triggerDelayTicks;

	public int TriggerDamage => Mathf.Clamp(triggerDamage, 1, 100);
	public ItemDamageIncidentType IncidentType => incidentType;
	public int Radius => Mathf.Max(0, radius);
	public int Severity => Mathf.Clamp(severity, 0, 100);
	public int EdgeDamagePercent => Mathf.Clamp(edgeDamagePercent, 0, 100);
	public int TriggerDelayTicks => Mathf.Max(0, triggerDelayTicks);
}


// 지금은 이걸 그대로 사용하지만
// 나중엔 이걸 따로 등록하고 걔를 가리키는 형식으로다가 하자
[CreateAssetMenu(menuName = "Item/ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
	private const float DefaultConditionMaximum = 1000.0f;

	[SerializeField] private uint itemID;
	[SerializeField] private float size;
	[SerializeField] private ItemTag tag;
	// 혹시 모를 render를 위한 프리팹
	[SerializeField] private GameObject itemPrefab;

	[SerializeField] private int price = 100;
	[SerializeField, Min(1.0f)] private float maxFreshness = DefaultConditionMaximum;
	[SerializeField, Min(1.0f)] private float maxIntegrity = DefaultConditionMaximum;
	[SerializeField] private ThermalResponse thermalResponse = ThermalResponse.Normal;
	[SerializeField] private float freezingDamageTemperatureCelsius = ThermalUtility.AbsoluteZeroCelsius;
	[SerializeField] private float heatDamageTemperatureCelsius = ThermalUtility.AbsoluteZeroCelsius;
	[SerializeField, Min(0.0f)] private float ignitionTemperatureCelsius;
	[SerializeField] private List<DamageIncidentDefinition> damageIncidents = new();

	public uint ItemID => itemID;
	public float Size => size;
	public ItemTag Tag => tag;
	public GameObject ItemPrefab => itemPrefab;
	public int Price => price;
	public float MaxFreshness => Mathf.Max(1.0f, maxFreshness);
	public float MaxIntegrity => Mathf.Max(1.0f, maxIntegrity);
	public ThermalResponse ThermalResponse => thermalResponse;
	public float FreezingDamageTemperatureCelsius =>
		Mathf.Max(ThermalUtility.AbsoluteZeroCelsius, freezingDamageTemperatureCelsius);
	public float HeatDamageTemperatureCelsius =>
		heatDamageTemperatureCelsius <= ThermalUtility.AbsoluteZeroCelsius
			? float.PositiveInfinity
			: heatDamageTemperatureCelsius;
	public float IgnitionTemperatureCelsius => ignitionTemperatureCelsius > 0.0f
		? ignitionTemperatureCelsius
		: float.PositiveInfinity;
	public IReadOnlyList<DamageIncidentDefinition> DamageIncidents => damageIncidents;
}
