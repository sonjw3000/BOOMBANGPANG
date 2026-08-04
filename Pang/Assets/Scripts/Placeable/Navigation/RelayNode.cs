using Unity.Mathematics;
using UnityEngine;

public sealed class RelayNode : MonoBehaviour, IFacility
{
	[SerializeField] private HealthState health = new();
	[SerializeField, Range(0.0f, 100.0f)] private float fireIntensity;
	[SerializeField, Min(0)] private int coverageRadius = 6;
	[SerializeField] private uint ownerHubId;

	private int3 gridPosition;
	private FacingDirection facingDirection;
	private uint facilityRulePresetId;
	private bool isConnected;

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.RelayNode;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public int PowerConsumption => 0;
	public int CoverageRadius => Mathf.Max(0, coverageRadius);
	public uint OwnerHubId => ownerHubId;
	public bool IsConnected => isConnected;
	public bool IsOperational => Health > 0.0f;
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
		isConnected = false;
	}

	public void SetFacilityRulePresetId(uint presetId)
	{
		facilityRulePresetId = presetId;
	}

	internal void SetOwnerHubId(uint hubId)
	{
		ownerHubId = hubId;
	}

	internal void SetConnected(bool connected)
	{
		isConnected = connected;
	}

	private void NotifyNavigationStateChanged()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.RobotNavigationSvc?.RebuildRuntimeState();
	}
}
