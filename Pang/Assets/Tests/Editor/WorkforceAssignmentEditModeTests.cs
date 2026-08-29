using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniverseLogistics.UI.Toolkit;

namespace Pang.Tests.Editor
{
public sealed class WorkforceAssignmentEditModeTests
{
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private GameContext context;
	private WorkerManager workerManager;
	private BuildingManager buildingManager;
	private TaskManager taskManager;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		workerManager = CreateComponent<WorkerManager>("Workforce Test Worker Manager", active: false);
		buildingManager = CreateComponent<BuildingManager>("Workforce Test Building Manager", active: false);
		taskManager = CreateComponent<TaskManager>("Workforce Test Task Manager", active: false);
		RestFacilityService restFacilityService =
			CreateComponent<RestFacilityService>("Workforce Test Rest Facility Service", active: false);
		WorkPolicyService workPolicyService =
			CreateComponent<WorkPolicyService>("Workforce Test Work Policy Service", active: false);
		WMSystem warehouseManagement =
			CreateComponent<WMSystem>("Workforce Test Warehouse Management", active: false);
		SetPrivateField(
			typeof(WMSystem),
			warehouseManagement,
			"workPolicyService",
			workPolicyService);
		InvokeNonPublic(typeof(WorkerManager), workerManager, "Awake");
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");
		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");

		GameObject contextObject = CreateGameObject("Workforce Test Context", active: false);
		context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
		SetPrivateField(typeof(GameContext), context, "warehouseManagement", warehouseManagement);
		SetPrivateStaticField(typeof(GameContext), "instance", context);
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
	public void Catalog_ExposesAgreedRolesInBuildingOrder()
	{
		WorkforceRole[] buildingRoles =
		{
			WorkforceRole.Labeling,
			WorkforceRole.Storing,
			WorkforceRole.Picking,
			WorkforceRole.Packing,
			WorkforceRole.PackingLogistics,
			WorkforceRole.LaunchSorting,
			WorkforceRole.CapsuleHandling,
		};
		CollectionAssert.AreEqual(
			buildingRoles,
			WorkforceRoleCatalog.GetRoles(1));
		CollectionAssert.AreEqual(
			buildingRoles,
			WorkforceRoleCatalog.GetRoles(1));
		CollectionAssert.AreEqual(
			buildingRoles,
			WorkforceRoleCatalog.GetRoles(1));
		CollectionAssert.AreEqual(
			buildingRoles,
			WorkforceRoleCatalog.GetRoles(1));
		CollectionAssert.AreEqual(
			buildingRoles,
			WorkforceRoleCatalog.GetRoles(1));
		CollectionAssert.AreEqual(
			new[]
			{
				WorkforceRole.Unloading,
				WorkforceRole.Loading,
				WorkforceRole.CargoTransfer,
				WorkforceRole.WasteCollection,
			},
			WorkforceRoleCatalog.GetRoles(0));
	}

	[Test]
	public void Catalog_BundlesOnlyTheAgreedTaskTypes()
	{
		AssertDefinition(
			WorkforceRole.CapsuleHandling,
			"Capsule Handling",
			WorkerTask.TaskType.IB,
			WorkerTask.TaskType.CapsuleClear,
			WorkerTask.TaskType.CapsuleSupply,
			WorkerTask.TaskType.OB);
		AssertDefinition(
			WorkforceRole.PackingLogistics,
			"Packing Logistics",
			WorkerTask.TaskType.PackingInput,
			WorkerTask.TaskType.PackingOutput);
		AssertDefinition(
			WorkforceRole.Storing,
			"Storing",
			WorkerTask.TaskType.Storing);
		AssertDefinition(
			WorkforceRole.Picking,
			"Picking",
			WorkerTask.TaskType.Picking);
		AssertDefinition(
			WorkforceRole.Labeling,
			"Labeling",
			WorkerTask.TaskType.Labeling);
		AssertDefinition(
			WorkforceRole.Packing,
			"Packing",
			WorkerTask.TaskType.Packing);
		AssertDefinition(
			WorkforceRole.LaunchSorting,
			"Launch Sorting",
			WorkerTask.TaskType.LaunchSort);
		AssertDefinition(
			WorkforceRole.Unloading,
			"Unloading",
			WorkerTask.TaskType.Unloading);
		AssertDefinition(
			WorkforceRole.Loading,
			"Loading",
			WorkerTask.TaskType.Loading);
		AssertDefinition(
			WorkforceRole.CargoTransfer,
			"Cargo Transfer",
			WorkerTask.TaskType.CargoTransfer);
		AssertDefinition(
			WorkforceRole.WasteCollection,
			"Waste Collection",
			WorkerTask.TaskType.WasteCollection);
	}

	[Test]
	public void Catalog_DefinesEveryRoleWithAssignableTaskTypes()
	{
		foreach (WorkforceRole role in Enum.GetValues(typeof(WorkforceRole)))
		{
			if (role == WorkforceRole.Undefined)
			{
				Assert.That(WorkforceRoleCatalog.TryGetDefinition(role, out _), Is.False);
				continue;
			}

			Assert.That(
				WorkforceRoleCatalog.TryGetDefinition(role, out WorkforceRoleDefinition definition),
				Is.True,
				$"Missing definition for {role}");
			Assert.That(definition.DisplayName, Is.Not.Empty);
			Assert.That(definition.TaskTypes, Is.Not.Empty);
			for (int i = 0; i < definition.TaskTypes.Count; ++i)
			{
				Assert.That(definition.TaskTypes[i], Is.Not.EqualTo(WorkerTask.TaskType.Undefined));
				Assert.That(definition.TaskTypes[i], Is.Not.EqualTo(WorkerTask.TaskType.HandleMistake));
			}
		}
	}

	[Test]
	public void Catalog_ReportsNonePartialAndFullWithoutNormalizingLegacyAssignments()
	{
		List<WorkerTask.TaskType> legacyAssignment = new() { WorkerTask.TaskType.IB };

		Assert.That(
			WorkforceRoleCatalog.GetAssignmentState(
				WorkforceRole.CapsuleHandling,
				Array.Empty<WorkerTask.TaskType>()),
			Is.EqualTo(WorkforceRoleAssignmentState.None));
		Assert.That(
			WorkforceRoleCatalog.GetAssignmentState(
				WorkforceRole.CapsuleHandling,
				legacyAssignment),
			Is.EqualTo(WorkforceRoleAssignmentState.Partial));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.IB },
			legacyAssignment,
			"Reading assignment state must not normalize legacy task types.");
		Assert.That(
			WorkforceRoleCatalog.GetAssignmentState(
				WorkforceRole.CapsuleHandling,
				new[]
				{
					WorkerTask.TaskType.IB,
					WorkerTask.TaskType.CapsuleClear,
					WorkerTask.TaskType.CapsuleSupply,
					WorkerTask.TaskType.OB,
				}),
			Is.EqualTo(WorkforceRoleAssignmentState.Full));
		Assert.That(
			WorkforceRoleCatalog.GetAssignmentState(
				WorkforceRole.PackingLogistics,
				new[] { WorkerTask.TaskType.PackingInput }),
			Is.EqualTo(WorkforceRoleAssignmentState.Partial));
		Assert.That(
			WorkforceRoleCatalog.GetAssignmentState(
				WorkforceRole.PackingLogistics,
				new[] { WorkerTask.TaskType.PackingInput, WorkerTask.TaskType.PackingOutput }),
			Is.EqualTo(WorkforceRoleAssignmentState.Full));
	}

	[Test]
	public void CanRequestWorkerRoleAssignment_ValidatesWithoutMutatingWorker()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Valid Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;

		bool canAssign = workerManager.CanRequestWorkerRoleAssignment(
			worker,
			storage.RuntimeBuildingId,
			WorkforceRole.CapsuleHandling);

		Assert.That(canAssign, Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		Assert.That(worker.AssignedTaskTypes, Is.Empty);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(worker.PendingAssignedTaskTypes, Is.Empty);
		Assert.That(workerChangedCount, Is.Zero);
	}

	[Test]
	public void CanRequestWorkerRoleAssignment_RejectsRegistrationOperationalAndTargetFailures()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker registeredWorker = CreateWorker(
			"Workforce Registered Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(registeredWorker);

		HumanWorker unregisteredWorker = CreateWorker(
			"Workforce Unregistered Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);

		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				unregisteredWorker,
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling),
			Is.False);
		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				registeredWorker,
				uint.MaxValue,
				WorkforceRole.CapsuleHandling),
			Is.False);
		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				registeredWorker,
				storage.RuntimeBuildingId,
				WorkforceRole.Labeling),
			Is.False);
		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				registeredWorker,
				0,
				WorkforceRole.CapsuleHandling),
			Is.False);

		SetPrivateField(
			typeof(AIWorker),
			registeredWorker,
			"operationalState",
			WorkerOperationalState.Death);
		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				registeredWorker,
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling),
			Is.False);
	}

	[Test]
	public void CanRequestWorkerRoleAssignment_UsesExistingAbilityAndComponentRequirements()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker missingComponent = CreateWorker(
			"Workforce Missing Component Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox);
		workerManager.RegisterWorker(missingComponent);
		HumanWorker missingAbility = CreateWorker(
			"Workforce Missing Ability Worker",
			WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(missingAbility);

		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				missingComponent,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.False);
		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				missingAbility,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.False);
	}

	[Test]
	public void CanRequestWorkerRoleAssignment_AcceptsPublicRoleOnlyInPublicScope()
	{
		Building staging = CreateBuilding(ItemProcessStage.Labeled);
		HumanWorker worker = CreateWorker(
			"Workforce Public Worker",
			WorkerAbility.CargoHandling,
			addCargoHandlingAbility: true);
		workerManager.RegisterWorker(worker);

		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				worker,
				0,
				WorkforceRole.Unloading),
			Is.True);
		Assert.That(
			workerManager.CanRequestWorkerRoleAssignment(
				worker,
				staging.RuntimeBuildingId,
				WorkforceRole.Unloading),
			Is.False);
	}

	[Test]
	public void TryRequestWorkerRoleAssignment_AppliesCatalogBundleImmediately()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Role Request Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);

		Assert.That(
			workerManager.TryRequestWorkerRoleAssignment(
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling),
			Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[]
			{
				WorkerTask.TaskType.IB,
				WorkerTask.TaskType.CapsuleClear,
				WorkerTask.TaskType.CapsuleSupply,
				WorkerTask.TaskType.OB,
			},
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.False);
	}

	[Test]
	public void TryRequestWorkerRoleAssignment_SchedulesCatalogBundleWhileWorkerIsBusy()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Busy Role Request Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		WorkerTask currentTask = new CapsuleRelocationTask(
			WorkerTask.TaskType.IB,
			null,
			null,
			storage.RuntimeBuildingId,
			CapsuleRelocationReason.RoleChanged);
		SetPrivateField(typeof(AIWorker), worker, "currentTask", currentTask);

		Assert.That(
			workerManager.TryRequestWorkerRoleAssignment(
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		Assert.That(worker.AssignedTaskTypes, Is.Empty);
		Assert.That(worker.HasPendingAssignment, Is.True);
		Assert.That(worker.PendingPrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.PendingAssignedTaskTypes);

		SetPrivateField(typeof(AIWorker), worker, "currentTask", null);
		Assert.That(workerManager.TryApplyPendingAssignment(worker), Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.False);
	}

	[Test]
	public void TryRequestWorkerUnassignment_AppliesImmediatelyOrAfterCurrentTask()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker idleWorker = CreateWorker(
			"Workforce Idle Unassignment Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(idleWorker);
		SetCurrentAssignment(
			idleWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });

		Assert.That(workerManager.TryRequestWorkerUnassignment(idleWorker), Is.True);
		Assert.That(idleWorker.PrimaryBuildingId, Is.Zero);
		Assert.That(idleWorker.AssignedTaskTypes, Is.Empty);
		Assert.That(idleWorker.HasPendingAssignment, Is.False);

		HumanWorker busyWorker = CreateWorker(
			"Workforce Busy Unassignment Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(busyWorker);
		SetCurrentAssignment(
			busyWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		WorkerTask currentTask = new CapsuleRelocationTask(
			WorkerTask.TaskType.IB,
			null,
			null,
			storage.RuntimeBuildingId,
			CapsuleRelocationReason.RoleChanged);
		SetPrivateField(typeof(AIWorker), busyWorker, "currentTask", currentTask);

		Assert.That(workerManager.TryRequestWorkerUnassignment(busyWorker), Is.True);
		Assert.That(busyWorker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			busyWorker.AssignedTaskTypes);
		Assert.That(busyWorker.HasPendingAssignment, Is.True);
		Assert.That(busyWorker.PendingPrimaryBuildingId, Is.Zero);
		Assert.That(busyWorker.PendingAssignedTaskTypes, Is.Empty);

		SetPrivateField(typeof(AIWorker), busyWorker, "currentTask", null);
		Assert.That(workerManager.TryApplyPendingAssignment(busyWorker), Is.True);
		Assert.That(busyWorker.PrimaryBuildingId, Is.Zero);
		Assert.That(busyWorker.AssignedTaskTypes, Is.Empty);
		Assert.That(busyWorker.HasPendingAssignment, Is.False);
	}

	[Test]
	public void WorkforceSummary_CountsCurrentFullAndPartialOperationalAssignments()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		Building otherStorage = CreateBuilding(ItemProcessStage.Picked);
		WorkerTask.TaskType[] capsuleTasks =
		{
			WorkerTask.TaskType.IB,
			WorkerTask.TaskType.CapsuleClear,
			WorkerTask.TaskType.CapsuleSupply,
			WorkerTask.TaskType.OB,
		};

		HumanWorker fullWorker = CreateWorker(
			"Workforce Full Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(fullWorker);
		List<WorkerTask.TaskType> fullAssignments = new(capsuleTasks)
		{
			WorkerTask.TaskType.Storing,
		};
		SetCurrentAssignment(fullWorker, storage.RuntimeBuildingId, fullAssignments);

		HumanWorker partialWorker = CreateWorker(
			"Workforce Partial Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(partialWorker);
		SetCurrentAssignment(
			partialWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.IB });

		HumanWorker otherBuildingWorker = CreateWorker(
			"Workforce Other Building Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(otherBuildingWorker);
		SetCurrentAssignment(otherBuildingWorker, otherStorage.RuntimeBuildingId, capsuleTasks);

		HumanWorker deadWorker = CreateWorker(
			"Workforce Dead Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(deadWorker);
		SetCurrentAssignment(deadWorker, storage.RuntimeBuildingId, capsuleTasks);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling,
				out WorkforceRoleSummary beforeDeathSummary),
			Is.True);
		Assert.That(beforeDeathSummary.FullCount, Is.EqualTo(2));
		Assert.That(beforeDeathSummary.PartialCount, Is.EqualTo(1));
		Assert.That(beforeDeathSummary.OperationalCount, Is.EqualTo(3));
		Assert.That(deadWorker.EnterIncapacitatedState(WorkerOperationalState.Death), Is.True);

		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling,
				out WorkforceRoleSummary capsuleSummary),
			Is.True);
		Assert.That(capsuleSummary.FullCount, Is.EqualTo(1));
		Assert.That(capsuleSummary.PartialCount, Is.EqualTo(1));
		Assert.That(capsuleSummary.OperationalCount, Is.EqualTo(2));

		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary storingSummary),
			Is.True);
		Assert.That(storingSummary.OperationalCount, Is.EqualTo(1));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Picking,
				out WorkforceRoleSummary pickingSummary),
			Is.True);
		Assert.That(pickingSummary.OperationalCount, Is.Zero);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Labeling,
				out WorkforceRoleSummary labelingSummary),
			Is.True);
		Assert.That(labelingSummary.OperationalCount, Is.Zero);
	}

	[Test]
	public void WorkforceSummary_UsesCurrentAssignmentInsteadOfPendingAssignment()
	{
		Building currentStorage = CreateBuilding(ItemProcessStage.Picked);
		Building pendingStorage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Pending Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			currentStorage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		InvokeNonPublic(
			typeof(AIWorker),
			worker,
			"SetPendingAssignment",
			pendingStorage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Picking });

		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				currentStorage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary currentSummary),
			Is.True);
		Assert.That(currentSummary.OperationalCount, Is.EqualTo(1));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				pendingStorage.RuntimeBuildingId,
				WorkforceRole.Picking,
				out WorkforceRoleSummary pendingSummary),
			Is.True);
		Assert.That(pendingSummary.OperationalCount, Is.Zero);
	}

	[Test]
	public void OperationalUnassignedWorkers_UsesCurrentAssignmentAndIgnoresPendingAssignment()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker pendingWorker = CreateWorker(
			"Workforce Pending Unassigned Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(pendingWorker);
		pendingWorker.SetPrimaryBuildingId(storage.RuntimeBuildingId);
		InvokeNonPublic(
			typeof(AIWorker),
			pendingWorker,
			"SetPendingAssignment",
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });

		HumanWorker assignedWorker = CreateWorker(
			"Workforce Assigned Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(assignedWorker);
		SetCurrentAssignment(
			assignedWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });

		HumanWorker deadUnassignedWorker = CreateWorker(
			"Workforce Dead Unassigned Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(deadUnassignedWorker);
		Assert.That(
			deadUnassignedWorker.EnterIncapacitatedState(WorkerOperationalState.Death),
			Is.True);

		List<AIWorker> results = new() { assignedWorker };
		workerManager.GetOperationalUnassignedWorkers(results);

		CollectionAssert.AreEqual(new[] { pendingWorker }, results);
		Assert.That(pendingWorker.HasPendingAssignment, Is.True);
		Assert.That(pendingWorker.AssignedTaskTypes, Is.Empty);
	}

	[Test]
	public void WorkforceRoleWorkers_MatchesSummaryForExactBuildingAndPublicScopes()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		Building otherStorage = CreateBuilding(ItemProcessStage.Picked);
		WorkerTask.TaskType[] capsuleTasks =
		{
			WorkerTask.TaskType.IB,
			WorkerTask.TaskType.CapsuleClear,
			WorkerTask.TaskType.CapsuleSupply,
			WorkerTask.TaskType.OB,
		};

		HumanWorker fullWorker = CreateWorker(
			"Workforce Roster Full Worker",
			WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(fullWorker);
		SetCurrentAssignment(fullWorker, storage.RuntimeBuildingId, capsuleTasks);
		InvokeNonPublic(
			typeof(AIWorker),
			fullWorker,
			"SetPendingAssignment",
			otherStorage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Picking });

		HumanWorker partialWorker = CreateWorker(
			"Workforce Roster Partial Worker",
			WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(partialWorker);
		SetCurrentAssignment(
			partialWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.IB });

		HumanWorker otherBuildingWorker = CreateWorker(
			"Workforce Roster Other Building Worker",
			WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(otherBuildingWorker);
		SetCurrentAssignment(otherBuildingWorker, otherStorage.RuntimeBuildingId, capsuleTasks);

		HumanWorker deadWorker = CreateWorker(
			"Workforce Roster Dead Worker",
			WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(deadWorker);
		SetCurrentAssignment(deadWorker, storage.RuntimeBuildingId, capsuleTasks);
		Assert.That(deadWorker.EnterIncapacitatedState(WorkerOperationalState.Death), Is.True);

		HumanWorker publicWorker = CreateWorker(
			"Workforce Roster Public Worker",
			WorkerAbility.CargoHandling,
			addCargoHandlingAbility: true);
		workerManager.RegisterWorker(publicWorker);
		SetCurrentAssignment(
			publicWorker,
			0,
			new[] { WorkerTask.TaskType.Unloading });

		List<WorkforceRoleWorkerEntry> results = new();
		Assert.That(
			workerManager.TryGetWorkforceRoleWorkers(
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling,
				results),
			Is.True);
		Assert.That(results.Count, Is.EqualTo(2));
		Assert.That(results[0].Worker, Is.SameAs(fullWorker));
		Assert.That(results[0].AssignmentState, Is.EqualTo(WorkforceRoleAssignmentState.Full));
		Assert.That(results[1].Worker, Is.SameAs(partialWorker));
		Assert.That(results[1].AssignmentState, Is.EqualTo(WorkforceRoleAssignmentState.Partial));

		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling,
				out WorkforceRoleSummary summary),
			Is.True);
		Assert.That(
			results.FindAll(entry =>
				entry.AssignmentState == WorkforceRoleAssignmentState.Full).Count,
			Is.EqualTo(summary.FullCount));
		Assert.That(
			results.FindAll(entry =>
				entry.AssignmentState == WorkforceRoleAssignmentState.Partial).Count,
			Is.EqualTo(summary.PartialCount));
		Assert.That(results.Count, Is.EqualTo(summary.OperationalCount));

		Assert.That(
			workerManager.TryGetWorkforceRoleWorkers(
				otherStorage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling,
				results),
			Is.True);
		Assert.That(results.Count, Is.EqualTo(1));
		Assert.That(results[0].Worker, Is.SameAs(otherBuildingWorker));

		Assert.That(
			workerManager.TryGetWorkforceRoleWorkers(
				0,
				WorkforceRole.Unloading,
				results),
			Is.True);
		Assert.That(results.Count, Is.EqualTo(1));
		Assert.That(results[0].Worker, Is.SameAs(publicWorker));
		Assert.That(results[0].AssignmentState, Is.EqualTo(WorkforceRoleAssignmentState.Full));

		Assert.That(
			workerManager.TryGetWorkforceRoleWorkers(
				storage.RuntimeBuildingId,
				WorkforceRole.Unloading,
				results),
			Is.False);
		Assert.That(results, Is.Empty);
		Assert.That(
			workerManager.TryGetWorkforceRoleWorkers(
				0,
				WorkforceRole.CapsuleHandling,
				results),
			Is.False);
		Assert.That(results, Is.Empty);
	}

	[Test]
	public void BuildingProvider_WorkforcePanelDisplaysSupportedRolesIncludingZero()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Panel Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });

		GameObject proxyObject = CreateGameObject("Workforce Building Selection Proxy");
		BuildingSelectionProxy proxy = proxyObject.AddComponent<BuildingSelectionProxy>();
		proxy.Bind(buildingManager, storage);
		BuildingUIProvider provider = new();
		provider.LinkObject(proxyObject);
		SelectionInspectorModel model = new();
		provider.BuildInspectorModel(model);

		Assert.That(model.Tabs, Is.Not.Empty);
		Assert.That(model.Tabs[0].Label, Is.EqualTo("Workforce"));
		SelectionDetailPanelModel panel = model.Tabs[0].BuildContent();
		Assert.That(panel.Title, Is.EqualTo("WORKFORCE"));
		Assert.That(panel.Rows.Count, Is.EqualTo(7));
		AssertWorkforceRow(panel.Rows[0], "Labeling", "0");
		AssertWorkforceRow(panel.Rows[1], "Storing", "1");
		AssertWorkforceRow(panel.Rows[2], "Picking", "0");
		AssertWorkforceRow(panel.Rows[3], "Packing", "0");
		AssertWorkforceRow(panel.Rows[4], "Packing Logistics", "0");
		AssertWorkforceRow(panel.Rows[5], "Launch Sorting", "0");
		AssertWorkforceRow(panel.Rows[6], "Capsule Handling", "0");

		int versionBeforeDeath = model.Tabs[0].GetContentVersion();
		Assert.That(worker.EnterIncapacitatedState(WorkerOperationalState.Death), Is.True);
		Assert.That(model.Tabs[0].GetContentVersion(), Is.Not.EqualTo(versionBeforeDeath));
		SelectionDetailPanelModel panelAfterDeath = model.Tabs[0].BuildContent();
		AssertWorkforceRow(panelAfterDeath.Rows[1], "Storing", "0");

		SelectionInspectorAction workMonitorAction = null;
		for (int i = 0; i < model.Actions.Count; ++i)
		{
			if (model.Actions[i].Label == "Work Monitor")
			{
				workMonitorAction = model.Actions[i];
				break;
			}
		}

		Assert.That(workMonitorAction, Is.Not.Null);
		Assert.That(workMonitorAction.Execute, Is.Not.Null);
		Assert.That(workMonitorAction.CanExecute?.Invoke(), Is.True);
		buildingManager.Unregister(storage);
		Assert.That(workMonitorAction.CanExecute?.Invoke(), Is.False);
	}

	[Test]
	public void BuildingProvider_SettingsActionOpensConsolidatedSettingsEditor()
	{
		Building building = CreateBuilding(ItemProcessStage.Picked);
		GameObject proxyObject = CreateGameObject("Settings Building Selection Proxy");
		BuildingSelectionProxy proxy = proxyObject.AddComponent<BuildingSelectionProxy>();
		proxy.Bind(buildingManager, building);
		BuildingUIProvider provider = new();
		provider.LinkObject(proxyObject);
		SelectionInspectorModel model = new();
		provider.BuildInspectorModel(model);

		SelectionInspectorAction settingsAction = null;
		for (int i = 0; i < model.Actions.Count; ++i)
		{
			SelectionInspectorAction action = model.Actions[i];
			Assert.That(action.Label, Does.Not.StartWith("Cycle "));
			if (action.Label == "Settings")
				settingsAction = action;
		}

		Assert.That(settingsAction, Is.Not.Null);
		Assert.That(settingsAction.TargetTabLabel, Is.EqualTo("Settings"));
		Assert.That(settingsAction.CanExecute?.Invoke(), Is.True);
		settingsAction.Execute?.Invoke();

		SelectionInspectorTab settingsTab = null;
		for (int i = 0; i < model.Tabs.Count; ++i)
		{
			if (model.Tabs[i].Label == "Settings")
			{
				settingsTab = model.Tabs[i];
				break;
			}
		}

		Assert.That(settingsTab, Is.Not.Null);
		SelectionDetailPanelModel panel = settingsTab.BuildContent();
		Assert.That(panel.Title, Is.EqualTo("SETTINGS"));
		Assert.That(panel.PreferredWidth, Is.GreaterThanOrEqualTo(420.0f));
		Assert.That(panel.PreferredHeight, Is.GreaterThanOrEqualTo(540.0f));
		Assert.That(panel.HasSlider, Is.True);
		Assert.That(panel.Editor, Is.Not.Null);
		Assert.That(panel.Editor.DropdownLabel, Is.EqualTo("Work Scope"));
		Assert.That(panel.Editor.DropdownChoices, Is.Not.Empty);
		Assert.That(panel.Editor.SecondaryDropdownLabel, Is.EqualTo("Outbound Target"));
		Assert.That(panel.Editor.SecondaryDropdownChoices, Does.Contain("Launch Ready"));
		Assert.That(panel.Editor.Toggles.Count, Is.EqualTo(2));
		Assert.That(panel.Editor.Toggles[0].Label, Is.EqualTo("Use Building Threshold"));
		Assert.That(panel.Editor.Toggles[1].Label, Is.EqualTo("Allow EVA Suit Removal"));
		Assert.That(panel.Editor.PrimaryActionLabel, Is.EqualTo("Apply"));
		Assert.That(panel.Editor.SecondaryActionLabel, Is.EqualTo("Cancel"));

		SetPrivateField(
			typeof(GameContext),
			context,
			"capsuleDockService",
			CreateComponent<CapsuleDockService>("Settings Test Capsule Dock Service", active: false));
		SetPrivateField(
			typeof(GameContext),
			context,
			"capsuleBufferService",
			CreateComponent<CapsuleBufferService>("Settings Test Capsule Buffer Service", active: false));
		SetPrivateField(
			typeof(GameContext),
			context,
			"facilityManager",
			CreateComponent<FacilityManager>("Settings Test Facility Manager", active: false));
		SetPrivateField(
			typeof(GameContext),
			context,
			"cargoPortService",
			CreateComponent<CargoPortService>("Settings Test Cargo Port Service", active: false));

		int launchReadyIndex = panel.Editor.SecondaryDropdownChoices.IndexOf("Launch Ready");
		Assert.That(launchReadyIndex, Is.GreaterThanOrEqualTo(0));
		panel.Editor.SecondaryDropdownChanged?.Invoke(launchReadyIndex);
		panel.Editor.PrimaryAction?.Invoke();
		Assert.That(building.OutboundTargetStage, Is.EqualTo(ItemProcessStage.LaunchReady));

		int packedIndex = panel.Editor.SecondaryDropdownChoices.IndexOf("Packed");
		panel.Editor.SecondaryDropdownChanged?.Invoke(packedIndex);
		panel.Editor.SecondaryAction?.Invoke();
		panel.Editor.PrimaryAction?.Invoke();
		Assert.That(building.OutboundTargetStage, Is.EqualTo(ItemProcessStage.LaunchReady));
	}

	[Test]
	public void ManagementContent_DeclaresAssignmentsFirstAndIncludesHierarchyShell()
	{
		VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
			"Assets/UI/Toolkit/WorkforceManagementContent.uxml");
		Assert.That(template, Is.Not.Null);
		TemplateContainer content = template.CloneTree();
		VisualElement tabs = content.Q<VisualElement>(className: "workforce-tabs");
		Assert.That(tabs, Is.Not.Null);

		List<string> buttonNames = new();
		foreach (VisualElement child in tabs.Children())
		{
			if (child is Button button)
				buttonNames.Add(button.name);
		}

		CollectionAssert.AreEqual(
			new[]
			{
				"workforce-assignments-button",
				"workforce-roster-button",
				"workforce-hiring-button",
			},
			buttonNames);
		Assert.That(content.Q<VisualElement>("workforce-assignments-tab"), Is.Not.Null);
		Assert.That(content.Q<ScrollView>("workforce-unassigned-list"), Is.Not.Null);
		Assert.That(content.Q<ScrollView>("workforce-assignment-tree"), Is.Not.Null);
		Assert.That(content.Q<VisualElement>("workforce-unassigned-drop-target"), Is.Not.Null);
		Assert.That(content.Q<Label>("workforce-assignment-drag-status"), Is.Not.Null);
	}

	[Test]
	public void ManagementAssignments_ShowsZeroRolesAndKeepsExpandedPartialWorker()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker unassignedWorker = CreateWorker(
			"Workforce UI Unassigned Worker",
			WorkerAbility.PickingStoring);
		workerManager.RegisterWorker(unassignedWorker);
		HumanWorker partialWorker = CreateWorker(
			"Workforce UI Partial Worker",
			WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(partialWorker);
		SetCurrentAssignment(
			partialWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.IB });

		VisualTreeAsset contentTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
			"Assets/UI/Toolkit/WorkforceManagementContent.uxml");
		VisualTreeAsset rowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
			"Assets/UI/Toolkit/WorkforceRosterRow.uxml");
		Assert.That(contentTemplate, Is.Not.Null);
		Assert.That(rowTemplate, Is.Not.Null);
		TemplateContainer content = contentTemplate.CloneTree();
		WorkforceManagementWindow controller =
			CreateComponent<WorkforceManagementWindow>("Workforce UI Controller", active: false);
		SetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"rosterRowTemplate",
			rowTemplate);
		SetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"workerManager",
			workerManager);
		SetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"buildingManager",
			buildingManager);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"InitializeAssignmentsView",
			content);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");

		Assert.That(
			content.Q<Label>("workforce-unassigned-count").text,
			Is.EqualTo("1 WORKERS"));
		ScrollView unassignedList = content.Q<ScrollView>("workforce-unassigned-list");
		List<VisualElement> unassignedRows = QueryByClass(
			unassignedList,
			"workforce-assignment-worker-row");
		Assert.That(unassignedRows.Count, Is.EqualTo(1));
		Assert.That(unassignedRows[0].userData, Is.SameAs(unassignedWorker));

		ScrollView tree = content.Q<ScrollView>("workforce-assignment-tree");
		List<VisualElement> groups = QueryByClass(tree, "workforce-assignment-group");
		Assert.That(groups.Count, Is.EqualTo(2));
		Assert.That(groups[0].userData, Is.EqualTo(0u));
		Assert.That(groups[1].userData, Is.EqualTo(storage.RuntimeBuildingId));

		List<VisualElement> publicRoles = QueryByClass(
			groups[0],
			"workforce-assignment-role");
		AssertRoleOrder(
			publicRoles,
			WorkforceRole.Unloading,
			WorkforceRole.Loading,
			WorkforceRole.CargoTransfer,
			WorkforceRole.WasteCollection);

		List<VisualElement> storageRoles = QueryByClass(
			groups[1],
			"workforce-assignment-role");
		AssertRoleOrder(
			storageRoles,
			WorkforceRole.Labeling,
			WorkforceRole.Storing,
			WorkforceRole.Picking,
			WorkforceRole.Packing,
			WorkforceRole.PackingLogistics,
			WorkforceRole.LaunchSorting,
			WorkforceRole.CapsuleHandling);
		AssertRoleCount(storageRoles[0], "0");
		AssertRoleCount(storageRoles[1], "0");
		AssertRoleCount(storageRoles[2], "0");
		AssertRoleCount(storageRoles[3], "0");
		AssertRoleCount(storageRoles[4], "0");
		AssertRoleCount(storageRoles[5], "0");
		AssertRoleCount(storageRoles[6], "1");
		Assert.That(
			storageRoles[6].ClassListContains("workforce-assignment-role--partial"),
			Is.True);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentRole",
			storage.RuntimeBuildingId,
			WorkforceRole.CapsuleHandling,
			1);
		groups = QueryByClass(tree, "workforce-assignment-group");
		List<VisualElement> expandedWorkers = QueryByClass(
			groups[1],
			"workforce-assignment-worker-row");
		Assert.That(expandedWorkers.Count, Is.EqualTo(1));
		Assert.That(expandedWorkers[0].userData, Is.SameAs(partialWorker));
		Assert.That(
			expandedWorkers[0].ClassListContains("workforce-assignment-worker-row--partial"),
			Is.True);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		groups = QueryByClass(tree, "workforce-assignment-group");
		Assert.That(
			QueryByClass(groups[1], "workforce-assignment-worker-row").Count,
			Is.EqualTo(1),
			"A refresh must preserve expanded role state.");
	}

	[Test]
	public void ManagementAssignments_ScopesDefaultExpandedAndReportActiveRoles()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker publicWorker = CreateWorker(
			"Workforce Public Scope Summary Worker",
			WorkerAbility.CargoHandling,
			addCargoHandlingAbility: true);
		workerManager.RegisterWorker(publicWorker);
		SetCurrentAssignment(
			publicWorker,
			0,
			new[] { WorkerTask.TaskType.Unloading });
		HumanWorker storageWorker = CreateWorker(
			"Workforce Building Scope Summary Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(storageWorker);
		SetCurrentAssignment(
			storageWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing, WorkerTask.TaskType.IB });
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		VisualElement publicGroup = FindAssignmentGroup(content, 0);
		VisualElement storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			publicGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.False);
		Assert.That(
			storageGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.False);
		Assert.That(
			QueryByClass(publicGroup, "workforce-assignment-role").Count,
			Is.EqualTo(4));
		Assert.That(
			QueryByClass(storageGroup, "workforce-assignment-role").Count,
			Is.EqualTo(7));
		Assert.That(
			publicGroup.Q<Label>(className: "workforce-assignment-group__summary").text,
			Is.EqualTo("1 / 4 ACTIVE ROLES"));
		Assert.That(
			storageGroup.Q<Label>(className: "workforce-assignment-group__summary").text,
			Is.EqualTo("2 / 7 ACTIVE ROLES"),
			"A partial Capsule Handling assignment still activates that role.");

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			storageGroup.Q<Label>(className: "workforce-assignment-group__summary").text,
			Is.EqualTo("2 / 7 ACTIVE ROLES"));
	}

	[Test]
	public void ManagementAssignments_CollapseRefreshExpandPreservesRoleStateAndZeroRows()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker partialWorker = CreateWorker(
			"Workforce Fold Partial Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(partialWorker);
		SetCurrentAssignment(
			partialWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.IB });
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentRole",
			storage.RuntimeBuildingId,
			WorkforceRole.CapsuleHandling,
			1);
		VisualElement storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			QueryByClass(storageGroup, "workforce-assignment-worker-row").Count,
			Is.EqualTo(1));

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			storage.RuntimeBuildingId);
		storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			storageGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.True);
		Assert.That(QueryByClass(storageGroup, "workforce-assignment-role").Count, Is.Zero);
		Assert.That(QueryByClass(storageGroup, "workforce-assignment-worker-row").Count, Is.Zero);
		Assert.That(
			storageGroup.Q<Label>(className: "workforce-assignment-group__summary").text,
			Is.EqualTo("1 / 7 ACTIVE ROLES"));

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			storageGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.True,
			"A normal refresh must preserve the collapsed scope.");

		SetCurrentAssignment(partialWorker, 0, Array.Empty<WorkerTask.TaskType>());
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			storageGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.True);
		Assert.That(
			storageGroup.Q<Label>(className: "workforce-assignment-group__summary").text,
			Is.EqualTo("0 / 7 ACTIVE ROLES"),
			"A folded scope must expose an operational role falling from one to zero.");
		SetCurrentAssignment(
			partialWorker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.IB });
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		Assert.That(
			storageGroup.Q<Label>(className: "workforce-assignment-group__summary").text,
			Is.EqualTo("1 / 7 ACTIVE ROLES"));

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			storage.RuntimeBuildingId);
		storageGroup = FindAssignmentGroup(content, storage.RuntimeBuildingId);
		List<VisualElement> storageRoles = QueryByClass(
			storageGroup,
			"workforce-assignment-role");
		AssertRoleOrder(
			storageRoles,
			WorkforceRole.Labeling,
			WorkforceRole.Storing,
			WorkforceRole.Picking,
			WorkforceRole.Packing,
			WorkforceRole.PackingLogistics,
			WorkforceRole.LaunchSorting,
			WorkforceRole.CapsuleHandling);
		AssertRoleCount(storageRoles[0], "0");
		AssertRoleCount(storageRoles[1], "0");
		AssertRoleCount(storageRoles[2], "0");
		AssertRoleCount(storageRoles[3], "0");
		AssertRoleCount(storageRoles[4], "0");
		AssertRoleCount(storageRoles[5], "0");
		AssertRoleCount(storageRoles[6], "1");
		Assert.That(
			QueryByClass(storageGroup, "workforce-assignment-worker-row").Count,
			Is.EqualTo(1),
			"Re-expanding the scope must restore the expanded role worker list.");

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			0u);
		VisualElement publicGroup = FindAssignmentGroup(content, 0);
		Assert.That(QueryByClass(publicGroup, "workforce-assignment-role").Count, Is.Zero);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		publicGroup = FindAssignmentGroup(content, 0);
		Assert.That(
			publicGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.True);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			0u);
		publicGroup = FindAssignmentGroup(content, 0);
		List<VisualElement> publicRoles = QueryByClass(
			publicGroup,
			"workforce-assignment-role");
		AssertRoleOrder(
			publicRoles,
			WorkforceRole.Unloading,
			WorkforceRole.Loading,
			WorkforceRole.CargoTransfer,
			WorkforceRole.WasteCollection);
		for (int i = 0; i < publicRoles.Count; ++i)
			AssertRoleCount(publicRoles[i], "0");
	}

	[Test]
	public void ManagementAssignments_CollapsedScopesRemoveAndRestoreDragTargets()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Fold Drag Target Worker",
			WorkerAbility.PickingStoring |
			WorkerAbility.CarryBox |
			WorkerAbility.CargoHandling,
			addCarryBoxAbility: true,
			addCargoHandlingAbility: true);
		workerManager.RegisterWorker(worker);
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(GetAssignmentRoleDropTargetCount(controller), Is.EqualTo(11));
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			0u);
		Assert.That(GetAssignmentRoleDropTargetCount(controller), Is.EqualTo(7));
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			storage.RuntimeBuildingId);
		Assert.That(GetAssignmentRoleDropTargetCount(controller), Is.Zero);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		Assert.That(GetAssignmentRoleDropTargetCount(controller), Is.Zero);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			0u);
		Assert.That(GetAssignmentRoleDropTargetCount(controller), Is.EqualTo(4));
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			storage.RuntimeBuildingId);
		Assert.That(GetAssignmentRoleDropTargetCount(controller), Is.EqualTo(11));

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Undefined),
			Is.True);
		VisualElement publicUnloading = FindAssignmentRoleRow(
			content,
			0,
			WorkforceRole.Unloading);
		VisualElement storageStoring = FindAssignmentRoleRow(
			content,
			storage.RuntimeBuildingId,
			WorkforceRole.Storing);
		Assert.That(
			publicUnloading.ClassListContains("workforce-assignment-role--drop-valid"),
			Is.True);
		Assert.That(
			storageStoring.ClassListContains("workforce-assignment-role--drop-valid"),
			Is.True);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementAssignments_BuildingResetCancelsDragAndPrunesReusedScopeState()
	{
		Building originalStorage = CreateBuilding(ItemProcessStage.Picked);
		uint reusedBuildingId = originalStorage.RuntimeBuildingId;
		HumanWorker assignedWorker = CreateWorker(
			"Workforce Fold State Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(assignedWorker);
		SetCurrentAssignment(
			assignedWorker,
			originalStorage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		HumanWorker dragWorker = CreateWorker(
			"Workforce Fold Reset Drag Worker",
			WorkerAbility.PickingStoring |
			WorkerAbility.CarryBox |
			WorkerAbility.CargoHandling,
			addCarryBoxAbility: true,
			addCargoHandlingAbility: true);
		workerManager.RegisterWorker(dragWorker);
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentRole",
			originalStorage.RuntimeBuildingId,
			WorkforceRole.Storing,
			1);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ToggleAssignmentScope",
			originalStorage.RuntimeBuildingId);
		SetCurrentAssignment(assignedWorker, 0, Array.Empty<WorkerTask.TaskType>());
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				dragWorker,
				0u,
				WorkforceRole.Undefined),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				0u,
				WorkforceRole.Unloading),
			Is.True,
			"The drag must have a valid target before the building generation changes.");

		buildingManager.ResetRuntimeState();
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"OnBuildingsChanged");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				0u,
				WorkforceRole.Unloading),
			Is.False,
			"A building generation change must cancel the active drag.");

		Building replacementStorage = CreateBuilding(ItemProcessStage.Picked);
		Assert.That(replacementStorage.RuntimeBuildingId, Is.EqualTo(reusedBuildingId));
		SetCurrentAssignment(
			assignedWorker,
			replacementStorage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"OnBuildingsChanged");
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");

		VisualElement replacementGroup = FindAssignmentGroup(
			content,
			replacementStorage.RuntimeBuildingId);
		Assert.That(
			replacementGroup.ClassListContains("workforce-assignment-group--collapsed"),
			Is.False,
			"A reused runtime ID must not inherit the previous building's fold state.");
		Assert.That(
			QueryByClass(replacementGroup, "workforce-assignment-role").Count,
			Is.EqualTo(7));
		Assert.That(
			QueryByClass(replacementGroup, "workforce-assignment-worker-row").Count,
			Is.Zero,
			"A reused runtime ID must not inherit stale expanded-role state.");
	}

	[Test]
	public void ManagementDrag_HighlightsOnlyEligibleRolesAndCancelDoesNotMutate()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		Building staging = CreateBuilding(ItemProcessStage.Labeled);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Eligibility Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Undefined),
			Is.True);
		VisualElement storing = FindAssignmentRoleRow(
			content,
			storage.RuntimeBuildingId,
			WorkforceRole.Storing);
		VisualElement labeling = FindAssignmentRoleRow(
			content,
			staging.RuntimeBuildingId,
			WorkforceRole.Labeling);
		VisualElement unassignedTarget =
			content.Q<VisualElement>("workforce-unassigned-drop-target");

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				staging.RuntimeBuildingId,
				WorkforceRole.Labeling),
			Is.False);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnUnassigned"),
			Is.False);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				staging.RuntimeBuildingId,
				WorkforceRole.Labeling),
			Is.False);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		Assert.That(worker.AssignedTaskTypes, Is.Empty);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(workerChangedCount, Is.Zero);
		Assert.That(
			storing.ClassListContains("workforce-assignment-role--drop-valid"),
			Is.True);
		Assert.That(
			labeling.ClassListContains("workforce-assignment-role--drop-invalid"),
			Is.True);
		Assert.That(
			unassignedTarget.ClassListContains("workforce-unassigned-column--drop-invalid"),
			Is.True);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
		Assert.That(
			storing.ClassListContains("workforce-assignment-role--drop-valid"),
			Is.False);
		Assert.That(
			labeling.ClassListContains("workforce-assignment-role--drop-invalid"),
			Is.False);
		Assert.That(
			unassignedTarget.ClassListContains("workforce-unassigned-column--drop-invalid"),
			Is.False);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.False);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		Assert.That(worker.AssignedTaskTypes, Is.Empty);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(workerChangedCount, Is.Zero);
	}

	[Test]
	public void ManagementDrag_UnassignedToRoleMutatesThroughWorkerManager()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Role Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Undefined),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);

		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(workerChangedCount, Is.GreaterThan(0));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary summary),
			Is.True);
		Assert.That(summary.OperationalCount, Is.EqualTo(1));
		List<AIWorker> unassigned = new();
		workerManager.GetOperationalUnassignedWorkers(unassigned);
		CollectionAssert.DoesNotContain(unassigned, worker);
		Label feedback = content.Q<Label>("workforce-assignment-drag-status");
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--success"),
			Is.True);
		StringAssert.Contains("assigned to Storing", feedback.text);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementDrag_ExactCurrentRoleIsNoOpButUnassignedDropApplies()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Unassignment Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		VisualElement storing = FindAssignmentRoleRow(
			content,
			storage.RuntimeBuildingId,
			WorkforceRole.Storing);
		VisualElement unassignedTarget =
			content.Q<VisualElement>("workforce-unassigned-drop-target");
		Assert.That(
			storing.ClassListContains("workforce-assignment-role--drop-invalid"),
			Is.True);
		Assert.That(
			unassignedTarget.ClassListContains("workforce-unassigned-column--drop-valid"),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.False);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);
		Assert.That(workerChangedCount, Is.Zero);
		Assert.That(worker.HasPendingAssignment, Is.False);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnUnassigned"),
			Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		Assert.That(worker.AssignedTaskTypes, Is.Empty);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(workerChangedCount, Is.GreaterThan(0));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary summary),
			Is.True);
		Assert.That(summary.OperationalCount, Is.Zero);
		List<AIWorker> unassigned = new();
		workerManager.GetOperationalUnassignedWorkers(unassigned);
		Assert.That(unassigned, Does.Contain(worker));

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementDrag_AssignedRoleRejectsIneligibleAndReplacesEntireBundle()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		Building staging = CreateBuilding(ItemProcessStage.Labeled);
		HumanWorker worker = CreateWorker(
			"Workforce Direct Role Replacement Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[]
			{
				WorkerTask.TaskType.Storing,
				WorkerTask.TaskType.Picking,
			});
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		VisualElement labeling = FindAssignmentRoleRow(
			content,
			staging.RuntimeBuildingId,
			WorkforceRole.Labeling);
		VisualElement capsuleHandling = FindAssignmentRoleRow(
			content,
			storage.RuntimeBuildingId,
			WorkforceRole.CapsuleHandling);
		Assert.That(
			labeling.ClassListContains("workforce-assignment-role--drop-invalid"),
			Is.True);
		Assert.That(
			capsuleHandling.ClassListContains("workforce-assignment-role--drop-valid"),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				staging.RuntimeBuildingId,
				WorkforceRole.Labeling),
			Is.False);
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing, WorkerTask.TaskType.Picking },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(workerChangedCount, Is.Zero);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling),
			Is.True);

		CollectionAssert.AreEqual(
			new[]
			{
				WorkerTask.TaskType.IB,
				WorkerTask.TaskType.CapsuleClear,
				WorkerTask.TaskType.CapsuleSupply,
				WorkerTask.TaskType.OB,
			},
			worker.AssignedTaskTypes);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(workerChangedCount, Is.GreaterThan(0));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary storingSummary),
			Is.True);
		Assert.That(storingSummary.OperationalCount, Is.Zero);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Picking,
				out WorkforceRoleSummary pickingSummary),
			Is.True);
		Assert.That(pickingSummary.OperationalCount, Is.Zero);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling,
				out WorkforceRoleSummary capsuleSummary),
			Is.True);
		Assert.That(capsuleSummary.FullCount, Is.EqualTo(1));
		Assert.That(capsuleSummary.PartialCount, Is.Zero);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementDrag_PartialRoleCanNormalizeToItsExactBundle()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Partial Role Normalization Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.IB });
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling),
			Is.True);
		VisualElement capsuleHandling = FindAssignmentRoleRow(
			content,
			storage.RuntimeBuildingId,
			WorkforceRole.CapsuleHandling);
		Assert.That(
			capsuleHandling.ClassListContains("workforce-assignment-role--drop-valid"),
			Is.True,
			"A partial role is not an exact no-op and can be normalized.");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.CapsuleHandling),
			Is.True);

		Assert.That(
			WorkforceRoleCatalog.GetAssignmentState(
				WorkforceRole.CapsuleHandling,
				worker.AssignedTaskTypes),
			Is.EqualTo(WorkforceRoleAssignmentState.Full));
		CollectionAssert.AreEqual(
			new[]
			{
				WorkerTask.TaskType.IB,
				WorkerTask.TaskType.CapsuleClear,
				WorkerTask.TaskType.CapsuleSupply,
				WorkerTask.TaskType.OB,
			},
			worker.AssignedTaskTypes);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementDrag_AssignedRoleSupportsCrossBuildingAndPublicRoundTrip()
	{
		Building storageA = CreateBuilding(ItemProcessStage.Picked);
		Building storageB = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Cross Scope Reassignment Worker",
			WorkerAbility.PickingStoring |
			WorkerAbility.CarryBox |
			WorkerAbility.CargoHandling,
			addCarryBoxAbility: true,
			addCargoHandlingAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storageA.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out _);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storageA.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storageB.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True,
			"The same role in a different building is a real reassignment.");
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storageB.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ClearAssignmentDragState");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storageB.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				0u,
				WorkforceRole.Unloading),
			Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Unloading },
			worker.AssignedTaskTypes);
		List<AIWorker> unassigned = new();
		workerManager.GetOperationalUnassignedWorkers(unassigned);
		CollectionAssert.DoesNotContain(unassigned, worker);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ClearAssignmentDragState");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Unloading),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storageA.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.True);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storageA.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Picking },
			worker.AssignedTaskTypes);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementDrag_BusyRoleReassignmentKeepsCurrentCountsUntilApplied()
	{
		Building sourceStorage = CreateBuilding(ItemProcessStage.Picked);
		Building targetStorage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Busy Direct Reassignment Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			sourceStorage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		WorkerTask currentTask = new CapsuleRelocationTask(
			WorkerTask.TaskType.IB,
			null,
			null,
			sourceStorage.RuntimeBuildingId,
			CapsuleRelocationReason.RoleChanged);
		SetPrivateField(typeof(AIWorker), worker, "currentTask", currentTask);
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				sourceStorage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.True);

		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(sourceStorage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.True);
		Assert.That(
			worker.PendingPrimaryBuildingId,
			Is.EqualTo(targetStorage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Picking },
			worker.PendingAssignedTaskTypes);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				sourceStorage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary sourceBeforeApply),
			Is.True);
		Assert.That(sourceBeforeApply.OperationalCount, Is.EqualTo(1));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking,
				out WorkforceRoleSummary targetBeforeApply),
			Is.True);
		Assert.That(targetBeforeApply.OperationalCount, Is.Zero);
		Label feedback = content.Q<Label>("workforce-assignment-drag-status");
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--pending"),
			Is.True);
		StringAssert.Contains("after the current task", feedback.text);
		int eventsAfterSchedule = workerChangedCount;

		WorkforceManagementWindow noOpController =
			CreateAssignmentsController(out _);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				noOpController,
				"BeginAssignmentDrag",
				worker,
				sourceStorage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				noOpController,
				"TryDropAssignmentOnRole",
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False,
			"Re-requesting the exact queued target must be a no-op.");
		Assert.That(workerChangedCount, Is.EqualTo(eventsAfterSchedule));
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			noOpController,
			"CancelAssignmentDrag");

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ClearAssignmentDragState");
		SetPrivateField(typeof(AIWorker), worker, "currentTask", null);
		Assert.That(workerManager.TryApplyPendingAssignment(worker), Is.True);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");

		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(targetStorage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Picking },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				sourceStorage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary sourceAfterApply),
			Is.True);
		Assert.That(sourceAfterApply.OperationalCount, Is.Zero);
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking,
				out WorkforceRoleSummary targetAfterApply),
			Is.True);
		Assert.That(targetAfterApply.OperationalCount, Is.EqualTo(1));
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--pending"),
			Is.False);
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--success"),
			Is.True);
		StringAssert.Contains("assigned to Picking", feedback.text);
	}

	[Test]
	public void ManagementDrag_BusyUnassignSchedulesPendingWithoutChangingCurrentCounts()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Busy Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		WorkerTask currentTask = new CapsuleRelocationTask(
			WorkerTask.TaskType.IB,
			null,
			null,
			storage.RuntimeBuildingId,
			CapsuleRelocationReason.RoleChanged);
		SetPrivateField(typeof(AIWorker), worker, "currentTask", currentTask);
		int workerChangedCount = 0;
		workerManager.OnWorkerChanged += _ => ++workerChangedCount;
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnUnassigned"),
			Is.True);

		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.True);
		Assert.That(worker.PendingPrimaryBuildingId, Is.Zero);
		Assert.That(worker.PendingAssignedTaskTypes, Is.Empty);
		Assert.That(workerChangedCount, Is.EqualTo(1));
		Assert.That(
			workerManager.TryGetWorkforceRoleSummary(
				storage.RuntimeBuildingId,
				WorkforceRole.Storing,
				out WorkforceRoleSummary summary),
			Is.True);
		Assert.That(summary.OperationalCount, Is.EqualTo(1));
		List<AIWorker> unassigned = new();
		workerManager.GetOperationalUnassignedWorkers(unassigned);
		CollectionAssert.DoesNotContain(unassigned, worker);
		Label feedback = content.Q<Label>("workforce-assignment-drag-status");
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--pending"),
			Is.True);
		StringAssert.Contains("after the current task", feedback.text);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ClearAssignmentDragState");
		SetPrivateField(typeof(AIWorker), worker, "currentTask", null);
		Assert.That(workerManager.TryApplyPendingAssignment(worker), Is.True);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		Assert.That(worker.HasPendingAssignment, Is.False);
		Assert.That(worker.PrimaryBuildingId, Is.Zero);
		Assert.That(worker.AssignedTaskTypes, Is.Empty);
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--pending"),
			Is.False);
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--success"),
			Is.True);
		StringAssert.Contains("is now unassigned", feedback.text);
	}

	[Test]
	public void ManagementDrag_RejectsStaleOrForgedSourceRows()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		Building targetStorage = CreateBuilding(ItemProcessStage.Picked);
		Building staging = CreateBuilding(ItemProcessStage.Labeled);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Forged Source Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out _);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Undefined),
			Is.False,
			"An assigned worker cannot be presented as an Unassigned source.");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				staging.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.False,
			"The source building must still match the current assignment.");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False,
			"The source role must still contain the worker.");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);

		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[]
			{
				WorkerTask.TaskType.Storing,
				WorkerTask.TaskType.Picking,
			});
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False,
			"Retaining the source role must not hide a changed task-type set.");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnUnassigned"),
			Is.False,
			"Drop must revalidate the source after the drag has started.");
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnUnassigned"),
			Is.False);
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing, WorkerTask.TaskType.Picking },
			worker.AssignedTaskTypes);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");

		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		buildingManager.Unregister(targetStorage);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				targetStorage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);
		Assert.That(worker.HasPendingAssignment, Is.False);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void ManagementDrag_PendingFeedbackStopsWhenRoleTargetDisappears()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Removed Target Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		WorkerTask currentTask = new CapsuleRelocationTask(
			WorkerTask.TaskType.IB,
			null,
			null,
			storage.RuntimeBuildingId,
			CapsuleRelocationReason.RoleChanged);
		SetPrivateField(typeof(AIWorker), worker, "currentTask", currentTask);
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out TemplateContainer content);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Undefined),
			Is.True);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Storing),
			Is.True);
		Label feedback = content.Q<Label>("workforce-assignment-drag-status");
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--pending"),
			Is.True);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"ClearAssignmentDragState");
		buildingManager.Unregister(storage);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");

		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--pending"),
			Is.False);
		Assert.That(
			feedback.ClassListContains("workforce-assignment-drag-status--error"),
			Is.True);
		StringAssert.Contains("could not be completed", feedback.text);
		SetPrivateField(typeof(AIWorker), worker, "currentTask", null);
		Assert.That(workerManager.CancelPendingWorkerAssignment(worker), Is.True);
	}

	[Test]
	public void ManagementDrag_RevalidatesSourceStateBeforeDrop()
	{
		Building storage = CreateBuilding(ItemProcessStage.Picked);
		HumanWorker worker = CreateWorker(
			"Workforce Drag Source Revalidation Worker",
			WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			addCarryBoxAbility: true);
		workerManager.RegisterWorker(worker);
		WorkforceManagementWindow controller =
			CreateAssignmentsController(out _);

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"BeginAssignmentDrag",
				worker,
				0u,
				WorkforceRole.Undefined),
			Is.True);
		SetCurrentAssignment(
			worker,
			storage.RuntimeBuildingId,
			new[] { WorkerTask.TaskType.Storing });

		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"CanDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False);
		Assert.That(
			(bool)InvokeNonPublic(
				typeof(WorkforceManagementWindow),
				controller,
				"TryDropAssignmentOnRole",
				storage.RuntimeBuildingId,
				WorkforceRole.Picking),
			Is.False);
		Assert.That(worker.PrimaryBuildingId, Is.EqualTo(storage.RuntimeBuildingId));
		CollectionAssert.AreEqual(
			new[] { WorkerTask.TaskType.Storing },
			worker.AssignedTaskTypes);

		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"CancelAssignmentDrag");
	}

	[Test]
	public void BuildingManager_RaisesChangesForRegisterUnregisterAndReset()
	{
		int changedCount = 0;
		buildingManager.OnBuildingsChanged += () => ++changedCount;
		Building building = new(
			"Workforce Event Storage",
			new List<GridCell>(),
			ItemProcessStage.Picked);

		buildingManager.Register(building);
		Assert.That(changedCount, Is.EqualTo(1));
		buildingManager.Register(building);
		Assert.That(changedCount, Is.EqualTo(1));
		buildingManager.Unregister(building);
		Assert.That(changedCount, Is.EqualTo(2));
		buildingManager.Register(building);
		Assert.That(changedCount, Is.EqualTo(3));
		buildingManager.ResetRuntimeState();
		Assert.That(changedCount, Is.EqualTo(4));
		buildingManager.ResetRuntimeState();
		Assert.That(changedCount, Is.EqualTo(4));
	}

	private static void SetCurrentAssignment(
		AIWorker worker,
		uint buildingId,
		IEnumerable<WorkerTask.TaskType> taskTypes)
	{
		worker.SetPrimaryBuildingId(buildingId);
		worker.SetAssignedTaskTypes(taskTypes);
	}

	private Building CreateBuilding(ItemProcessStage outboundTargetStage)
	{
		Building building = new($"Workforce Test {outboundTargetStage}", new List<GridCell>(), outboundTargetStage);
		buildingManager.Register(building);
		return building;
	}

	private HumanWorker CreateWorker(
		string objectName,
		WorkerAbility abilities,
		bool addCarryBoxAbility = false,
		bool addCargoHandlingAbility = false)
	{
		GameObject workerObject = CreateGameObject(objectName);
		GameObject slotObject = new("SlotRoot");
		slotObject.transform.SetParent(workerObject.transform, false);
		HumanWorker worker = workerObject.AddComponent<HumanWorker>();
		SetPrivateField(typeof(AIWorker), worker, "abilities", abilities);
		if (addCarryBoxAbility)
			workerObject.AddComponent<CarryBoxAbility>();
		if (addCargoHandlingAbility)
			workerObject.AddComponent<CargoHandlingAbility>();
		return worker;
	}

	private static void AssertDefinition(
		WorkforceRole role,
		string displayName,
		params WorkerTask.TaskType[] expectedTaskTypes)
	{
		Assert.That(
			WorkforceRoleCatalog.TryGetDefinition(role, out WorkforceRoleDefinition definition),
			Is.True);
		Assert.That(definition.Role, Is.EqualTo(role));
		Assert.That(definition.DisplayName, Is.EqualTo(displayName));
		CollectionAssert.AreEqual(expectedTaskTypes, definition.TaskTypes);
	}

	private static void AssertWorkforceRow(
		SelectionDetailRow row,
		string expectedLabel,
		string expectedCount)
	{
		Assert.That(row.Primary, Is.EqualTo(expectedLabel));
		Assert.That(row.Trailing, Is.EqualTo(expectedCount));
	}

	private static List<VisualElement> QueryByClass(VisualElement root, string className)
	{
		List<VisualElement> results = new();
		root.Query<VisualElement>(className: className).ForEach(results.Add);
		return results;
	}

	private static void AssertRoleOrder(
		IReadOnlyList<VisualElement> roleRows,
		params WorkforceRole[] expectedRoles)
	{
		Assert.That(roleRows.Count, Is.EqualTo(expectedRoles.Length));
		for (int i = 0; i < expectedRoles.Length; ++i)
			Assert.That(roleRows[i].userData, Is.EqualTo(expectedRoles[i]));
	}

	private static void AssertRoleCount(VisualElement roleRow, string expectedCount)
	{
		Label count = roleRow.Q<Label>(className: "workforce-assignment-role__count");
		Assert.That(count, Is.Not.Null);
		Assert.That(count.text, Is.EqualTo(expectedCount));
	}

	private WorkforceManagementWindow CreateAssignmentsController(
		out TemplateContainer content)
	{
		VisualTreeAsset contentTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
			"Assets/UI/Toolkit/WorkforceManagementContent.uxml");
		VisualTreeAsset rowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
			"Assets/UI/Toolkit/WorkforceRosterRow.uxml");
		Assert.That(contentTemplate, Is.Not.Null);
		Assert.That(rowTemplate, Is.Not.Null);
		content = contentTemplate.CloneTree();
		WorkforceManagementWindow controller =
			CreateComponent<WorkforceManagementWindow>("Workforce Drag UI Controller", active: false);
		SetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"rosterRowTemplate",
			rowTemplate);
		SetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"workerManager",
			workerManager);
		SetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"buildingManager",
			buildingManager);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"InitializeAssignmentsView",
			content);
		InvokeNonPublic(
			typeof(WorkforceManagementWindow),
			controller,
			"RefreshAssignments");
		return controller;
	}

	private static VisualElement FindAssignmentRoleRow(
		VisualElement content,
		uint buildingId,
		WorkforceRole role)
	{
		VisualElement group = FindAssignmentGroup(content, buildingId);
		List<VisualElement> roles = QueryByClass(group, "workforce-assignment-role");
		for (int roleIndex = 0; roleIndex < roles.Count; ++roleIndex)
		{
			if (roles[roleIndex].userData is WorkforceRole candidate && candidate == role)
				return roles[roleIndex];
		}

		Assert.Fail($"Missing assignment role row for building {buildingId}, role {role}.");
		return null;
	}

	private static VisualElement FindAssignmentGroup(VisualElement content, uint buildingId)
	{
		List<VisualElement> groups = QueryByClass(content, "workforce-assignment-group");
		for (int i = 0; i < groups.Count; ++i)
		{
			if (groups[i].userData is uint candidate && candidate == buildingId)
				return groups[i];
		}

		Assert.Fail($"Missing assignment group for building {buildingId}.");
		return null;
	}

	private static int GetAssignmentRoleDropTargetCount(
		WorkforceManagementWindow controller)
	{
		object targets = GetPrivateField(
			typeof(WorkforceManagementWindow),
			controller,
			"assignmentRoleDropTargets");
		Assert.That(targets, Is.InstanceOf<System.Collections.ICollection>());
		return ((System.Collections.ICollection)targets).Count;
	}

	private T CreateComponent<T>(string objectName, bool active = true) where T : Component
	{
		return CreateGameObject(objectName, active).AddComponent<T>();
	}

	private GameObject CreateGameObject(string objectName, bool active = true)
	{
		GameObject gameObject = new(objectName);
		if (active == false)
			gameObject.SetActive(false);
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

	private static object InvokeNonPublic(
		Type ownerType,
		object target,
		string methodName,
		params object[] arguments)
	{
		MethodInfo method = ownerType.GetMethod(
			methodName,
			BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing test method {ownerType.Name}.{methodName}");
		return method.Invoke(target, arguments);
	}
}
}
