using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class PowerService : MonoBehaviour
{
	private readonly List<PowerHub> installedHubs = new();
	private readonly List<PowerPort> installedPorts = new();
	private bool eventsBound;

	private FacilityManager FacilityManager => GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private VendorService VendorService => GameContext.HasInstance ? GameContext.Instance.VendorService : null;
	private EconomyService EconomyService => GameContext.HasInstance ? GameContext.Instance.EconomyService : null;

	private void OnEnable()
	{
		BindEvents();
		RebuildRuntimeState();
	}

	private void Start()
	{
		BindEvents();
		RebuildRuntimeState();
	}

	private void OnDisable()
	{
		UnbindEvents();
	}

	public void ProcessWeeklyVendor(PowerVendor vendor)
	{
		if (vendor == null)
			return;

		int activeHubCount = 0;
		for (int i = 0; i < installedHubs.Count; ++i)
		{
			PowerHub hub = installedHubs[i];
			if (hub != null && hub.PowerVendorId == vendor.VendorId)
				activeHubCount++;
		}

		int weeklyCost = CalculateWeeklyCost(vendor, activeHubCount);
		if (weeklyCost <= 0 || EconomyService == null)
			return;

		EconomyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = -weeklyCost,
			reputationDelta = 0f,
			reason = EconomyTransaction.Reason.PowerService,
		});
	}

	public int CalculateWeeklyCost(PowerVendor vendor, int activeHubCount)
	{
		if (vendor == null || activeHubCount <= 0)
			return 0;

		return vendor.WeeklyPowerCost * activeHubCount;
	}

	public float GetPowerEfficiency(IFacility facility)
	{
		if (facility == null ||
			FacilityManager == null ||
			FacilityManager.TryGetBuildingId(facility, out uint buildingId) == false ||
			BuildingManager == null ||
			BuildingManager.TryGetBuilding(buildingId, out Building building) == false)
			return 0f;

		return building.PowerEfficiency;
	}

	public void ResetRuntimeState()
	{
		for (int i = 0; i < installedPorts.Count; ++i)
			installedPorts[i]?.Disconnect();

		for (int i = 0; i < installedHubs.Count; ++i)
			installedHubs[i]?.ClearConnections();

		installedPorts.Clear();
		installedHubs.Clear();
	}

	public void RebuildConnections()
	{
		RefreshHubSupply();

		for (int i = 0; i < installedHubs.Count; ++i)
			installedHubs[i]?.ClearConnections();

		for (int i = 0; i < installedPorts.Count; ++i)
			installedPorts[i]?.Disconnect();

		for (int portIndex = 0; portIndex < installedPorts.Count; ++portIndex)
		{
			PowerPort port = installedPorts[portIndex];
			if (port == null || IsEffectiveBuildingPort(port) == false)
				continue;

			for (int hubIndex = 0; hubIndex < installedHubs.Count; ++hubIndex)
			{
				PowerHub hub = installedHubs[hubIndex];
				if (hub == null || hub.HasPower == false || IsInSupplyRange(hub, port) == false)
					continue;

				port.Connect(hub);
				break;
			}
		}
	}

	private void BindEvents()
	{
		if (eventsBound || FacilityManager == null || VendorService == null)
			return;

		FacilityManager.SubscribeFacilityRegister<PowerHub>(HandleHubRegistered, HandleHubUnregistered);
		FacilityManager.SubscribeFacilityRegister<PowerPort>(HandlePortRegistered, HandlePortUnregistered);
		VendorService.OnVendorsChanged += HandleVendorsChanged;
		eventsBound = true;
	}

	private void UnbindEvents()
	{
		if (eventsBound == false)
			return;

		if (FacilityManager != null)
		{
			FacilityManager.UnsubscribeFacilityRegister<PowerHub>(HandleHubRegistered, HandleHubUnregistered);
			FacilityManager.UnsubscribeFacilityRegister<PowerPort>(HandlePortRegistered, HandlePortUnregistered);
		}

		if (VendorService != null)
			VendorService.OnVendorsChanged -= HandleVendorsChanged;

		eventsBound = false;
	}

	private void RebuildRuntimeState()
	{
		if (FacilityManager == null)
			return;

		installedHubs.Clear();
		installedPorts.Clear();

		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			uint buildingId = buildingIds[i];
			IReadOnlyList<PowerHub> hubs = FacilityManager.GetFacilities<PowerHub>(buildingId);
			for (int hubIndex = 0; hubIndex < hubs.Count; ++hubIndex)
				AddUnique(installedHubs, hubs[hubIndex]);

			IReadOnlyList<PowerPort> ports = FacilityManager.GetFacilities<PowerPort>(buildingId);
			for (int portIndex = 0; portIndex < ports.Count; ++portIndex)
			{
				PowerPort port = ports[portIndex];
				AddUnique(installedPorts, port);
				AssignBuilding(buildingId, port);
			}
		}

		RebuildConnections();
	}

	private void HandleHubRegistered(uint buildingId, IFacility facility)
	{
		if (facility is not PowerHub hub)
			return;

		AddUnique(installedHubs, hub);
		RebuildConnections();
	}

	private void HandleHubUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is not PowerHub hub)
			return;

		installedHubs.Remove(hub);
		hub.ClearConnections();
		RebuildConnections();
	}

	private void HandlePortRegistered(uint buildingId, IFacility facility)
	{
		if (facility is not PowerPort port)
			return;

		AddUnique(installedPorts, port);
		AssignBuilding(buildingId, port);
		RebuildConnections();
	}

	private void HandlePortUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is not PowerPort port)
			return;

		installedPorts.Remove(port);
		port.Disconnect();
		port.SetConnectedBuilding(null);
		RebuildConnections();
	}

	private void HandleVendorsChanged()
	{
		RebuildConnections();
	}

	private void RefreshHubSupply()
	{
		for (int i = 0; i < installedHubs.Count; ++i)
		{
			PowerHub hub = installedHubs[i];
			if (hub == null)
				continue;

			hub.SetActiveVendor(TryGetActiveVendor(hub.PowerVendorId, out PowerVendor vendor) ? vendor : null);
		}
	}

	private bool TryGetActiveVendor(uint vendorId, out PowerVendor vendor)
	{
		vendor = null;
		if (VendorService == null)
			return false;

		IReadOnlyList<VendorRuntime> activeVendors = VendorService.GetActiveVendors(VendorType.Power);
		for (int i = 0; i < activeVendors.Count; ++i)
		{
			if (activeVendors[i]?.Vendor is not PowerVendor candidate || candidate.VendorId != vendorId)
				continue;

			vendor = candidate;
			return true;
		}

		return false;
	}

	private bool IsEffectiveBuildingPort(PowerPort port)
	{
		return port != null &&
			port.ConnectedBuildingId != 0 &&
			BuildingManager != null &&
			BuildingManager.TryGetBuilding(port.ConnectedBuildingId, out Building building) &&
			building.PowerPort == port;
	}

	private void AssignBuilding(uint buildingId, PowerPort port)
	{
		if (port == null)
			return;

		port.SetConnectedBuilding(
			BuildingManager != null && BuildingManager.TryGetBuilding(buildingId, out Building building)
				? building
				: null);
	}

	private static bool IsInSupplyRange(PowerHub hub, PowerPort port)
	{
		int3 delta = math.abs(hub.GridPosition - port.GridPosition);
		return delta.x + delta.y + delta.z <= hub.SupplyRadius;
	}

	private static void AddUnique<T>(List<T> list, T value) where T : class
	{
		if (value != null && list.Contains(value) == false)
			list.Add(value);
	}
}
