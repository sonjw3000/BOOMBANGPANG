using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class PowerHub : MonoBehaviour, IFacility
{
	private int3 gridPosition;
	private FacingDirection facingDirection;
	private uint facilityRulePresetId;
	[SerializeField] private uint powerVendorId;
	[SerializeField] private int supplyRadius;

	private readonly List<PowerPort> connectedPorts = new();
	private PowerVendor activeVendor;

	public int3 GridPosition => gridPosition;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public int PowerConsumption => 0;
	public FacingDirection Direction => facingDirection;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.PowerHub;
	public uint PowerVendorId => powerVendorId;
	public int SupplyRadius => supplyRadius;
	public int PowerCapacity => activeVendor != null ? activeVendor.PowerCapacity : 0;
	public bool HasPower => activeVendor != null;
	public int ConnectedBuildingCount => connectedPorts.Count;

	public int CurrentPowerUsage
	{
		get
		{
			int totalUsage = 0;
			foreach (var port in connectedPorts)
			{
				totalUsage += port.CurrentPowerUsage;
			}
			return totalUsage;
		}
	}

	public float PowerEfficiency
	{
		get
		{
			if (PowerCapacity <= 0)
				return 0f;
			if (CurrentPowerUsage <= 0)
				return 1f;

			return math.clamp((float)PowerCapacity / CurrentPowerUsage, 0f, 1f);
		}
	}

	internal void SetActiveVendor(PowerVendor vendor)
	{
		activeVendor = vendor != null && vendor.VendorId == powerVendorId ? vendor : null;
	}

	internal bool TryConnect(PowerPort port)
	{
		if (port == null || connectedPorts.Contains(port))
			return false;

		connectedPorts.Add(port);
		return true;
	}

	internal void Disconnect(PowerPort port)
	{
		if (port != null)
			connectedPorts.Remove(port);
	}

	internal void ClearConnections()
	{
		connectedPorts.Clear();
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
