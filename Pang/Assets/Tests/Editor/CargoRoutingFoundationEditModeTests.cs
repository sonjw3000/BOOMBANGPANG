using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ItemProcessStageEvaluatorEditModeTests
{
	[Test]
	public void TryEvaluate_EmptyCargo_ReturnsFalse()
	{
		Assert.That(
			ItemProcessStageEvaluator.TryEvaluate(
				Array.Empty<ItemStack>(),
				manifest: null,
				launchReady: false,
				out ItemProcessStage stage),
			Is.False);
		Assert.That(stage, Is.EqualTo(ItemProcessStage.Any));
	}

	[TestCase(ItemStatus.None, ItemProcessStage.Unlabeled)]
	[TestCase(ItemStatus.Labeled, ItemProcessStage.Labeled)]
	[TestCase(ItemStatus.Packed, ItemProcessStage.Packed)]
	public void TryEvaluate_UniformPhysicalStatus_ReturnsExpectedStage(
		ItemStatus status,
		ItemProcessStage expected)
	{
		List<ItemStack> stacks = new()
		{
			CreateStack(101, 3, status),
			CreateStack(102, 2, status),
		};

		Assert.That(
			ItemProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest: null,
				launchReady: false,
				out ItemProcessStage stage),
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
			ItemProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest,
				launchReady: false,
				out ItemProcessStage stage),
			Is.True);
		Assert.That(stage, Is.EqualTo(ItemProcessStage.Picked));
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
			ItemProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest,
				launchReady: false,
				out ItemProcessStage stage),
			Is.False);
		Assert.That(stage, Is.EqualTo(ItemProcessStage.Any));
	}

	[TestCase(false, ItemProcessStage.Packed)]
	[TestCase(true, ItemProcessStage.LaunchReady)]
	public void TryEvaluate_CompletePackedManifest_UsesExplicitLaunchReadiness(
		bool launchReady,
		ItemProcessStage expected)
	{
		List<ItemStack> stacks = new()
		{
			CreateStack(301, 5, ItemStatus.Packed),
		};
		PickingManifest manifest = new();
		manifest.AddPacked(new OrderLine(null, 301, 5, null), 301, 5);

		Assert.That(
			ItemProcessStageEvaluator.TryEvaluate(
				stacks,
				manifest,
				launchReady,
				out ItemProcessStage stage),
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
			ItemProcessStageEvaluator.TryEvaluate(mixed, null, false, out _),
			Is.False);
		Assert.That(
			ItemProcessStageEvaluator.TryEvaluate(waste, null, false, out _),
			Is.False);
	}

	[Test]
	public void FacilityRule_ItemProcessStages_AcceptEverySelectedStageAndRejectOthers()
	{
		FacilityRule rule = new();
		rule.SetItemProcessStageAllowed(ItemProcessStage.Unlabeled, true);
		rule.SetItemProcessStageAllowed(ItemProcessStage.Labeled, true);

		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(itemProcessStage: ItemProcessStage.Unlabeled)),
			Is.True);
		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(itemProcessStage: ItemProcessStage.Labeled)),
			Is.True);
		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(itemProcessStage: ItemProcessStage.Picked)),
			Is.False);
		Assert.That(
			rule.IsFilterCapable(FacilityFilter.None),
			Is.False,
			"A stage-specific Rule must not accept a query that did not describe the incoming item's stage.");
	}

	[Test]
	public void TransferFilter_ExplicitStageEnforcesRuleAndUnstagedTransferDoesNotMatch()
	{
		FacilityRule pickedRule = new();
		pickedRule.SetItemProcessStageAllowed(ItemProcessStage.Picked, true);
		FacilityRule packedRule = new();
		packedRule.SetItemProcessStageAllowed(ItemProcessStage.Packed, true);
		FacilityRule emptyPickedRule = new();
		emptyPickedRule.SetItemProcessStageAllowed(ItemProcessStage.Picked, true);
		emptyPickedRule.SetRequiredContentState(FacilityContentState.Empty);
		FacilityRule insidePickedRule = new();
		insidePickedRule.SetItemProcessStageAllowed(ItemProcessStage.Picked, true);
		insidePickedRule.SetRequiredContentState(FacilityContentState.HasItems);

		FacilityFilter legacyTransfer = FacilityFilter.ForTransfer(
			source: null,
			itemId: 1,
			quantity: 1);
		FacilityFilter pickedTransfer = FacilityFilter.WithContentState(
			FacilityFilter.WithItemProcessStage(
				legacyTransfer,
				ItemProcessStage.Picked),
			FacilityContentState.HasItems);
		FacilityFilter packedTransfer = FacilityFilter.WithContentState(
			FacilityFilter.WithItemProcessStage(
				legacyTransfer,
				ItemProcessStage.Packed),
			FacilityContentState.HasItems);

		Assert.That(legacyTransfer.ItemProcessStage, Is.EqualTo(ItemProcessStage.Any));
		Assert.That(pickedRule.IsFilterCapable(legacyTransfer), Is.False);
		Assert.That(pickedRule.IsFilterCapable(pickedTransfer), Is.True);
		Assert.That(packedRule.IsFilterCapable(pickedTransfer), Is.False);
		Assert.That(pickedRule.IsFilterCapable(packedTransfer), Is.False);
		Assert.That(packedRule.IsFilterCapable(packedTransfer), Is.True);
		Assert.That(pickedTransfer.ContentState, Is.EqualTo(FacilityContentState.HasItems));
		Assert.That(emptyPickedRule.IsFilterCapable(pickedTransfer), Is.False);
		Assert.That(insidePickedRule.IsFilterCapable(pickedTransfer), Is.True);
	}

	[Test]
	public void FacilityRule_ContentState_IsExactAndRejectsUnknownContent()
	{
		FacilityRule rule = new();
		rule.SetRequiredContentState(FacilityContentState.Empty);

		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(
				contentState: FacilityContentState.HasItems)),
			Is.False);
		Assert.That(
			rule.IsFilterCapable(new FacilityFilter(
				contentState: FacilityContentState.Empty)),
			Is.True);
		Assert.That(
			rule.IsFilterCapable(FacilityFilter.None),
			Is.False,
			"A content-specific Rule must not accept a query that did not describe the Facility contents.");
	}

	[Test]
	public void FacilityFilter_ForContainer_DerivesGenericContentAndItemProcessStage()
	{
		TestItemContainer container = new();

		Assert.That(FacilityFilter.TryForContainer(
			container,
			manifest: null,
			launchReady: false,
			out FacilityFilter emptyFilter), Is.True);
		Assert.That(emptyFilter.ContentState, Is.EqualTo(FacilityContentState.Empty));
		Assert.That(emptyFilter.ItemProcessStage, Is.EqualTo(ItemProcessStage.Any));

		container.StackList.Add(CreateStack(900, 2, ItemStatus.Labeled));
		Assert.That(FacilityFilter.TryForContainer(
			container,
			manifest: null,
			launchReady: false,
			out FacilityFilter labeledFilter), Is.True);
		Assert.That(labeledFilter.ContentState, Is.EqualTo(FacilityContentState.HasItems));
		Assert.That(labeledFilter.ItemProcessStage, Is.EqualTo(ItemProcessStage.Labeled));

		container.StackList.Add(CreateStack(901, 1, ItemStatus.Packed));
		Assert.That(FacilityFilter.TryForContainer(
			container,
			manifest: null,
			launchReady: false,
			out FacilityFilter mixedFilter), Is.True);
		Assert.That(mixedFilter.ContentState, Is.EqualTo(FacilityContentState.HasItems));
		Assert.That(mixedFilter.ItemProcessStage, Is.EqualTo(ItemProcessStage.Any));

		FacilityRule contentOnlyRule = new();
		contentOnlyRule.SetRequiredContentState(FacilityContentState.HasItems);
		FacilityRule stageRule = new();
		stageRule.SetItemProcessStageAllowed(ItemProcessStage.Packed, true);
		Assert.That(contentOnlyRule.IsFilterCapable(mixedFilter), Is.True);
		Assert.That(stageRule.IsFilterCapable(mixedFilter), Is.False);
	}

	[Test]
	public void Building_OutboundTargetStage_IsExplicitAndDefaultsToAny()
	{
		Assert.That(
			new Building("Staging", new List<GridCell>(), ItemProcessStage.Labeled).OutboundTargetStage,
			Is.EqualTo(ItemProcessStage.Labeled));
		Assert.That(
			new Building("Storage", new List<GridCell>(), ItemProcessStage.Picked).OutboundTargetStage,
			Is.EqualTo(ItemProcessStage.Picked));
		Assert.That(
			new Building("Packing", new List<GridCell>(), ItemProcessStage.Packed).OutboundTargetStage,
			Is.EqualTo(ItemProcessStage.Packed));
		Assert.That(
			new Building("Launch", new List<GridCell>(), ItemProcessStage.LaunchReady).OutboundTargetStage,
			Is.EqualTo(ItemProcessStage.LaunchReady));
		Assert.That(
			new Building("Default", new List<GridCell>()).OutboundTargetStage,
			Is.EqualTo(ItemProcessStage.Any));
	}

	[Test]
	public void GameSaveData_UsesCurrentBreakingSchemaVersion()
	{
		Assert.That(GameSaveData.CurrentVersion, Is.EqualTo(21));
		Assert.That(new GameSaveData().Version, Is.EqualTo(GameSaveData.CurrentVersion));
	}

	[Test]
	public void FacilityRuleSaveData_StoresAllowedItemProcessStageMask()
	{
		FacilityRuleSaveData data = new()
		{
			RequiredCapsuleBufferState = FacilityContentState.HasItems,
			AllowedItemProcessStages = ItemProcessStageMask.Unlabeled | ItemProcessStageMask.Labeled,
		};

		string json = JsonUtility.ToJson(data);

		Assert.That(json, Does.Contain("\"RequiredCapsuleBufferState\":1"));
		Assert.That(json, Does.Contain("\"AllowedItemProcessStages\":3"));
		Assert.That(json, Does.Not.Contain("RequiredContentState"));
		Assert.That(json, Does.Not.Contain("RequiredCargoProcessStage"));
	}

	[Test]
	public void BuildManagementRuleEditor_GroupsGenericProcessFieldsUnderItemConditions()
	{
		const string assetPath = "Assets/UI/Toolkit/BuildManagementContent.uxml";
		VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
		Assert.That(template, Is.Not.Null, $"Missing {assetPath}");

		VisualElement root = template.CloneTree();
		Foldout itemConditions = root.Q<Foldout>("rule-editor-item-conditions");
		Foldout workerConditions = root.Q<Foldout>("rule-editor-worker-conditions");
		Foldout manifestConditions = root.Q<Foldout>("rule-editor-manifest-conditions");
		VisualElement processStages = root.Q<VisualElement>("rule-editor-item-process-stages");
		DropdownField contentState = root.Q<DropdownField>("rule-editor-content-state");

		Assert.That(itemConditions, Is.Not.Null);
		Assert.That(workerConditions, Is.Not.Null);
		Assert.That(manifestConditions, Is.Not.Null);
		Assert.That(itemConditions.value, Is.True);
		Assert.That(workerConditions.value, Is.True);
		Assert.That(manifestConditions.value, Is.True);
		Assert.That(processStages, Is.Not.Null);
		Assert.That(processStages.Q<Toggle>("rule-process-stage-unlabeled"), Is.Not.Null);
		Assert.That(processStages.Q<Toggle>("rule-process-stage-labeled"), Is.Not.Null);
		Assert.That(contentState, Is.Not.Null);
		Assert.That(contentState.label, Is.EqualTo("Content"));
		Assert.That(itemConditions.Contains(processStages), Is.True);
		Assert.That(itemConditions.Contains(contentState), Is.True);
		Assert.That(root.Q("rule-editor-cargo-process-stage"), Is.Null);
		Assert.That(root.Q("rule-editor-capsule-buffer-state"), Is.Null);
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

	private sealed class TestItemContainer : IItemContainer
	{
		public List<ItemStack> StackList { get; } = new();
		public IReadOnlyList<ItemStack> Stacks => StackList;
		public IReadOnlyDictionary<uint, int> ItemTotals { get; } = new Dictionary<uint, int>();
		public float TotalSize => 0.0f;
		public float MaxSize => 100.0f;
		public ItemTag ItemTags => ItemTag.None;

		public bool CanRegister() => true;
		public int GetQuantity(uint itemId) => 0;
		public int GetAcceptableQuantity(uint itemId, int requested) => requested;
		public bool CanAcceptStack(ItemStack stack) => stack != null;
		public int AddItem(uint itemId, int quantity) => 0;
		public int RemoveItem(uint itemId, int quantity) => 0;
		public bool AddStack(ItemStack stack) => false;
		public bool RemoveStack(ItemStack stack) => false;
		public bool TryRemoveFromStack(ItemStack stack, int quantity, out ItemStack removedStack)
		{
			removedStack = null;
			return false;
		}
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
	private CargoPortService cargoPortService;
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
		GameObject cargoPortServiceObject = CreateGameObject("Cargo Rule Query Port Service", active: false);
		cargoPortService = cargoPortServiceObject.AddComponent<CargoPortService>();

		GameObject contextObject = CreateGameObject("Cargo Rule Query Context", active: false);
		GameContext context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "gridService", gridService);
		SetPrivateField(typeof(GameContext), context, "facilityManager", facilityManager);
		SetPrivateField(typeof(GameContext), context, "facilityRuleManager", ruleManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "capsuleDockService", dockService);
		SetPrivateField(typeof(GameContext), context, "capsuleBufferService", bufferService);
		SetPrivateField(typeof(GameContext), context, "cargoPortService", cargoPortService);
		SetPrivateField(typeof(GameContext), context, "outboundWorkflowService", outboundWorkflow);
		SetPrivateStaticField(typeof(GameContext), "instance", context);

		ruleManagerObject.SetActive(true);
		dockServiceObject.SetActive(true);
		serviceObject.SetActive(true);
		cargoPortServiceObject.SetActive(true);
		InvokeNonPublic(
			typeof(FacilityService<CapsuleDock>),
			dockService,
			"TryBindFacilityManager");
		InvokeNonPublic(
			typeof(FacilityService<CapsuleBuffer>),
			bufferService,
			"TryBindFacilityManager");
		InvokeNonPublic(
			typeof(FacilityService<CargoPort>),
			cargoPortService,
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
	public void DefaultProcessStagePresets_CreateOneRuleForEachConcreteStage()
	{
		ruleManager.EnsureDefaultProcessStagePresets();

		Assert.That(ruleManager.Presets, Has.Count.EqualTo(5));
		ItemProcessStage[] expectedStages =
		{
			ItemProcessStage.Unlabeled,
			ItemProcessStage.Labeled,
			ItemProcessStage.Picked,
			ItemProcessStage.Packed,
			ItemProcessStage.LaunchReady,
		};

		for (int i = 0; i < expectedStages.Length; ++i)
		{
			FacilityRulePreset preset = ruleManager.Presets[i];
			Assert.That(preset.DisplayName, Is.EqualTo(ItemProcessStageUtility.ToDisplayString(expectedStages[i])));
			Assert.That(preset.Rule.AllowedItemProcessStages, Is.EqualTo(ItemProcessStageUtility.ToMask(expectedStages[i])));
		}

		ruleManager.EnsureDefaultProcessStagePresets();
		Assert.That(ruleManager.Presets, Has.Count.EqualTo(5));
	}

	[Test]
	public void DefaultProcessStagePresets_DoNotModifyExistingRules()
	{
		FacilityRulePreset existing = ruleManager.CreatePreset("Custom Rule", new FacilityRule());

		ruleManager.EnsureDefaultProcessStagePresets();

		Assert.That(ruleManager.Presets, Is.EqualTo(new[] { existing }));
	}

	[Test]
	public void Query_UsesExactStageExplicitRuleAndBuildingScope()
	{
		CargoCapsule labeledCapsule = CreateCapsule("Labeled Capsule", 501, 3, ItemStatus.Labeled);
		CapsuleBuffer noRule = CreateBuffer("No Rule", FirstBuildingId, 1);
		CapsuleBuffer wrongStage = CreateBuffer("Wrong Stage", FirstBuildingId, 2);
		CapsuleBuffer matching = CreateBuffer("Matching", FirstBuildingId, 3);
		CapsuleBuffer otherBuilding = CreateBuffer("Other Building", SecondBuildingId, 4);

		ApplyRule(wrongStage, ItemProcessStage.Packed);
		ApplyRule(matching, ItemProcessStage.Labeled);
		ApplyRule(otherBuilding, ItemProcessStage.Labeled);

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
		ApplyRule(lowPriority, ItemProcessStage.Labeled, priority: 1);
		ApplyRule(highPriority, ItemProcessStage.Labeled, priority: 10);
		ApplyRule(equalPriority, ItemProcessStage.Labeled, priority: 10);

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
	public void RuleSave_RoundTripsContentStateAndAllowedItemProcessStages()
	{
		FacilityRule rule = new();
		rule.SetRequiredContentState(FacilityContentState.HasItems);
		rule.SetAllowedItemProcessStages(ItemProcessStageMask.Unlabeled | ItemProcessStageMask.Labeled);
		FacilityRulePreset preset = ruleManager.CreatePreset("Round Trip Rule", rule);

		FacilityRuleManagerSaveData save = ruleManager.CaptureState();
		ruleManager.RestoreState(save);

		Assert.That(ruleManager.TryGetPreset(preset.Id, out FacilityRulePreset restored), Is.True);
		Assert.That(
			restored.Rule.RequiredContentState,
			Is.EqualTo(FacilityContentState.HasItems));
		Assert.That(
			restored.Rule.AllowedItemProcessStages,
			Is.EqualTo(ItemProcessStageMask.Unlabeled | ItemProcessStageMask.Labeled));
	}

	[Test]
	public void Query_EmptyRuleRejectsAllCargo()
	{
		CargoCapsule unlabeledCapsule = CreateCapsule("Unlabeled Capsule", 601, 2, ItemStatus.None);
		CapsuleBuffer catchAll = CreateBuffer("Catch All", FirstBuildingId, 5);
		ApplyRule(catchAll, ItemProcessStage.Any);

		List<CapsuleBuffer> results = new();
		Assert.That(
			bufferService.TryQueryRuleMatchedDestinations(
				FirstBuildingId,
				unlabeledCapsule,
				results,
				evaluateLaunchReadiness: false),
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
			ItemProcessStage.Picked,
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
		Building storage = CreateStorageBuilding("Outbound-owned Storage");
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
		Building storage = CreateStorageBuilding("Manifest Storage");
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
		ApplyRule(inputBuffer, ItemProcessStage.Labeled);

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
		Building storage = CreateStorageBuilding("Rule-mismatch Storage");
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
		ApplyRule(buffer, ItemProcessStage.Packed);

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
		ApplyRule(marsOnly, ItemProcessStage.Picked, new[] { OrderDestination.Mars });
		ApplyRule(
			bothDestinations,
			ItemProcessStage.Picked,
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
		ApplyRule(packed, ItemProcessStage.Packed);
		ApplyRule(launchReady, ItemProcessStage.LaunchReady);

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
		ApplyRule(occupied, ItemProcessStage.Labeled);
		ApplyRule(wrongRoute, ItemProcessStage.Labeled);
		ApplyRule(invalidating, ItemProcessStage.Labeled);
		ApplyRule(available, ItemProcessStage.Labeled);

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
			ItemProcessStage.Any,
			contentState: FacilityContentState.Empty);
		ApplyRule(
			contradictoryEmpty,
			ItemProcessStage.Labeled,
			contentState: FacilityContentState.Empty);

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
	public void PickingOutput_UsesProjectedPickedRuleDirectly()
	{
		const uint itemId = 803;
		const int quantity = 2;
		CargoCapsule source = CreateCapsule(
			"Picked Transfer Source",
			itemId,
			quantity,
			ItemStatus.Labeled);
		Order order = new()
		{
			Destination = OrderDestination.Mars,
			Lines = new List<OrderLine>(),
		};
		OrderLine orderLine = new(order, itemId, quantity, null);
		order.Lines.Add(orderLine);
		PickingManifest sourceManifest = outboundWorkflow.GetPickingManifest(source);
		Assert.That(sourceManifest.AddPicked(orderLine, itemId, quantity), Is.EqualTo(quantity));

		CapsuleBuffer emptyInput = CreateBuffer("Empty Picking Input", FirstBuildingId, 16);
		CargoCapsule emptyInputCapsule = CreateCapsule("Empty Picking Capsule");
		emptyInputCapsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		Assert.That(emptyInput.TryDockCapsule(emptyInputCapsule), Is.True);
		ApplyRule(
			emptyInput,
			ItemProcessStage.Any,
			contentState: FacilityContentState.Empty);

		CapsuleBuffer pickedOutput = CreateBuffer("Picked Output", FirstBuildingId, 17);
		CargoCapsule pickedOutputCapsule = CreateCapsule("Empty Capsule At Picked Output");
		pickedOutputCapsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		Assert.That(pickedOutput.TryDockCapsule(pickedOutputCapsule), Is.True);
		ApplyRule(
			pickedOutput,
			ItemProcessStage.Picked,
			contentState: FacilityContentState.HasItems);

		FacilityFilter projectedInput = FacilityFilter.WithContentState(
			FacilityFilter.WithItemProcessStage(
				FacilityFilter.ForManifestTransfer(
				source,
				sourceManifest,
				itemId,
				quantity,
				stack => stack.HasStatus(ItemStatus.Labeled)),
				ItemProcessStage.Picked),
			FacilityContentState.HasItems);
		ItemTransferTask pickingTask = new(
			WorkerTask.TaskType.Picking,
			new ItemTransferJob(
				planner: null,
				TransferObjectType.Item,
				TransferObjectType.Item,
				FirstBuildingId));
		InvokeNonPublic(
			typeof(ItemTransferTask),
			pickingTask,
			"RetainCapsuleOutput",
			new WorkLine(
				WorkLineAction.Put,
				pickedOutput,
				pickedOutput,
				itemId,
				quantity,
				orderLine));

		Assert.That(
			InvokePrivateStatic<bool>(
				typeof(PickingPlanner),
				"IsProjectedInputRuleMatchedBuffer",
				emptyInput,
				projectedInput),
			Is.False,
			"An Empty-only Rule does not describe the incoming Picked items.");
		Assert.That(
			InvokePrivateStatic<bool>(
				typeof(PickingPlanner),
				"IsProjectedInputRuleMatchedBuffer",
				pickedOutput,
				projectedInput),
			Is.True,
			"Picking should place directly into a physically empty container whose Rule accepts the projected Picked items.");

		AddStack(pickedOutputCapsule, itemId, quantity, ItemStatus.Labeled);
		Assert.That(
			outboundWorkflow.TransferPickingManifest(
				source,
				pickedOutputCapsule,
				orderLine,
				itemId,
				quantity),
			Is.EqualTo(quantity));
		Assert.That(
			FacilityFilter.TryForCapsule(
				pickedOutputCapsule,
				evaluateLaunchReadiness: false,
				out FacilityFilter pickedFilter),
			Is.True);
		Assert.That(pickedFilter.ItemProcessStage, Is.EqualTo(ItemProcessStage.Picked));
		Assert.That(
			bufferService.IsRuleMatchedBuffer(
				pickedOutput,
				pickedOutputCapsule,
				evaluateLaunchReadiness: false),
			Is.True,
			"The completed Picking output should already match its selected Rule.");
		pickedOutputCapsule.SetLogisticsState(CapsuleLogisticsState.Inside);
		Assert.That(pickingTask.DependsOnFacility(pickedOutput), Is.True);
		Assert.That(
			InvokePrivateStatic<bool>(
				typeof(PickingPlanner),
				"IsRetainedPickingOutputBuffer",
				pickingTask,
				pickedOutput,
				FirstBuildingId,
				projectedInput),
			Is.True,
			"The same Picking task must keep using its directly selected Picked output.");
	}

	[Test]
	public void PackingInput_RequiresCurrentPickedInsideRule()
	{
		const uint itemId = 804;
		const int quantity = 3;
		CapsuleBuffer source = CreateBuffer("Packing Picked Input", FirstBuildingId, 18);
		CargoCapsule capsule = CreateCapsule(
			"Packing Picked Capsule",
			itemId,
			quantity,
			ItemStatus.Labeled);
		capsule.SetLogisticsState(CapsuleLogisticsState.Inside);
		OrderLine line = new(null, itemId, quantity, null);
		Assert.That(
			outboundWorkflow.GetPickingManifest(capsule).AddPicked(line, itemId, quantity),
			Is.EqualTo(quantity));
		Assert.That(source.TryDockCapsule(capsule), Is.True);
		ApplyRule(
			source,
			ItemProcessStage.Picked,
			contentState: FacilityContentState.HasItems);

		PackingInputPlanner planner = new(FirstBuildingId);
		Assert.That(planner.HasAvailableWork(), Is.True);

		ApplyRule(
			source,
			ItemProcessStage.Picked,
			contentState: FacilityContentState.Any);
		Assert.That(
			planner.HasAvailableWork(),
			Is.False,
			"Packing must be activated by an explicit Picked/Inside Rule, not a building type or an Any-state Rule.");
	}

	[Test]
	public void PackingAndLaunchOutput_UseProjectedPackedRuleDirectly()
	{
		const uint itemId = 805;
		const int quantity = 2;
		Building building = new(
			"Generic Launch Rule Building",
			new List<GridCell>(),
			ItemProcessStage.Any);
		buildingManager.Register(building);
		Assert.That(building.TrySetOutboundTargetStage(ItemProcessStage.LaunchReady), Is.True);

		CargoCapsule source = CreateCapsule(
			"Packed Output Source",
			itemId,
			quantity,
			ItemStatus.Packed);
		Order order = new()
		{
			Destination = OrderDestination.Mars,
			Lines = new List<OrderLine>(),
		};
		OrderLine orderLine = new(order, itemId, quantity, null);
		order.Lines.Add(orderLine);
		PickingManifest manifest = outboundWorkflow.GetPickingManifest(source);
		Assert.That(manifest.AddPacked(orderLine, itemId, quantity), Is.EqualTo(quantity));

		CapsuleBuffer emptyInput = CreateBuffer(
			"Mars Empty Output Input",
			building.RuntimeBuildingId,
			20);
		CargoCapsule emptyCapsule = CreateCapsule("Mars Empty Output Capsule");
		emptyCapsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		Assert.That(emptyInput.TryDockCapsule(emptyCapsule), Is.True);
		ApplyRule(
			emptyInput,
			ItemProcessStage.Any,
			new[] { OrderDestination.Mars },
			FacilityContentState.Empty);

		CapsuleBuffer packedOutput = CreateBuffer(
			"Packed Relocation Output",
			building.RuntimeBuildingId,
			21);
		CargoCapsule packedOutputCapsule = CreateCapsule("Empty Capsule At Packed Output");
		packedOutputCapsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		Assert.That(packedOutput.TryDockCapsule(packedOutputCapsule), Is.True);
		ApplyRule(
			packedOutput,
			ItemProcessStage.Packed,
			new[] { OrderDestination.Mars },
			FacilityContentState.HasItems);

		FacilityFilter projectedInput = FacilityFilter.WithContentState(
			FacilityFilter.WithItemProcessStage(
				FacilityFilter.ForManifestTransfer(
				source,
				manifest,
				itemId,
				quantity,
				stack => stack.HasStatus(ItemStatus.Packed)),
				ItemProcessStage.Packed),
			FacilityContentState.HasItems);
		PackingOutputPlanner packingPlanner = new(building.RuntimeBuildingId);
		LaunchSortPlanner launchPlanner = new(building.RuntimeBuildingId);

		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(PackingOutputPlanner),
				packingPlanner,
				"IsProjectedInputRuleMatchedBuffer",
				emptyInput,
				projectedInput),
			Is.False);
		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(LaunchSortPlanner),
				launchPlanner,
				"IsProjectedInputRuleMatchedBuffer",
				emptyInput,
				projectedInput),
			Is.False);
		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(PackingOutputPlanner),
				packingPlanner,
				"IsProjectedInputRuleMatchedBuffer",
				packedOutput,
				projectedInput),
			Is.True,
			"Packing should place directly into a physically empty container whose Rule accepts the projected Packed items.");
		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(LaunchSortPlanner),
				launchPlanner,
				"IsProjectedInputRuleMatchedBuffer",
				packedOutput,
				projectedInput),
			Is.True);

		AddStack(packedOutputCapsule, itemId, quantity, ItemStatus.Packed);
		Assert.That(
			outboundWorkflow.TransferPickingManifest(
				source,
				packedOutputCapsule,
				orderLine,
				itemId,
				quantity,
				packed: true),
			Is.EqualTo(quantity));
		Assert.That(
			bufferService.IsRuleMatchedBuffer(
				packedOutput,
				packedOutputCapsule,
				evaluateLaunchReadiness: false),
			Is.True,
			"Without Launch context the completed output remains Packed and matches its Rule.");
		Assert.That(
			bufferService.IsRuleMatchedBuffer(
				packedOutput,
				packedOutputCapsule,
				evaluateLaunchReadiness: true),
			Is.False,
			"Launch context derives Launch Ready after the Packed transfer settles.");
	}

	[Test]
	public void PackingItemTransfer_RestoresPlacePhaseFromSavedWorkerPayload()
	{
		ToteBox packingInputPayload = CreateBox<ToteBox>(
			"Restored Packing Input Payload",
			BoxType.Personal);
		AddStack(packingInputPayload, 806, 3, ItemStatus.Labeled);
		AddStack(packingInputPayload, 808, 2, ItemStatus.Labeled);
		ItemTransferTask packingInputTask = new(
			WorkerTask.TaskType.PackingInput,
			new ItemTransferJob(
				new PackingInputPlanner(FirstBuildingId),
				TransferObjectType.Item,
				TransferObjectType.Box,
				FirstBuildingId));

		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(ItemTransferTask),
				packingInputTask,
				"RestoreCollectedPackingPayload",
				packingInputPayload),
			Is.True);
		Assert.That(packingInputTask.Phase, Is.EqualTo(ItemTransferPhase.Place));
		Assert.That(packingInputTask.CollectedLines.Count, Is.EqualTo(2));
		Assert.That(packingInputTask.CollectedLines[0].CollectLine.ItemID, Is.EqualTo(806u));
		Assert.That(packingInputTask.CollectedLines[1].CollectLine.ItemID, Is.EqualTo(808u));

		ItemTransferTask collectingPackingInputTask = new(
			WorkerTask.TaskType.PackingInput,
			new ItemTransferJob(
				new PackingInputPlanner(FirstBuildingId),
				TransferObjectType.Item,
				TransferObjectType.Box,
				FirstBuildingId));
		Assert.That(
			InvokePrivateStatic<bool>(
				typeof(GameSaveService),
				"RestoreOutboundPayloadForSavedPhase",
				collectingPackingInputTask,
				packingInputPayload,
				ItemTransferPhase.Collect),
			Is.True);
		Assert.That(collectingPackingInputTask.Phase, Is.EqualTo(ItemTransferPhase.Collect));
		Assert.That(collectingPackingInputTask.CollectedLines.Count, Is.EqualTo(2));

		ToteBox packingOutputPayload = CreateBox<ToteBox>(
			"Restored Packing Output Payload",
			BoxType.Personal);
		AddStack(packingOutputPayload, 807, 2, ItemStatus.Packed);
		ItemTransferTask packingOutputTask = new(
			WorkerTask.TaskType.PackingOutput,
			new ItemTransferJob(
				new PackingOutputPlanner(FirstBuildingId),
				TransferObjectType.Box,
				TransferObjectType.Item,
				FirstBuildingId));

		Assert.That(
			InvokePrivateInstance<bool>(
				typeof(ItemTransferTask),
				packingOutputTask,
				"RestoreCollectedPackingPayload",
				packingOutputPayload),
			Is.True);
		Assert.That(packingOutputTask.Phase, Is.EqualTo(ItemTransferPhase.Place));
		Assert.That(packingOutputTask.CollectedLines.Count, Is.EqualTo(1));
		Assert.That(packingOutputTask.CollectedLines[0].CollectLine.ItemID, Is.EqualTo(807u));
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
		ApplyRule(
			target,
			ItemProcessStage.Labeled,
			contentState: FacilityContentState.HasItems);
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
			null,
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
		Assert.That(target.DockState, Is.EqualTo(CapsuleDockState.Buffer));
	}

	[Test]
	public void OutboundPortQuery_RequiresNonEmptyMatchingRuleInSameBuilding()
	{
		CargoCapsule capsule = CreateCapsule("Outbound Rule Capsule", 940, 2, ItemStatus.None);
		OutboundCargoPort noRule = CreateOutboundPort("No Rule Port", FirstBuildingId, 30);
		OutboundCargoPort emptyRule = CreateOutboundPort("Empty Rule Port", FirstBuildingId, 31);
		OutboundCargoPort wrongStage = CreateOutboundPort("Wrong Stage Port", FirstBuildingId, 32);
		OutboundCargoPort matching = CreateOutboundPort("Matching Port", FirstBuildingId, 33);
		OutboundCargoPort otherBuilding = CreateOutboundPort("Other Building Port", SecondBuildingId, 34);

		ApplyOutboundRule(emptyRule, ItemProcessStageMask.None);
		ApplyOutboundRule(wrongStage, ItemProcessStageMask.Packed);
		ApplyOutboundRule(
			matching,
			ItemProcessStageMask.Unlabeled | ItemProcessStageMask.Labeled,
			priority: 5);
		ApplyOutboundRule(otherBuilding, ItemProcessStageMask.Unlabeled, priority: 10);

		Assert.That(cargoPortService.IsRuleMatchedOutboundPort(noRule, capsule, false), Is.False);
		Assert.That(cargoPortService.IsRuleMatchedOutboundPort(emptyRule, capsule, false), Is.False);
		Assert.That(cargoPortService.IsRuleMatchedOutboundPort(wrongStage, capsule, false), Is.False);
		Assert.That(cargoPortService.IsRuleMatchedOutboundPort(matching, capsule, false), Is.True);
		Assert.That(
			cargoPortService.TryFindRuleMatchedOutboundPort(
				FirstBuildingId,
				capsule,
				evaluateLaunchReadiness: false,
				out OutboundCargoPort result),
			Is.True);
		Assert.That(result, Is.SameAs(matching));
	}

	[Test]
	public void OutboundPortQuery_AvailabilityIsExplicitAndPriorityIsStable()
	{
		CargoCapsule capsule = CreateCapsule("Available Outbound Capsule", 941, 2, ItemStatus.Labeled);
		OutboundCargoPort occupiedHighPriority = CreateOutboundPort("Occupied High Priority Port", FirstBuildingId, 35);
		OutboundCargoPort availableLowPriority = CreateOutboundPort("Available Low Priority Port", FirstBuildingId, 36);
		ApplyOutboundRule(occupiedHighPriority, ItemProcessStageMask.Labeled, priority: 10);
		ApplyOutboundRule(availableLowPriority, ItemProcessStageMask.Labeled, priority: 1);
		Assert.That(
			occupiedHighPriority.TryDockCapsule(CreateCapsule("Occupying Capsule")),
			Is.True);

		Assert.That(
			cargoPortService.TryFindRuleMatchedOutboundPort(
				FirstBuildingId,
				capsule,
				evaluateLaunchReadiness: false,
				out OutboundCargoPort availableResult),
			Is.True);
		Assert.That(availableResult, Is.SameAs(availableLowPriority));

		Assert.That(
			cargoPortService.TryFindRuleMatchedOutboundPort(
				FirstBuildingId,
				capsule,
				evaluateLaunchReadiness: false,
				out OutboundCargoPort policyResult,
				requireAvailable: false),
			Is.True);
		Assert.That(policyResult, Is.SameAs(occupiedHighPriority));

		WasteBin waste = CreateComponent<WasteBin>("Waste Outbound Capsule", active: false);
		Assert.That(cargoPortService.IsRuleMatchedOutboundPort(availableLowPriority, waste, false), Is.False);
	}

	private CapsuleBuffer CreateBuffer(string name, uint buildingId, int x)
	{
		CapsuleBuffer buffer = CreateComponent<CapsuleBuffer>(name, active: false);
		buffer.OnPositionSet(new int3(x, 0, 1), FacingDirection.North);
		facilityManager.RegisterFacility(buildingId, buffer);
		return buffer;
	}

	private OutboundCargoPort CreateOutboundPort(string name, uint buildingId, int x)
	{
		OutboundCargoPort port = CreateComponent<OutboundCargoPort>(name, active: false);
		port.OnPositionSet(new int3(x, 0, 1), FacingDirection.North);
		facilityManager.RegisterFacility(buildingId, port);
		return port;
	}

	private Building CreateStorageBuilding(string name)
	{
		Building building = new(name, new List<GridCell>(), ItemProcessStage.Picked);
		buildingManager.Register(building);
		Assert.That(building.RuntimeBuildingId, Is.Not.Zero);
		return building;
	}

	private void ApplyRule(
		CapsuleBuffer buffer,
		ItemProcessStage stage,
		IEnumerable<OrderDestination> destinations = null,
		FacilityContentState contentState = FacilityContentState.Any,
		int priority = 0)
	{
		FacilityRule rule = new();
		rule.SetPriority(priority);
		rule.SetItemProcessStageAllowed(stage, true);
		rule.SetRequiredContentState(contentState);
		if (destinations != null)
		{
			FacilityManifestRule manifestRule = new();
			manifestRule.SetRequiredDestinations(destinations);
			rule.SetManifestRule(manifestRule);
		}

		FacilityRulePreset preset = ruleManager.CreatePreset($"{buffer.name} Rule", rule);
		Assert.That(ruleManager.ApplyPreset(buffer, preset.Id), Is.True);
	}

	private void ApplyOutboundRule(
		OutboundCargoPort port,
		ItemProcessStageMask stages,
		int priority = 0)
	{
		FacilityRule rule = new();
		rule.SetPriority(priority);
		rule.SetAllowedItemProcessStages(stages);
		FacilityRulePreset preset = ruleManager.CreatePreset($"{port.name} Rule", rule);
		Assert.That(ruleManager.ApplyPreset(port, preset.Id), Is.True);
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

	private T CreateBox<T>(string name, BoxType boxType) where T : BoxBase
	{
		T box = CreateComponent<T>(name, active: false);
		SetPrivateField(typeof(BoxBase), box, "boxType", boxType);
		box.SetBoxId(nextBoxId++);
		return box;
	}

	private static void AddStack(
		BoxBase box,
		uint itemId,
		int quantity,
		ItemStatus status)
	{
		ItemStack stack = new(itemId, status: status);
		Assert.That(stack.AddItem(quantity), Is.EqualTo(quantity));

		List<ItemStack> stacks =
			(List<ItemStack>)GetPrivateField(typeof(BoxBase), box, "stacks");
		Dictionary<uint, int> totals =
			(Dictionary<uint, int>)GetPrivateField(typeof(BoxBase), box, "itemTotals");
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

	private static void InvokeNonPublic(
		Type ownerType,
		object target,
		string methodName,
		params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		method.Invoke(target, arguments);
	}

	private static T InvokePrivateStatic<T>(Type ownerType, string methodName, params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		return (T)method.Invoke(null, arguments);
	}

	private static T InvokePrivateInstance<T>(
		Type ownerType,
		object target,
		string methodName,
		params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		return (T)method.Invoke(target, arguments);
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
