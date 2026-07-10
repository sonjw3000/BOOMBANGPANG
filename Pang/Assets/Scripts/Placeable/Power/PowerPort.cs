using UnityEngine;
using Unity.Mathematics;

public class PowerPort : MonoBehaviour, IFacility
{
	private int3 gridPosition;
	private FacingDirection facingDirection;
	private uint facilityRulePresetId;
	private PowerHub connectedHub;
	private Building connectedBuilding;

	public int3 GridPosition => gridPosition;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public FacingDirection Direction => facingDirection;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.PowerPort;
	public PowerHub ConnectedHub => connectedHub;
	public uint ConnectedBuildingId => connectedBuilding != null ? connectedBuilding.RuntimeBuildingId : 0;
	public int CurrentPowerUsage => connectedBuilding != null ? connectedBuilding.CurrentPowerConsumption : 0;
	public float PowerEfficiency => connectedHub != null ? connectedHub.PowerEfficiency : 0f;

	internal void SetConnectedBuilding(Building building)
	{
		connectedBuilding = building;
	}

	internal void Connect(PowerHub hub)
	{
		if (connectedHub == hub)
			return;

		connectedHub?.Disconnect(this);
		connectedHub = hub;
		connectedHub?.TryConnect(this);
	}

	internal void Disconnect()
	{
		PowerHub previousHub = connectedHub;
		connectedHub = null;
		previousHub?.Disconnect(this);
	}

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		gridPosition = position;
		facingDirection = direction;
	}
	public void OnDestroyedBy(in DestroyContext ctx)
	{
		// Do nothing
	}

	public void SetFacilityRulePresetId(uint presetId)
	{
		facilityRulePresetId = presetId;
	}
}
