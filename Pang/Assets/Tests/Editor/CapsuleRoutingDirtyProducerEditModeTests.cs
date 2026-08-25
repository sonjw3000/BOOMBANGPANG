using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CapsuleRoutingDirtyProducerEditModeTests
{
	private const uint TestItemId = 94001;
	private readonly List<UnityEngine.Object> createdObjects = new();
	private GameContext previousContext;
	private GameContext context;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		context = CreateComponent<GameContext>("Dirty Producer Test Context");
		ItemDatabase itemDatabase = CreateComponent<ItemDatabase>("Dirty Producer Test Item Database");
		CargoPortService cargoPortService = CreateComponent<CargoPortService>("Dirty Producer Test Cargo Port Service");
		ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
		createdObjects.Add(itemDefinition);
		SetPrivateField(typeof(ItemDefinition), itemDefinition, "itemID", TestItemId);
		SetPrivateField(typeof(ItemDefinition), itemDefinition, "size", 1.0f);
		SetPrivateField(
			typeof(ItemDatabase),
			itemDatabase,
			"itemIDMap",
			new Dictionary<uint, ItemDefinition> { [TestItemId] = itemDefinition });

		SetPrivateField(typeof(GameContext), context, "itemDB", itemDatabase);
		SetPrivateField(typeof(GameContext), context, "cargoPortService", cargoPortService);
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
	public void CargoPort_PartialContentChange_MarksDockDirtyWithoutThresholdEvent()
	{
		CapsuleDock evaluatedDock = null;
		CapsuleRelocateCoordinator coordinator = new(
			dockService: null,
			evaluateDirtyDock: dock => evaluatedDock = dock);
		SetPrivateField(typeof(GameContext), context, "capsuleRelocateCoordinator", coordinator);

		InboundCargoPort port = CreateComponent<InboundCargoPort>("Dirty Producer Inbound Port");
		CargoCapsule capsule = CreateComponent<CargoCapsule>("Dirty Producer Capsule");
		Assert.That(port.TryDockCapsule(capsule), Is.True);

		Building building = new("Dirty Producer Building", new List<GridCell>(), BuildingType.Generic);
		InvokeNonPublic(typeof(Building), building, "SubscribeCargoPort", port);

		int contentChangeCount = 0;
		int zeroThresholdCount = 0;
		int overThresholdCount = 0;
		port.OnCargoContentChanged += _ => ++contentChangeCount;
		port.OnCargoQuantityZero += _ => ++zeroThresholdCount;
		port.OnCargoQuantityOverPercent += _ => ++overThresholdCount;

		Assert.That(capsule.AddItem(TestItemId, 1), Is.EqualTo(1));

		Assert.That(port.FilledPercent, Is.EqualTo(10.0f).Within(0.001f));
		Assert.That(contentChangeCount, Is.EqualTo(1));
		Assert.That(zeroThresholdCount, Is.Zero);
		Assert.That(overThresholdCount, Is.Zero);
		Assert.That(coordinator.DirtyDockCount, Is.EqualTo(1));

		coordinator.ProcessDirty();

		Assert.That(evaluatedDock, Is.SameAs(port));
		Assert.That(coordinator.HasDirty, Is.False);
		InvokeNonPublic(typeof(Building), building, "UnsubscribeCargoPort", port);
	}

	[Test]
	public void ProcessDirty_BuildingAndItsDockInSameBatch_CoalescesDockEvaluation()
	{
		CapsuleDockService dockService = CreateComponent<CapsuleDockService>("Dirty Coalesce Dock Service");
		InboundCargoPort coveredDock = CreateComponent<InboundCargoPort>("Covered Dirty Dock");
		InboundCargoPort otherDock = CreateComponent<InboundCargoPort>("Other Dirty Dock");
		InvokeNonPublic(typeof(CapsuleDockService), dockService, "OnRegisterFacility", 41u, coveredDock);
		InvokeNonPublic(typeof(CapsuleDockService), dockService, "OnRegisterFacility", 42u, otherDock);
		Assert.That(dockService.TryGetRegisteredBuildingId(coveredDock, out uint coveredBuildingId), Is.True);
		Assert.That(coveredBuildingId, Is.EqualTo(41u));

		List<uint> evaluatedBuildings = new();
		List<CapsuleDock> evaluatedDocks = new();
		CapsuleRelocateCoordinator coordinator = new(
			dockService,
			evaluateDirtyDock: evaluatedDocks.Add,
			evaluateDirtyBuilding: evaluatedBuildings.Add);

		coordinator.MarkBuildingDirty(41u);
		coordinator.MarkDirty(coveredDock);
		coordinator.MarkDirty(otherDock);
		coordinator.ProcessDirty();

		Assert.That(evaluatedBuildings, Is.EqualTo(new uint[] { 41u }));
		Assert.That(evaluatedDocks, Is.EqualTo(new CapsuleDock[] { otherDock }));
		Assert.That(coordinator.HasDirty, Is.False);
	}

	private T CreateComponent<T>(string objectName) where T : Component
	{
		GameObject gameObject = new(objectName);
		gameObject.SetActive(false);
		createdObjects.Add(gameObject);
		return gameObject.AddComponent<T>();
	}

	private static object GetPrivateStaticField(Type type, string fieldName)
	{
		return GetField(type, fieldName, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
	}

	private static void SetPrivateStaticField(Type type, string fieldName, object value)
	{
		GetField(type, fieldName, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
	}

	private static void SetPrivateField(Type type, object target, string fieldName, object value)
	{
		GetField(type, fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
	}

	private static FieldInfo GetField(Type type, string fieldName, BindingFlags flags)
	{
		FieldInfo field = type.GetField(fieldName, flags);
		Assert.That(field, Is.Not.Null, $"Missing field {type.Name}.{fieldName}");
		return field;
	}

	private static object InvokeNonPublic(Type type, object target, string methodName, params object[] args)
	{
		MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"Missing method {type.Name}.{methodName}");
		return method.Invoke(target, args);
	}
}
