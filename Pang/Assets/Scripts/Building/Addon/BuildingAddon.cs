public class BuildingAddon : IHealth, IWearable
{
	private readonly BuildingAddonDefinition definition;
	private readonly HealthState health = new();
	private readonly WearState wear = new();

	public BuildingAddonDefinition Definition => definition;
	public int PowerConsumption => definition != null ? definition.PowerConsumption : 0;
	public float OxygenSupplyPerTick => definition != null ? definition.OxygenSupplyPerTick : 0.0f;
	public float Health => health.Health;
	public float MaxHealth => health.MaxHealth;
	public float Wear => wear.Wear;
	public float WearEfficiency => wear.Efficiency;
	public float PassiveWearPerQuarterWeek => wear.PassiveWearPerQuarterWeek;
	public float OperatingWearPerQuarterWeek => wear.OperatingWearPerQuarterWeek;

	public BuildingAddon(BuildingAddonDefinition definition)
	{
		this.definition = definition;
	}

	public float ApplyDamage(float amount) => health.ApplyDamage(amount);
	public void RestoreHealth(float value) => health.RestoreHealth(value);
	public void ApplyWear(float amount) => wear.Apply(amount);
	public void SetWearFromSave(float value) => wear.SetFromSave(value);
}

public sealed class OxygenSupplyBuildingAddon : BuildingAddon, IOxygenSupplier
{
	public OxygenSupplyBuildingAddon(BuildingAddonDefinition definition) : base(definition)
	{
	}
}
