
using System.Collections.Generic;
using UnityEngine;

public class LaunchVendorPickupService : VendorProcessor
{
	public override VendorType Type => VendorType.Launch;

	private readonly List<CargoPort> queryBuffer = new();

	private static GameContext Context => GameContext.Instance;

	public override void ProcessVendor(VendorRuntime vendor)
	{
		if (vendor?.Vendor is not LaunchServiceVendor launchVendor)
			return;

		
		if (Context == null || Context.BuildingMgr == null || Context.CargoPortSvc == null || Context.OBWorkflowSvc == null || Context.OrderDelivery == null)
			return;

		int remainingCapacity = launchVendor.CapsuleCapacity;
		int pickedCount = 0;

		if (remainingCapacity <= 0)
		{
			Debug.LogWarning($"[LaunchVendorPickupService] Vendor '{launchVendor.VendorName}' has no pickup capacity.");
			return;
		}

		uint destinationBuildingId = Context.OBWorkflowSvc.LoadingDestinationBuildingId;
		if (destinationBuildingId != 0)
		{
			pickedCount += PickFromBuilding(launchVendor, destinationBuildingId, ref remainingCapacity);
			ReportPickupResult(launchVendor, pickedCount, remainingCapacity);
			return;
		}

		IReadOnlyList<Building> buildings = Context.BuildingMgr.RegisteredBuildings;
		for (int i = 0; i < buildings.Count && remainingCapacity > 0; ++i)
		{
			Building building = buildings[i];
			if (building == null || building.RuntimeBuildingId == 0)
				continue;

			pickedCount += PickFromBuilding(launchVendor, building.RuntimeBuildingId, ref remainingCapacity);
		}

		ReportPickupResult(launchVendor, pickedCount, remainingCapacity);
	}

	private static void ReportPickupResult(LaunchServiceVendor launchVendor, int pickedCount, int remainingCapacity)
	{
		string vendorName = launchVendor != null && string.IsNullOrWhiteSpace(launchVendor.VendorName) == false
			? launchVendor.VendorName
			: "Launch Vendor";

		string message = $"{vendorName} picked up {pickedCount} capsule(s). Capacity left: {remainingCapacity}.";
		Debug.Log($"[LaunchVendorPickupService] {message}");
		Context.HudEventManager?.Publish(HudEventType.Info, message);
	}

	private int PickFromBuilding(LaunchServiceVendor vendor, uint buildingId, ref int remainingCapacity)
	{
		queryBuffer.Clear();
		if (Context.CargoPortSvc.TryQueryPorts(buildingId, queryBuffer, IsReadyOutboundPort) == false)
			return 0;

		int pickedCount = 0;
		for (int i = 0; i < queryBuffer.Count && remainingCapacity > 0; ++i)
		{
			OutboundCargoPort port = queryBuffer[i] as OutboundCargoPort;
			if (port == null || IsReadyOutboundPort(port) == false)
				continue;

			if (TryPickupPortCargo(vendor, port))
			{
				--remainingCapacity;
				++pickedCount;
			}
		}

		return pickedCount;
	}

	private static bool TryPickupPortCargo(LaunchServiceVendor vendor, OutboundCargoPort port)
	{
		if (port.TryUndockCapsule(out CargoCapsule capsule) == false)
			return false;

		int reported = Context.OBWorkflowSvc.ReportOutboundProgressFromManifest(capsule, PackageOutboundStage.InDelivery);
		if (reported <= 0)
			Debug.LogWarning($"[LaunchVendorPickupService] Picked up capsule #{capsule.BoxId} without manifest delivery progress.");

		float deliveryDuration = Context.GameTime != null ? Context.GameTime.WeekToSeconds(4) : 0.0f;
		Context.OrderDelivery.DeliverCargo(capsule, deliveryDuration);

		Debug.Log($"[LaunchVendorPickupService] {vendor.VendorName} picked up capsule #{capsule.BoxId} from {port.name}.");

		return true;
	}

	private static bool IsReadyOutboundPort(CargoPort port)
	{
		return port is OutboundCargoPort &&
			port.DockedCapsule != null &&
			port.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB &&
			IsPackedCargo(port.DockedCapsule);
	}

	private static bool IsPackedCargo(BoxBase cargo)
	{
		if (cargo == null || cargo.Stacks == null || cargo.Stacks.Count <= 0)
			return false;

		foreach (ItemStack stack in cargo.Stacks)
		{
			if (stack == null || stack.HasStatus(ItemStatus.Packed) == false)
				return false;
		}

		return true;
	}

}
