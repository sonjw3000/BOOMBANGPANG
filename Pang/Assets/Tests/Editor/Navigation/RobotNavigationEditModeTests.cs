using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Save;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

public sealed class RobotNavigationEditModeTests
{
	[Test]
	public void SplitCompute_ThreeHubs_AssignsRemainderByStableHubOrder()
	{
		Dictionary<uint, int> shares = RobotNavigationAllocationMath.SplitCompute(100, new uint[] { 2, 5, 9 });

		Assert.That(shares[2], Is.EqualTo(34));
		Assert.That(shares[5], Is.EqualTo(33));
		Assert.That(shares[9], Is.EqualTo(33));
		Assert.That(shares[2] + shares[5] + shares[9], Is.EqualTo(100));
	}

	[Test]
	public void PositiveDelta_Handoff_ReservesOnlyNewHubIncrease()
	{
		Dictionary<uint, int> current = new() { [1] = 50, [2] = 50 };
		Dictionary<uint, int> target = new() { [2] = 50, [3] = 50 };

		Dictionary<uint, int> delta = RobotNavigationAllocationMath.PositiveDelta(current, target);

		Assert.That(delta, Has.Count.EqualTo(1));
		Assert.That(delta[3], Is.EqualTo(50));
	}

	[Test]
	public void CapacityReservation_ConcurrentRequests_CannotOverbook()
	{
		Assert.That(RobotNavigationAllocationMath.FitsCapacity(100, 70, 20, 10), Is.True);
		Assert.That(RobotNavigationAllocationMath.FitsCapacity(100, 70, 20, 11), Is.False);
	}

	[Test]
	public void ReservationLifecycle_CommitAndCancel_DoNotLeakReservedCompute()
	{
		int reserved = 0;
		reserved += 25;
		Assert.That(RobotNavigationAllocationMath.FitsCapacity(100, 50, reserved, 25), Is.True);
		reserved -= 25;
		Assert.That(reserved, Is.Zero, "cancel must release the reservation");
		reserved += 25;
		reserved -= 25;
		int assigned = 75;
		Assert.That(reserved, Is.Zero, "commit must release the reservation before assigning");
		Assert.That(assigned, Is.EqualTo(75));
	}

	[TestCase(RobotNavigationDependency.OnboardCompute, 0, true, false)]
	[TestCase(RobotNavigationDependency.FullyAutonomous, 0, false, false)]
	[TestCase(RobotNavigationDependency.HubOrchestrated, 75, true, true)]
	public void NavigationDependencies_ExposeCoverageAndComputePolicy(
		RobotNavigationDependency dependency,
		int expectedCompute,
		bool expectedCoverage,
		bool expectedOrchestration)
	{
		GameObject gameObject = new("Navigation Test Robot");
		try
		{
			RobotWorker robot = gameObject.AddComponent<RobotWorker>();
			SerializedObject serialized = new(robot);
			serialized.FindProperty("navigationDependency").enumValueIndex = (int)dependency;
			serialized.FindProperty("requiredNavigationCompute").intValue = 75;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			Assert.That(robot.RequiredNavigationCompute, Is.EqualTo(expectedCompute));
			Assert.That(robot.RequiresNavigationCoverage, Is.EqualTo(expectedCoverage));
			Assert.That(robot.RequiresOrchestrationCompute, Is.EqualTo(expectedOrchestration));
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(gameObject);
		}
	}

	[Test]
	public void SaveSchema_PersistsInputsButNotDerivedNavigationCaches()
	{
		Assert.That(new GameSaveData().Version, Is.EqualTo(GameSaveData.CurrentVersion));
		Assert.That(typeof(PlaceableSaveData).GetField("OwnerNavigationHubSaveId"), Is.Not.Null);
		Assert.That(typeof(WorkerSaveData).GetField("HasNavigationProfile"), Is.Not.Null);
		Assert.That(typeof(WorkerSaveData).GetField("NavigationDependency"), Is.Not.Null);
		Assert.That(typeof(WorkerSaveData).GetField("RequiredNavigationCompute"), Is.Not.Null);
		Assert.That(typeof(WorkerSaveData).GetField("NavigationRegionId"), Is.Null);
		Assert.That(typeof(WorkerSaveData).GetField("NavigationCoverageVersion"), Is.Null);
		Assert.That(typeof(PlaceableSaveData).GetField("RuntimeHubId"), Is.Null);
	}

	[Test]
	public void SaveSchema_RoundTripsRelayOwnerRobotProfileAndRescueGoal()
	{
		GameSaveData source = new();
		source.Placeables.Add(new PlaceableSaveData
		{
			SaveId = 7,
			OwnerNavigationHubSaveId = 3,
			IsWorker = true,
			Worker = new WorkerSaveData
			{
				HasNavigationProfile = true,
				NavigationDependency = RobotNavigationDependency.HubOrchestrated,
				RequiredNavigationCompute = 125,
				NavigationRescueOverride = true,
				HasNavigationRescueGoal = true,
				NavigationRescueGoal = new Int3SaveData(12, 0, 18),
			},
		});

		GameSaveData restored = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(source));
		PlaceableSaveData placeable = restored.Placeables[0];
		Assert.That(placeable.OwnerNavigationHubSaveId, Is.EqualTo(3));
		Assert.That(placeable.Worker.HasNavigationProfile, Is.True);
		Assert.That(placeable.Worker.RequiredNavigationCompute, Is.EqualTo(125));
		Assert.That(placeable.Worker.NavigationRescueOverride, Is.True);
		Assert.That(placeable.Worker.NavigationRescueGoal.X, Is.EqualTo(12));
		Assert.That(placeable.Worker.NavigationRescueGoal.Z, Is.EqualTo(18));
	}

	[Test]
	public void SameRegionTransition_GuardPrecedesShareAllocation()
	{
		MethodInfo method = typeof(RobotNavigationService).GetMethod(
			"TryReserveTransition",
			BindingFlags.Instance | BindingFlags.Public);
		Assert.That(method, Is.Not.Null);
		string source = System.IO.File.ReadAllText("Assets/Scripts/Placeable/Navigation/RobotNavigationService.cs");
		int sameRegionGuard = source.IndexOf("if (targetRegionId == robot.NavigationRegionId)", StringComparison.Ordinal);
		int shareAllocation = source.IndexOf("Dictionary<uint, int> targetShares = BuildShares", sameRegionGuard, StringComparison.Ordinal);
		Assert.That(sameRegionGuard, Is.GreaterThanOrEqualTo(0));
		Assert.That(shareAllocation, Is.GreaterThan(sameRegionGuard), "same-region movement must return without transition allocations");
	}

	[Test]
	public void HubAndRelayFailure_RebuildsCoverageAndRecovers()
	{
		GameObject gridObject = new("Navigation Test Grid");
		GameObject facilityObject = new("Navigation Test Facilities");
		GameObject serviceObject = new("Navigation Test Service");
		PowerVendor vendor = null;
		GameObject powerObject = null;
		GameObject hubObject = null;
		GameObject relayObject = null;
		try
		{
			GridService grid = gridObject.AddComponent<GridService>();
			grid.BuildDefaultMap();
			FacilityManager facilities = facilityObject.AddComponent<FacilityManager>();
			RobotNavigationService service = serviceObject.AddComponent<RobotNavigationService>();
			service.Bind(facilities, grid, null, null);

			NavigationHub hub = CreatePoweredHub(new int3(20, 0, 20), out hubObject, out powerObject, out vendor);
			facilities.RegisterFacility(0, hub);
			relayObject = new GameObject("Relay");
			RelayNode relay = relayObject.AddComponent<RelayNode>();
			relay.OnPositionSet(new int3(28, 0, 20), FacingDirection.North);
			facilities.RegisterFacility(0, relay);

			int3 relayOnlyCell = new(34, 0, 20);
			Assert.That(relay.IsConnected, Is.True);
			Assert.That(service.IsCellCovered(relayOnlyCell), Is.True);

			relay.ApplyDamage(100000f);
			service.RebuildRuntimeState();
			Assert.That(service.IsCellCovered(relayOnlyCell), Is.False);
			relay.RestoreHealth(relay.MaxHealth);
			service.RebuildRuntimeState();
			Assert.That(service.IsCellCovered(relayOnlyCell), Is.True);

			hub.ApplyDamage(100000f);
			service.RebuildRuntimeState();
			Assert.That(service.IsCellCovered(hub.GridPosition), Is.False);
			hub.RestoreHealth(hub.MaxHealth);
			service.RebuildRuntimeState();
			Assert.That(service.IsCellCovered(relayOnlyCell), Is.True);
		}
		finally
		{
			if (vendor != null) UnityEngine.Object.DestroyImmediate(vendor);
			if (relayObject != null) UnityEngine.Object.DestroyImmediate(relayObject);
			if (hubObject != null) UnityEngine.Object.DestroyImmediate(hubObject);
			if (powerObject != null) UnityEngine.Object.DestroyImmediate(powerObject);
			UnityEngine.Object.DestroyImmediate(serviceObject);
			UnityEngine.Object.DestroyImmediate(facilityObject);
			UnityEngine.Object.DestroyImmediate(gridObject);
		}
	}

	[Test]
	public void SameRegionTransition_RepeatedCheckAllocatesNoManagedMemory()
	{
		GameObject gridObject = new("Navigation Perf Grid");
		GameObject facilityObject = new("Navigation Perf Facilities");
		GameObject serviceObject = new("Navigation Perf Service");
		GameObject robotObject = new("Navigation Perf Robot");
		PowerVendor vendor = null;
		GameObject powerObject = null;
		GameObject hubObject = null;
		try
		{
			GridService grid = gridObject.AddComponent<GridService>();
			grid.BuildDefaultMap();
			FacilityManager facilities = facilityObject.AddComponent<FacilityManager>();
			RobotNavigationService service = serviceObject.AddComponent<RobotNavigationService>();
			service.Bind(facilities, grid, null, null);
			NavigationHub hub = CreatePoweredHub(new int3(20, 0, 20), out hubObject, out powerObject, out vendor);
			facilities.RegisterFacility(0, hub);

			RobotWorker robot = robotObject.AddComponent<RobotWorker>();
			SerializedObject serialized = new(robot);
			serialized.FindProperty("navigationDependency").enumValueIndex = (int)RobotNavigationDependency.OnboardCompute;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			robot.OnPositionSet(new int3(20, 0, 20), FacingDirection.North);
			Assert.That(service.CanRunAutomatic(robot, out _), Is.True);

			int3 nextCell = new(21, 0, 20);
			for (int i = 0; i < 32; ++i)
				Assert.That(service.TryReserveTransition(robot, nextCell, out _, out _), Is.True);

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 256; ++i)
				service.TryReserveTransition(robot, nextCell, out _, out _);
			long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.That(allocated, Is.Zero);
		}
		finally
		{
			if (vendor != null) UnityEngine.Object.DestroyImmediate(vendor);
			if (hubObject != null) UnityEngine.Object.DestroyImmediate(hubObject);
			if (powerObject != null) UnityEngine.Object.DestroyImmediate(powerObject);
			UnityEngine.Object.DestroyImmediate(robotObject);
			UnityEngine.Object.DestroyImmediate(serviceObject);
			UnityEngine.Object.DestroyImmediate(facilityObject);
			UnityEngine.Object.DestroyImmediate(gridObject);
		}
	}

	private static NavigationHub CreatePoweredHub(
		in int3 position,
		out GameObject hubObject,
		out GameObject powerObject,
		out PowerVendor vendor)
	{
		hubObject = new GameObject("Navigation Hub");
		NavigationHub hub = hubObject.AddComponent<NavigationHub>();
		hub.OnPositionSet(position, FacingDirection.North);
		powerObject = new GameObject("Power Hub");
		PowerHub powerHub = powerObject.AddComponent<PowerHub>();
		powerHub.OnPositionSet(position, FacingDirection.North);
		vendor = ScriptableObject.CreateInstance<PowerVendor>();
		SerializedObject vendorData = new(vendor);
		vendorData.FindProperty("vendorId").longValue = 0;
		vendorData.FindProperty("powerCapacity").intValue = 10000;
		vendorData.ApplyModifiedPropertiesWithoutUndo();
		typeof(PowerHub).GetMethod("SetActiveVendor", BindingFlags.Instance | BindingFlags.NonPublic)
			?.Invoke(powerHub, new object[] { vendor });
		typeof(NavigationHub).GetMethod("ConnectPower", BindingFlags.Instance | BindingFlags.NonPublic)
			?.Invoke(hub, new object[] { powerHub });
		Assert.That(hub.IsOperational, Is.True, "test hub must be powered");
		return hub;
	}
}
