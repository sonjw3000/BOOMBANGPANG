using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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
		InvokeNonPublic(typeof(WorkerManager), workerManager, "Awake");
		InvokeNonPublic(typeof(BuildingManager), buildingManager, "Awake");
		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");

		GameObject contextObject = CreateGameObject("Workforce Test Context", active: false);
		context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "buildingManager", buildingManager);
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
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

	[Test]
	public void WorkforceSummary_CountsCurrentFullAndPartialOperationalAssignments()
	{
		Building storage = CreateBuilding(BuildingType.Storage);
		Building otherStorage = CreateBuilding(BuildingType.Storage);
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
				out _),
			Is.False);
	}

	[Test]
	public void WorkforceSummary_UsesCurrentAssignmentInsteadOfPendingAssignment()
	{
		Building currentStorage = CreateBuilding(BuildingType.Storage);
		Building pendingStorage = CreateBuilding(BuildingType.Storage);
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
	public void BuildingProvider_WorkforcePanelDisplaysSupportedRolesIncludingZero()
	{
		Building storage = CreateBuilding(BuildingType.Storage);
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
		Assert.That(panel.Rows.Count, Is.EqualTo(3));
		AssertWorkforceRow(panel.Rows[0], "Storing", "1");
		AssertWorkforceRow(panel.Rows[1], "Picking", "0");
		AssertWorkforceRow(panel.Rows[2], "Capsule Handling", "0");

		int versionBeforeDeath = model.Tabs[0].GetContentVersion();
		Assert.That(worker.EnterIncapacitatedState(WorkerOperationalState.Death), Is.True);
		Assert.That(model.Tabs[0].GetContentVersion(), Is.Not.EqualTo(versionBeforeDeath));
		SelectionDetailPanelModel panelAfterDeath = model.Tabs[0].BuildContent();
		AssertWorkforceRow(panelAfterDeath.Rows[0], "Storing", "0");
	}

	private static void SetCurrentAssignment(
		AIWorker worker,
		uint buildingId,
		IEnumerable<WorkerTask.TaskType> taskTypes)
	{
		worker.SetPrimaryBuildingId(buildingId);
		worker.SetAssignedTaskTypes(taskTypes);
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

	private static void AssertWorkforceRow(
		SelectionDetailRow row,
		string expectedLabel,
		string expectedCount)
	{
		Assert.That(row.Primary, Is.EqualTo(expectedLabel));
		Assert.That(row.Trailing, Is.EqualTo(expectedCount));
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
