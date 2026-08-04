using Unity.Mathematics;
using UnityEngine;

public sealed class NavigationHub : MonoBehaviour, IFacility
{
	[SerializeField] private HealthState health = new();
	[SerializeField, Range(0.0f, 100.0f)] private float fireIntensity;
	[SerializeField, Min(0)] private int coverageRadius = 8;
	[SerializeField, Min(0)] private int computeCapacity = 1000;
	[SerializeField, Min(0)] private int relayCapacity = 8;
	[SerializeField, Min(0)] private int basePowerConsumption = 6;
	[SerializeField, Min(0)] private int relayPowerConsumption = 1;

	private int3 gridPosition;
	private FacingDirection facingDirection;
	private uint facilityRulePresetId;
	private uint runtimeHubId;
	private int activeRelayCount;
	private PowerHub connectedPowerHub;

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.NavigationHub;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public uint RuntimeHubId => runtimeHubId;
	public int CoverageRadius => Mathf.Max(0, coverageRadius);
	public int ComputeCapacity => Mathf.Max(0, computeCapacity);
	public int RelayCapacity => Mathf.Max(0, relayCapacity);
	public int ActiveRelayCount => Mathf.Max(0, activeRelayCount);
	public int BasePowerConsumption => Mathf.Max(0, basePowerConsumption);
	public int RelayPowerConsumption => Mathf.Max(0, relayPowerConsumption);
	public int PowerConsumption => BasePowerConsumption + ActiveRelayCount * RelayPowerConsumption;
	public PowerHub ConnectedPowerHub => connectedPowerHub;
	public float PowerEfficiency => connectedPowerHub != null ? connectedPowerHub.PowerEfficiency : 0.0f;
	public bool HasPower => connectedPowerHub != null && connectedPowerHub.HasPower && PowerEfficiency > 0.0f;
	public bool IsOperational => Health > 0.0f && HasPower;
	public float Health => health.Health;
	public float MaxHealth => health.MaxHealth;
	public float FireIntensity => fireIntensity;

	public float ApplyDamage(float amount)
	{
		bool wasOperational = IsOperational;
		float applied = health.ApplyDamage(amount);
		if (applied > 0.0f && wasOperational != IsOperational)
			NotifyNavigationStateChanged();

		return applied;
	}

	public void RestoreHealth(float value)
	{
		bool wasOperational = IsOperational;
		health.RestoreHealth(value);
		if (wasOperational != IsOperational)
			NotifyNavigationStateChanged();
	}

	public void SetFireIntensity(float intensity)
	{
		fireIntensity = Mathf.Clamp(intensity, 0.0f, 100.0f);
	}

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		gridPosition = position;
		facingDirection = direction;
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
		DisconnectPower();
	}

	public void SetFacilityRulePresetId(uint presetId)
	{
		facilityRulePresetId = presetId;
	}

	internal void SetRuntimeHubId(uint hubId)
	{
		runtimeHubId = hubId;
	}

	internal void SetActiveRelayCount(int count)
	{
		activeRelayCount = Mathf.Clamp(count, 0, RelayCapacity);
	}

	internal void ConnectPower(PowerHub hub)
	{
		if (connectedPowerHub == hub)
			return;

		connectedPowerHub?.Disconnect(this);
		connectedPowerHub = hub;
		connectedPowerHub?.TryConnect(this);
	}

	internal void DisconnectPower()
	{
		PowerHub previousHub = connectedPowerHub;
		connectedPowerHub = null;
		previousHub?.Disconnect(this);
	}

	private void NotifyNavigationStateChanged()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.RobotNavigationSvc?.RebuildRuntimeState();
	}
}
