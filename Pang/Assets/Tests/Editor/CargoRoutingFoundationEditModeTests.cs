using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class CargoProcessStageEvaluatorEditModeTests
{
	[Test]
	public void TryEvaluate_EmptyCargo_ReturnsFalse()
	{
		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(
				Array.Empty<ItemStack>(),
				manifest: null,
				launchReady: false,
				out CargoProcessStage stage),
			Is.False);
		Assert.That(stage, Is.EqualTo(CargoProcessStage.None));
	}

	[TestCase(ItemStatus.None, CargoProcessStage.Unlabeled)]
	[TestCase(ItemStatus.Labeled, CargoProcessStage.Labeled)]
	[TestCase(ItemStatus.Packed, CargoProcessStage.Packed)]
	public void TryEvaluate_UniformPhysicalStatus_ReturnsExpectedStage(
		ItemStatus status,
		CargoProcessStage expected)
	{
		List<ItemStack> stacks = new()
		{
			CreateStack(101, 3, status),
			CreateStack(102, 2, status),
		};

		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest: null,
				launchReady: false,
				out CargoProcessStage stage),
			Is.True);
		Assert.That(stage, Is.EqualTo(expected));
	}

	[Test]
	public void TryEvaluate_CompletePickedManifest_ReturnsPicked()
	{
		List<ItemStack> stacks = new()
		{
			CreateStack(201, 4, ItemStatus.Labeled),
		};
		PickingManifest manifest = new();
		manifest.AddPicked(new OrderLine(null, 201, 4, null), 201, 4);

		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest,
				launchReady: false,
				out CargoProcessStage stage),
			Is.True);
		Assert.That(stage, Is.EqualTo(CargoProcessStage.Picked));
	}

	[Test]
	public void TryEvaluate_PickedManifestMismatch_DoesNotFallBackToLabeled()
	{
		List<ItemStack> stacks = new()
		{
			CreateStack(202, 4, ItemStatus.Labeled),
		};
		PickingManifest manifest = new();
		manifest.AddPicked(new OrderLine(null, 202, 3, null), 202, 3);

		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest,
				launchReady: false,
				out CargoProcessStage stage),
			Is.False);
		Assert.That(stage, Is.EqualTo(CargoProcessStage.None));
	}

	[TestCase(false, CargoProcessStage.Packed)]
	[TestCase(true, CargoProcessStage.LaunchReady)]
	public void TryEvaluate_CompletePackedManifest_UsesExplicitLaunchReadiness(
		bool launchReady,
		CargoProcessStage expected)
	{
		List<ItemStack> stacks = new()
		{
			CreateStack(301, 5, ItemStatus.Packed),
		};
		PickingManifest manifest = new();
		manifest.AddPacked(new OrderLine(null, 301, 5, null), 301, 5);

		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest,
				launchReady,
				out CargoProcessStage stage),
			Is.True);
		Assert.That(stage, Is.EqualTo(expected));
	}

	[Test]
	public void TryEvaluate_MixedOrWasteCargo_ReturnsFalse()
	{
		List<ItemStack> mixed = new()
		{
			CreateStack(401, 1, ItemStatus.None),
			CreateStack(402, 1, ItemStatus.Labeled),
		};
		List<ItemStack> waste = new()
		{
			CreateStack(403, 1, ItemStatus.Labeled, ItemQuality.Waste),
		};

		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(mixed, null, false, out _),
			Is.False);
		Assert.That(
			CargoProcessStageEvaluator.TryEvaluate(waste, null, false, out _),
			Is.False);
	}

	[Test]
	public void FacilityRule_CargoProcessStage_IsExactForAggregateCapsuleFilters()
	{
		FacilityRule rule = new();
		rule.SetRequiredCargoProcessStage(CargoProcessStage.Picked);

		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(cargoProcessStage: CargoProcessStage.Labeled)),
			Is.False);
		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(cargoProcessStage: CargoProcessStage.Picked)),
			Is.True);
		Assert.That(
			rule.IsFilterCapable(FacilityFilter.None),
			Is.True,
			"Legacy non-capsule queries do not carry an aggregate stage during the staged migration.");
	}

	[Test]
	public void TransferFilter_ExplicitStageEnforcesRuleAndLegacyTransferRemainsUnstaged()
	{
		FacilityRule pickedRule = new();
		pickedRule.SetRequiredCargoProcessStage(CargoProcessStage.Picked);
		FacilityRule packedRule = new();
		packedRule.SetRequiredCargoProcessStage(CargoProcessStage.Packed);
		FacilityRule emptyPickedRule = new();
		emptyPickedRule.SetRequiredCargoProcessStage(CargoProcessStage.Picked);
		emptyPickedRule.SetRequiredCapsuleBufferState(CapsuleBufferStateRequirement.Empty);
		FacilityRule insidePickedRule = new();
		insidePickedRule.SetRequiredCargoProcessStage(CargoProcessStage.Picked);
		insidePickedRule.SetRequiredCapsuleBufferState(CapsuleBufferStateRequirement.Inside);

		FacilityFilter legacyTransfer = FacilityFilter.ForTransfer(
			source: null,
			itemId: 1,
			quantity: 1);
		FacilityFilter pickedTransfer = FacilityFilter.WithCapsuleBufferState(
			FacilityFilter.WithCargoProcessStage(
				legacyTransfer,
				CargoProcessStage.Picked),
			CapsuleBufferStateRequirement.Inside);
		FacilityFilter packedTransfer = FacilityFilter.WithCapsuleBufferState(
			FacilityFilter.WithCargoProcessStage(
				legacyTransfer,
				CargoProcessStage.Packed),
			CapsuleBufferStateRequirement.Inside);

		Assert.That(legacyTransfer.CargoProcessStage, Is.EqualTo(CargoProcessStage.None));
		Assert.That(pickedRule.IsFilterCapable(legacyTransfer), Is.True);
		Assert.That(pickedRule.IsFilterCapable(pickedTransfer), Is.True);
		Assert.That(packedRule.IsFilterCapable(pickedTransfer), Is.False);
		Assert.That(pickedRule.IsFilterCapable(packedTransfer), Is.False);
		Assert.That(packedRule.IsFilterCapable(packedTransfer), Is.True);
		Assert.That(pickedTransfer.CapsuleBufferState, Is.EqualTo(CapsuleBufferStateRequirement.Inside));
		Assert.That(emptyPickedRule.IsFilterCapable(pickedTransfer), Is.False);
		Assert.That(insidePickedRule.IsFilterCapable(pickedTransfer), Is.True);
	}

	[Test]
	public void FacilityRule_CapsuleBufferState_IsExactForCapsuleFilters()
	{
		FacilityRule rule = new();
		rule.SetRequiredCapsuleBufferState(CapsuleBufferStateRequirement.Empty);

		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(
				capsuleBufferState: CapsuleBufferStateRequirement.Inside)),
			Is.False);
		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(
				capsuleBufferState: CapsuleBufferStateRequirement.Empty)),
			Is.True);
		Assert.That(
			rule.IsFilterCapable(FacilityFilter.None),
			Is.True,
			"Legacy non-capsule queries do not carry a capsule buffer state.");
	}

	[Test]
	public void Building_DefaultOutboundTargetStage_PreservesCurrentSpecializedRoles()
	{
		Assert.That(
			new Building("Staging", new List<GridCell>(), BuildingType.Staging).OutboundTargetStage,
			Is.EqualTo(CargoProcessStage.Labeled));
		Assert.That(
			new Building("Storage", new List<GridCell>(), BuildingType.Storage).OutboundTargetStage,
			Is.EqualTo(CargoProcessStage.Picked));
		Assert.That(
			new Building("Packing", new List<GridCell>(), BuildingType.Packing).OutboundTargetStage,
			Is.EqualTo(CargoProcessStage.Packed));
		Assert.That(
			new Building("Launch", new List<GridCell>(), BuildingType.Launch).OutboundTargetStage,
			Is.EqualTo(CargoProcessStage.LaunchReady));
		Assert.That(
			new Building("Generic", new List<GridCell>(), BuildingType.Generic).OutboundTargetStage,
			Is.EqualTo(CargoProcessStage.None));
	}

	[Test]
	public void GameSaveData_UsesCurrentBreakingSchemaVersion()
	{
		Assert.That(GameSaveData.CurrentVersion, Is.EqualTo(16));
		Assert.That(new GameSaveData().Version, Is.EqualTo(GameSaveData.CurrentVersion));
	}

	private static ItemStack CreateStack(
		uint itemId,
		int quantity,
		ItemStatus status,
		ItemQuality quality = ItemQuality.None)
	{
		ItemStack stack = new(itemId, status: status, quality: quality);
		Assert.That(stack.AddItem(quantity), Is.EqualTo(quantity));
		return stack;
	}
}

public sealed class CapsuleBufferRuleQueryEditModeTests
{
	private const uint FirstBuildingId = 11;
	private const uint SecondBuildingId = 22;

	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private FacilityManager facilityManager;
	private FacilityRuleManager ruleManager;
	private BuildingManager buildingManager;
	private CapsuleDockService dockService;
	private CapsuleBufferService bufferService;
	private OutboundWorkflowService outboundWorkflow;
	private uint nextBoxId;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		GridService gridService = CreateComponent<GridService>("Cargo Rule Query Grid");
		facilityManager = CreateComponent<FacilityManager>("Cargo Rule Query Facility Manager");
		buildingManager = CreateComponent<BuildingManager>("Cargo Rule Query Building Manager", active: false);
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");
		GameObject ruleManagerObject = CreateGameObject("Cargo Rule Query Rule Manager", active: false);
		ruleManager = ruleManagerObject.AddComponent<FacilityRuleManager>();
		outboundWorkflow = CreateComponent<OutboundWorkflowService>("Cargo Rule Query Outbound", active: false);
		GameObject dockServiceObject = CreateGameObject("Cargo Rule Query Dock Service", active: false);
		dockService = dockServiceObject.AddComponent<CapsuleDockService>();
		GameObject serviceObject = CreateGameObject("Cargo Rule Query Buffer Service", active: false);
		bufferService = serviceObject.AddComponent<CapsuleBufferService>();

		GameObject contextObject = CreateGameObject("Cargo Rule Query Context", active: false);
		GameContext context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "gridService", gridService);
		SetPrivateField(typeof(GameContext), context, "facilityManager", facilityManager);
		SetPrivateField(typeof(GameContext), context, "facilityRuleManager", ruleManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "capsuleDockService", dockService);
		SetPrivateField(typeof(GameContext), context, "capsuleBufferService", bufferService);
		SetPrivateField(typeof(GameContext), context, "outboundWorkflowService", outboundWorkflow);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		ruleManagerObject.SetActive(true);
		dockServiceObject.SetActive(true);
		serviceObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<CapsuleDock>),
			dockService,
			"TryBindFacilityManager");
		InvokeNonPublic(
			typeof(FacilityService<CapsuleBuffer>),
			bufferService,
			"TryBindFacilityManager");

		nextBoxId = 1;
	}

	[TearDown]
	public void TearDown()
	{
		SetPrivateStaticField(typeof(GameContext), "instance", null);
		for (int i = createdObjects.Count - 1; i >= 0; --i)
		{
			if (createdObjects[i] != null)
				UnityEngine.Object.DestroyImmediate(createdObjects[i]);
		}

		createdObjects.Clear();
		SetPrivateStaticField(typeof(GameContext), "instance", previousContext);
	}

	[Test]
	public void Query_UsesExactStageExplicitRuleAndBuildingScope()
	{
		CargoCapsule labeledCapsule = CreateCapsule("Labeled Capsule", 501, 3, ItemStatus.Labeled);
		CapsuleBuffer noRule = CreateBuffer("No Rule", FirstBuildingId, 1);
		CapsuleBuffer wrongStage = CreateBuffer("Wrong Stage", FirstBuildingId, 2);
		CapsuleBuffer matching = CreateBuffer("Matching", FirstBuildingId, 3);
		CapsuleBuffer otherBuilding = CreateBuffer("Other Building", SecondBuildingId, 4);

		ApplyRule(wrongStage, CargoProcessStage.Packed);
		ApplyRule(matching, CargoProcessStage.Labeled);
		ApplyRule(otherBuilding, CargoProcessStage.Labeled);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				labeledCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { matching }));
		Assert.That(results.Contains(noRule), Is.False);
		Assert.That(results.Contains(wrongStage), Is.False);
		Assert.That(results.Contains(otherBuilding), Is.False);

		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				0,
				labeledCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EquivalentTo(new[] { matching, otherBuilding }));
	}

	[Test]
	public void Query_OrdersRuleMatchedDestinationsByDescendingPriority()
	{
		CargoCapsule labeledCapsule = CreateCapsule("Priority Capsule", 511, 2, ItemStatus.Labeled);
		CapsuleBuffer lowPriority = CreateBuffer("Low Priority", FirstBuildingId, 20);
		CapsuleBuffer highPriority = CreateBuffer("High Priority", FirstBuildingId, 21);
		CapsuleBuffer equalPriority = CreateBuffer("Equal Priority", FirstBuildingId, 22);
		ApplyRule(lowPriority, CargoProcessStage.Labeled, priority: 1);
		ApplyRule(highPriority, CargoProcessStage.Labeled, priority: 10);
		ApplyRule(equalPriority, CargoProcessStage.Labeled, priority: 10);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				labeledCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { highPriority, equalPriority, lowPriority }));
	}

	[Test]
	public void RuleSave_RoundTripsCapsuleBufferStateAndCargoProcessStage()
	{
		FacilityRule rule = new();
		rule.SetRequiredCapsuleBufferState(CapsuleBufferStateRequirement.Inside);
		rule.SetRequiredCargoProcessStage(CargoProcessStage.Packed);
		FacilityRulePreset preset = ruleManager.CreatePreset("Round Trip Rule", rule);

		FacilityRuleManagerSaveData save = ruleManager.CaptureState();
		ruleManager.RestoreState(save);

		Assert.That(ruleManager.TryGetPreset(preset.Id, out FacilityRulePreset restored), Is.True);
		Assert.That(
			restored.Rule.RequiredCapsuleBufferState,
			Is.EqualTo(CapsuleBufferStateRequirement.Inside));
		Assert.That(restored.Rule.RequiredCargoProcessStage, Is.EqualTo(CargoProcessStage.Packed));
	}

	[Test]
	public void Query_ExplicitEmptyRuleActsAsCatchAllAndPredicateCanExcludeIt()
	{
		CargoCapsule unlabeledCapsule = CreateCapsule("Unlabeled Capsule", 601, 2, ItemStatus.None);
		CapsuleBuffer catchAll = CreateBuffer("Catch All", FirstBuildingId, 5);
		ApplyRule(catchAll, CargoProcessStage.None);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				unlabeledCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { catchAll }));

		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				unlabeledCapsule,
				results,
				evaluateLaunchReadiness: false,
				candidate => candidate != catchAll),
			Is.False);
		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Query_ManifestRuleUsesCapsuleManifestAndRejectsMissingManifest()
	{
		CargoCapsule pickedCapsule = CreateCapsule("Mars Picked Capsule", 701, 4, ItemStatus.Labeled);
		Order marsOrder = new()
		{
			Destination = OrderDestination.Mars,
			Lines = new List<OrderLine>(),
		};
		OrderLine marsLine = new(marsOrder, 701, 4, null);
		marsOrder.Lines.Add(marsLine);
		outboundWorkflow.GetPickingManifest(pickedCapsule).AddPicked(marsLine, 701, 4);

		CargoCapsule noManifestCapsule = CreateCapsule("No Manifest Capsule", 702, 4, ItemStatus.Labeled);
		CapsuleBuffer marsBuffer = CreateBuffer("Mars Buffer", FirstBuildingId, 6);
		ApplyRule(
			marsBuffer,
			CargoProcessStage.Picked,
			new[] { OrderDestination.Mars });

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				pickedCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { marsBuffer }));

		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				noManifestCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.False);
		Assert.That(results, Is.Empty);
	}

	[Test]
	public void StoringPlanner_RejectsBufferRuleOwnedByStorageOutboundStage()
	{
		StorageBuilding storage = CreateStorageBuilding("Outbound-owned Storage");
		CapsuleBuffer outputBuffer = CreateBuffer(
			"Picked Output Buffer",
			storage.RuntimeBuildingId,
			30);
		CargoCapsule capsule = CreateCapsule(
			"Partial Picked Output",
			741,
			2,
			ItemStatus.Labeled);
		capsule.SetLogisticsState(CapsuleLogisticsState.Inside);
		Assert.That(outputBuffer.TryDockCapsule(capsule), Is.True);
		ApplyRule(outputBuffer, storage.OutboundTargetStage);

		StoringPlanner planner = new(bufferService);
		Assert.That(
			planner.HasPendingCollectWork(storage.RuntimeBuildingId),
			Is.False,
			"The Buffer Rule already assigns this partial capsule to the Building outbound stage.");
	}

	[Test]
	public void StoringPlanner_StopsConsumingInputBufferAfterPickingManifestAppears()
	{
		StorageBuilding storage = CreateStorageBuilding("Manifest Storage");
		CapsuleBuffer inputBuffer = CreateBuffer(
			"Labeled Input Buffer",
			storage.RuntimeBuildingId,
			31);
		CargoCapsule capsule = CreateCapsule(
			"Manifest Transition Capsule",
			742,
			2,
			ItemStatus.Labeled);
		capsule.SetLogisticsState(CapsuleLogisticsState.Inside);
		Assert.That(inputBuffer.TryDockCapsule(capsule), Is.True);
		ApplyRule(inputBuffer, CargoProcessStage.Labeled);

		StoringPlanner planner = new(bufferService);
		Assert.That(planner.HasPendingCollectWork(storage.RuntimeBuildingId), Is.True);

		OrderLine line = new(null, 742, 2, null);
		outboundWorkflow.GetPickingManifest(capsule).AddPicked(line, 742, 2);

		Assert.That(
			planner.HasPendingCollectWork(storage.RuntimeBuildingId),
			Is.False,
			"Manifest-owned outbound cargo must not be returned to storage shelves.");
	}

	[Test]
	public void StoringPlanner_DoesNotConsumeExplicitRuleMismatchBeforeRelocationSettles()
	{
		StorageBuilding storage = CreateStorageBuilding("Rule-mismatch Storage");
		CapsuleBuffer buffer = CreateBuffer(
			"Packed-only Input Buffer",
			storage.RuntimeBuildingId,
			32);
		CargoCapsule capsule = CreateCapsule(
			"Labeled Mismatch Capsule",
			743,
			2,
			ItemStatus.Labeled);
		capsule.SetLogisticsState(CapsuleLogisticsState.Inside);
		Assert.That(buffer.TryDockCapsule(capsule), Is.True);
		ApplyRule(buffer, CargoProcessStage.Packed);

		StoringPlanner planner = new(bufferService);
		Assert.That(
			planner.HasPendingCollectWork(storage.RuntimeBuildingId),
			Is.False,
			"An explicit Rule mismatch belongs to Capsule Relocate, not Storing.");
	}

	[Test]
	public void Query_AggregateManifestRequiresEveryDestinationToBeAllowed()
	{
		CargoCapsule mixedDestinationCapsule =
			CreateCapsule("Mixed Destination Capsule", 711, 2, ItemStatus.Labeled);
		AddStack(mixedDestinationCapsule, 712, 3, ItemStatus.Labeled);

		Order marsOrder = new()
		{
			Destination = OrderDestination.Mars,
			Lines = new List<OrderLine>(),
		};
		Order titanOrder = new()
		{
			Destination = OrderDestination.Titan,
			Lines = new List<OrderLine>(),
		};
		OrderLine marsLine = new(marsOrder, 711, 2, null);
		OrderLine titanLine = new(titanOrder, 712, 3, null);
		marsOrder.Lines.Add(marsLine);
		titanOrder.Lines.Add(titanLine);
		PickingManifest manifest = outboundWorkflow.GetPickingManifest(mixedDestinationCapsule);
		manifest.AddPicked(marsLine, 711, 2);
		manifest.AddPicked(titanLine, 712, 3);

		CapsuleBuffer marsOnly = CreateBuffer("Mars Only", FirstBuildingId, 8);
		CapsuleBuffer bothDestinations = CreateBuffer("Mars And Titan", FirstBuildingId, 9);
		ApplyRule(marsOnly, CargoProcessStage.Picked, new[] { OrderDestination.Mars });
		ApplyRule(
			bothDestinations,
			CargoProcessStage.Picked,
			new[] { OrderDestination.Mars, OrderDestination.Titan });

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				mixedDestinationCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { bothDestinations }));
	}

	[Test]
	public void Query_LaunchContextExplicitlyDistinguishesLaunchReadyFromPacked()
	{
		CargoCapsule capsule = CreateCapsule("Launch Ready Capsule", 721, 5, ItemStatus.Packed);
		OrderLine line = new(null, 721, 5, null);
		outboundWorkflow.GetPickingManifest(capsule).AddPacked(line, 721, 5);

		CapsuleBuffer packed = CreateBuffer("Packed", FirstBuildingId, 10);
		CapsuleBuffer launchReady = CreateBuffer("Launch Ready", FirstBuildingId, 11);
		ApplyRule(packed, CargoProcessStage.Packed);
		ApplyRule(launchReady, CargoProcessStage.LaunchReady);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				capsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { packed }));

		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				capsule,
				results,
				evaluateLaunchReadiness: true),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { launchReady }));
	}

	[Test]
	public void Query_ExcludesOccupiedWrongRouteAndInvalidatingBuffers()
	{
		CargoCapsule capsule = CreateCapsule("Eligible Capsule", 731, 1, ItemStatus.Labeled);
		CapsuleBuffer occupied = CreateBuffer("Occupied", FirstBuildingId, 12);
		CapsuleBuffer wrongRoute = CreateBuffer("Wrong Route", FirstBuildingId, 13);
		CapsuleBuffer invalidating = CreateBuffer("Invalidating", FirstBuildingId, 14);
		CapsuleBuffer available = CreateBuffer("Available", FirstBuildingId, 15);
		ApplyRule(occupied, CargoProcessStage.Labeled);
		ApplyRule(wrongRoute, CargoProcessStage.Labeled);
		ApplyRule(invalidating, CargoProcessStage.Labeled);
		ApplyRule(available, CargoProcessStage.Labeled);

		Assert.That(occupied.TryDockCapsule(CreateCapsule("Occupant")), Is.True);
		SetPrivateField(
			typeof(CapsuleDock),
			wrongRoute,
			"acceptedCargoRouteKind",
			CargoRouteKind.Waste);
		HashSet<IFacility> invalidatingFacilities =
			(HashSet<IFacility>)GetPrivateField(
				typeof(FacilityManager),
				facilityManager,
				"invalidatingFacilities");
		invalidatingFacilities.Add(invalidating);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				capsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { available }));
	}

	[Test]
	public void Query_EmptyCapsuleUsesExplicitEmptyRuleWhileMixedCargoIsRejected()
	{
		CargoCapsule emptyCapsule = CreateCapsule("Empty Capsule");
		CargoCapsule mixedCapsule = CreateCapsule("Mixed Capsule");
		AddStack(mixedCapsule, 801, 1, ItemStatus.None);
		AddStack(mixedCapsule, 802, 1, ItemStatus.Labeled);
		CapsuleBuffer emptyOnly = CreateBuffer("Empty Only", FirstBuildingId, 7);
		CapsuleBuffer contradictoryEmpty = CreateBuffer("Contradictory Empty", FirstBuildingId, 8);
		ApplyRule(
			emptyOnly,
			CargoProcessStage.None,
			bufferState: CapsuleBufferStateRequirement.Empty);
		ApplyRule(
			contradictoryEmpty,
			CargoProcessStage.Labeled,
			bufferState: CapsuleBufferStateRequirement.Empty);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				emptyCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(results, Is.EqualTo(new[] { emptyOnly }));
		Assert.That(results.Contains(contradictoryEmpty), Is.False);

		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				mixedCapsule,
				results,
				evaluateLaunchReadiness: false),
			Is.False);
		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Coordinator_RuleSendUsesRuleMatchedBufferInsteadOfLegacyDockState()
	{
		CargoCapsule capsule = CreateCapsule("Inbound Labeled Capsule", 811, 2, ItemStatus.Labeled);
		capsule.SetLogisticsState(CapsuleLogisticsState.IB);
		InboundCargoPort source = CreateComponent<InboundCargoPort>("Inbound Source", active: false);
		source.OnPositionSet(new int3(18, 0, 1), FacingDirection.North);
		facilityManager.RegisterFacility(FirstBuildingId, source);
		Assert.That(source.TryDockCapsule(capsule), Is.True);

		CapsuleBuffer target = CreateBuffer("Rule Target", FirstBuildingId, 19);
		target.SetDockState(CapsuleDockState.OBStandby);
		ApplyRule(
			target,
			CargoProcessStage.Labeled,
			bufferState: CapsuleBufferStateRequirement.Inside);
		List<CapsuleBuffer> ruleMatches = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				capsule,
				ruleMatches,
				evaluateLaunchReadiness: false),
			Is.True);
		Assert.That(ruleMatches, Is.EqualTo(new[] { target }));
		Assert.That(bufferService.TryGetRegisteredBuildingId(target, out uint targetBuildingId), Is.True);
		Assert.That(targetBuildingId, Is.EqualTo(FirstBuildingId));
		Assert.That(source.DockedCapsule, Is.SameAs(capsule));
		Assert.That(source.CanGetBox(), Is.True);
		Assert.That(target.CanPutBox(), Is.True);

		CapsuleRelocateMatch matched = default;
		bool callbackInvoked = false;
		CapsuleRelocateCoordinator coordinator = new(dockService, bufferService: bufferService);
		bool accepted = coordinator.RequestSend(new CapsuleRelocateSendRequest(
			source,
			source.DockState,
			CapsuleLogisticsState.IB,
			CapsuleDockState.Empty,
			CapsuleRelocateScope.SameBuilding,
			FirstBuildingId,
			onMatched: match =>
			{
				matched = match;
				callbackInvoked = true;
				return true;
			},
			requireRuleMatchedTarget: true));

		Assert.That(accepted, Is.True);
		Assert.That(callbackInvoked, Is.True);
		Assert.That(matched.SourceDock, Is.SameAs(source));
		Assert.That(matched.TargetDock, Is.SameAs(target));
		Assert.That(target.DockState, Is.EqualTo(CapsuleDockState.OBStandby));
	}

	private CapsuleBuffer CreateBuffer(string name, uint buildingId, int x)
	{
		CapsuleBuffer buffer = CreateComponent<CapsuleBuffer>(name, active: false);
		buffer.OnPositionSet(new int3(x, 0, 1), FacingDirection.North);
		facilityManager.RegisterFacility(buildingId, buffer);
		return buffer;
	}

	private StorageBuilding CreateStorageBuilding(string name)
	{
		StorageBuilding building = new(name, new List<GridCell>());
		buildingManager.Register(building);
		Assert.That(building.RuntimeBuildingId, Is.Not.Zero);
		return building;
	}

	private void ApplyRule(
		CapsuleBuffer buffer,
		CargoProcessStage stage,
		IEnumerable<OrderDestination> destinations = null,
		CapsuleBufferStateRequirement bufferState = CapsuleBufferStateRequirement.Any,
		int priority = 0)
	{
		FacilityRule rule = new();
		rule.SetPriority(priority);
		rule.SetRequiredCargoProcessStage(stage);
		rule.SetRequiredCapsuleBufferState(bufferState);
		if (destinations != null)
		{
			FacilityManifestRule manifestRule = new();
			manifestRule.SetRequiredDestinations(destinations);
			rule.SetManifestRule(manifestRule);
		}

		FacilityRulePreset preset = ruleManager.CreatePreset($"{buffer.name} Rule", rule);
		Assert.That(ruleManager.ApplyPreset(buffer, preset.Id), Is.True);
	}

	private CargoCapsule CreateCapsule(
		string name,
		uint itemId = 0,
		int quantity = 0,
		ItemStatus status = ItemStatus.None)
	{
		CargoCapsule capsule = CreateComponent<CargoCapsule>(name, active: false);
		SetPrivateField(typeof(BoxBase), capsule, "boxType", BoxType.Capsule);
		capsule.SetBoxId(nextBoxId++);
		if (itemId != 0 && quantity > 0)
			AddStack(capsule, itemId, quantity, status);
		return capsule;
	}

	private static void AddStack(
		CargoCapsule capsule,
		uint itemId,
		int quantity,
		ItemStatus status)
	{
		ItemStack stack = new(itemId, status: status);
		Assert.That(stack.AddItem(quantity), Is.EqualTo(quantity));

		List<ItemStack> stacks =
			(List<ItemStack>)GetPrivateField(typeof(BoxBase), capsule, "stacks");
		Dictionary<uint, int> totals =
			(Dictionary<uint, int>)GetPrivateField(typeof(BoxBase), capsule, "itemTotals");
		stacks.Add(stack);
		totals[itemId] = totals.GetValueOrDefault(itemId) + quantity;
	}

	private T CreateComponent<T>(string objectName, bool active = true) where T : Component
	{
		return CreateGameObject(objectName, active).AddComponent<T>();
	}

	private GameObject CreateGameObject(string objectName, bool active = true)
	{
		GameObject gameObject = new(objectName);
		gameObject.SetActive(active);
		createdObjects.Add(gameObject);
		return gameObject;
	}

	private static object GetPrivateStaticField(Type ownerType, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		return field.GetValue(null);
	}

	private static object GetPrivateField(Type ownerType, object target, string fieldName)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		return field.GetValue(target);
	}

	private static void SetPrivateField(Type ownerType, object target, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(target, value);
	}

	private static void SetPrivateStaticField(Type ownerType, string fieldName, object value)
	{
		FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"Missing test field {ownerType.Name}.{fieldName}");
		field.SetValue(null, value);
	}

	private static void InvokeNonPublic(Type ownerType, object target, string methodName)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		method.Invoke(target, null);
	}
}

public sealed class CapsuleRelocateDirtyEditModeTests
{
	[Test]
	public void ProcessDirty_DeduplicatesAndSettlesMarksRaisedDuringEvaluation()
	{
		GameObject dockObject = new("Dirty Dock");
		dockObject.SetActive(false);
		try
		{
			CapsuleBuffer dock = dockObject.AddComponent<CapsuleBuffer>();
			List<uint> evaluatedBuildings = new();
			int evaluatedDockCount = 0;
			CapsuleRelocateCoordinator coordinator = null;
			coordinator = new CapsuleRelocateCoordinator(
				dockService: null,
				evaluateDirtyDock: evaluatedDock =>
				{
					Assert.That(evaluatedDock, Is.SameAs(dock));
					++evaluatedDockCount;
					coordinator.MarkBuildingDirty(42);
				},
				evaluateDirtyBuilding: evaluatedBuildings.Add);

			coordinator.MarkDirty(dock);
			coordinator.MarkDirty(dock);
			coordinator.MarkBuildingDirty(41);
			coordinator.MarkBuildingDirty(41);

			Assert.That(coordinator.DirtyDockCount, Is.EqualTo(1));
			Assert.That(coordinator.DirtyBuildingCount, Is.EqualTo(1));

			coordinator.ProcessDirty();

			Assert.That(evaluatedDockCount, Is.EqualTo(1));
			Assert.That(evaluatedBuildings, Is.EqualTo(new uint[] { 41, 42 }));
			Assert.That(coordinator.HasDirty, Is.False);
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(dockObject);
		}
	}
}
