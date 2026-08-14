using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Pang.Tests.Editor
{
public sealed class WorkforceAssignmentEditModeTests
{
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private GameContext context;
	private WorkerManager workerManager;
	private BuildingManager buildingManager;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		workerManager = CreateComponent<WorkerManager>("Workforce Test Worker Manager", active: false);
		buildingManager = CreateComponent<BuildingManager>("Workforce Test Building Manager", active: false);
		InvokeNonPublic(typeof(WorkerManager), workerManager, "Awake");
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");

		GameObject contextObject = CreateGameObject("Workforce Test Context", active: false);
		context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
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
		CollectionAssert.AreEqual(
			new[] { WorkforceRole.CapsuleHandling },
			WorkforceRoleCatalog.GetRoles(BuildingType.Generic));
		CollectionAssert.AreEqual(
			new[] { WorkforceRole.Labeling, WorkforceRole.CapsuleHandling },
			WorkforceRoleCatalog.GetRoles(BuildingType.Staging));
		CollectionAssert.AreEqual(
			new[]
			{
				WorkforceRole.Storing,
				WorkforceRole.Picking,
				WorkforceRole.CapsuleHandling,
			},
			WorkforceRoleCatalog.GetRoles(BuildingType.Storage));
		CollectionAssert.AreEqual(
			new[]
			{
				WorkforceRole.Packing,
				WorkforceRole.PackingLogistics,
				WorkforceRole.CapsuleHandling,
			},
			WorkforceRoleCatalog.GetRoles(BuildingType.Packing));
		CollectionAssert.AreEqual(
			new[] { WorkforceRole.LaunchSorting, WorkforceRole.CapsuleHandling },
			WorkforceRoleCatalog.GetRoles(BuildingType.Launch));
		CollectionAssert.AreEqual(
			new[]
			{
				WorkforceRole.Unloading,
				WorkforceRole.Loading,
				WorkforceRole.CargoTransfer,
				WorkforceRole.WasteCollection,
			},
			WorkforceRoleCatalog.GetRoles(null));
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
		Building storage = CreateBuilding(BuildingType.Storage);
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
		Building storage = CreateBuilding(BuildingType.Storage);
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
		Building storage = CreateBuilding(BuildingType.Storage);
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
		Building staging = CreateBuilding(BuildingType.Staging);
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

	private Building CreateBuilding(BuildingType buildingType)
	{
		Building building = new($"Workforce Test {buildingType}", new List<GridCell>(), buildingType);
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
