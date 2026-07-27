using Unity.Mathematics;
using UnityEngine;

public sealed class RefrigerationUnit : MonoBehaviour, ITemperatureModifier
{
	[SerializeField, Min(0)] private int effectRadius = 3;
	[SerializeField, Min(0f)] private float temperatureReductionCelsius = 10f;
	[SerializeField, Min(0)] private int powerConsumption = 10;
	[SerializeField] private HealthState health = new();
	[SerializeField] private WearState wear = new();
	[SerializeField, Range(0.0f, 100.0f)] private float fireIntensity;

	private int3 gridPosition;
	private FacingDirection facingDirection;
	private uint facilityRulePresetId;

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public int PowerConsumption => powerConsumption;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.RefrigerationUnit;
	public int EffectRadius => effectRadius;
	public float TemperatureOffsetCelsius => -temperatureReductionCelsius;
	public float Health => health.Health;
	public float MaxHealth => health.MaxHealth;
	public float Wear => wear.Wear;
	public float WearEfficiency => wear.Efficiency;
	public float PassiveWearPerQuarterWeek => wear.PassiveWearPerQuarterWeek;
	public float OperatingWearPerQuarterWeek => wear.OperatingWearPerQuarterWeek;
	public float FireIntensity => fireIntensity;

	public float ApplyDamage(float amount) => health.ApplyDamage(amount);
	public void RestoreHealth(float value) => health.RestoreHealth(value);
	public void ApplyWear(float amount) => wear.Apply(amount);
	public void SetWearFromSave(float value) => wear.SetFromSave(value);
	public void SetFireIntensity(float intensity) => fireIntensity = Mathf.Clamp(intensity, 0.0f, 100.0f);

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		gridPosition = position;
		facingDirection = direction;
	}

	public void OnDestroyedBy(in DestroyContext context)
	{
	}

	public void SetFacilityRulePresetId(uint presetId)
	{
		facilityRulePresetId = presetId;
	}
}
