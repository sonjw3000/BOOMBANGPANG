using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.AI.BT;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public sealed class TaskMetricsEditModeTests
{
	private readonly List<GameObject> createdObjects = new();
	private GameContext previousContext;
	private TaskManager taskManager;
	private MetricsService metrics;
	private WorkerManager workerManager;
	private ProcessStatsCollector processStats;
	private RestFacilityService restFacilityService;
	private int nextWorkerPosition;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)GetPrivateStaticField(typeof(GameContext), "instance");
		SetPrivateStaticField(typeof(GameContext), "instance", null);

		taskManager = CreateComponent<TaskManager>("Task Metrics Test Task Manager");
		metrics = CreateComponent<MetricsService>("Task Metrics Test Metrics Service");
		workerManager = CreateComponent<WorkerManager>("Task Metrics Test Worker Manager");
		processStats = CreateComponent<ProcessStatsCollector>("Task Metrics Test Process Stats");
		restFacilityService = CreateComponent<RestFacilityService>("Task Metrics Test Rest Facility Service");

		InvokeNonPublic(typeof(TaskManager), taskManager, "Awake");
		InvokeNonPublic(typeof(WorkerManager), workerManager, "Awake");
		InvokeNonPublic(typeof(ProcessStatsCollector), processStats, "Awake");

		GameObject contextObject = CreateGameObject("Task Metrics Test Context", active: false);
		GameContext context = contextObject.AddComponent<GameContext>();
		SetPrivateField(typeof(GameContext), context, "taskManager", taskManager);
		SetPrivateField(typeof(GameContext), context, "metrics", metrics);
		SetPrivateField(typeof(GameContext), context, "workerManager", workerManager);
		SetPrivateField(typeof(GameContext), context, "processStats", processStats);
		SetPrivateField(typeof(GameContext), context, "restFacilityService", restFacilityService);
		SetPrivateStaticField(typeof(GameContext), "instance", context);
		nextWorkerPosition = 10;
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
	public void GetTaskCountSnapshot_SeparatesLifecycleStatesAndBlockedSubset()
	{
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.Picking));
		taskManager.AddRestoredReturnedTask(new TestWorkerTask(WorkerTask.TaskType.Picking));

		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Picking),
			WorkerStatusAction.Working);
		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Picking),
			WorkerStatusAction.WaitingForItems);
		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Picking),
			WorkerStatusAction.TrafficBlock);

		TaskCountSnapshot snapshot = metrics.GetTaskCountSnapshot(WorkerTask.TaskType.Picking);

		Assert.That(snapshot.Ready, Is.EqualTo(1));
		Assert.That(snapshot.Returned, Is.EqualTo(1));
		Assert.That(snapshot.Active, Is.EqualTo(3));
		Assert.That(snapshot.Blocked, Is.EqualTo(2));
		Assert.That(snapshot.Waiting, Is.EqualTo(2));
		Assert.That(snapshot.Total, Is.EqualTo(5));
	}

	[Test]
	public void GetCapsuleRelocationTaskCountSnapshot_UsesConcreteTaskClass()
	{
		taskManager.EnqueueTask(CreateCapsuleTask(WorkerTask.TaskType.OB));
		taskManager.AddRestoredReturnedTask(CreateCapsuleTask(WorkerTask.TaskType.IB));
		AddActiveTask(
			CreateCapsuleTask(WorkerTask.TaskType.CargoTransfer),
			WorkerStatusAction.WaitingForTargetBuilding);
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.OB));

		TaskCountSnapshot snapshot = metrics.GetCapsuleRelocationTaskCountSnapshot();

		Assert.That(snapshot.Ready, Is.EqualTo(1));
		Assert.That(snapshot.Returned, Is.EqualTo(1));
		Assert.That(snapshot.Active, Is.EqualTo(1));
		Assert.That(snapshot.Blocked, Is.EqualTo(1));
		Assert.That(snapshot.Total, Is.EqualTo(3));
	}

	[Test]
	public void TaskStateChanged_RaisesAfterSuccessfulLifecycleChangesOnly()
	{
		int eventCount = 0;
		taskManager.OnTaskStateChanged += () => ++eventCount;

		TestWorkerTask readyTask = new(WorkerTask.TaskType.PackingInput);
		taskManager.EnqueueTask(readyTask);
		Assert.That(eventCount, Is.EqualTo(1));

		taskManager.EnqueueTask(null);
		Assert.That(taskManager.InvalidateTask(readyTask), Is.True);
		Assert.That(taskManager.InvalidateTask(readyTask), Is.False);
		Assert.That(eventCount, Is.EqualTo(2));

		TestWorkerTask returnedTask = new(WorkerTask.TaskType.PackingInput);
		taskManager.AddRestoredReturnedTask(returnedTask);
		Assert.That(eventCount, Is.EqualTo(3));

		TestWorkerTask activeTask = new(WorkerTask.TaskType.PackingInput);
		HumanWorker activeWorker = AddActiveTask(activeTask, WorkerStatusAction.Working);
		Assert.That(eventCount, Is.EqualTo(4));
		Assert.That(taskManager.ReturnTask(activeWorker), Is.True);
		Assert.That(eventCount, Is.EqualTo(5));

		TestWorkerTask completingTask = new(WorkerTask.TaskType.PackingInput);
		AddActiveTask(completingTask, WorkerStatusAction.Working);
		Assert.That(eventCount, Is.EqualTo(6));
		taskManager.CompleteTask(completingTask);
		taskManager.CompleteTask(completingTask);
		Assert.That(eventCount, Is.EqualTo(7));

		taskManager.ResetRuntimeState();
		Assert.That(eventCount, Is.EqualTo(8));
	}

	[Test]
	public void TaskStateChanged_BatchesRestoreMutationsIntoFinalSnapshot()
	{
		int eventCount = 0;
		TaskCountSnapshot observedSnapshot = default;
		taskManager.OnTaskStateChanged += () =>
		{
			++eventCount;
			observedSnapshot = metrics.GetTaskCountSnapshot(WorkerTask.TaskType.Storing);
		};

		InvokeNonPublic(typeof(TaskManager), taskManager, "BeginTaskStateChangeBatch");
		taskManager.ResetRuntimeState();
		taskManager.EnqueueTask(new TestWorkerTask(WorkerTask.TaskType.Storing));
		taskManager.AddRestoredReturnedTask(new TestWorkerTask(WorkerTask.TaskType.Storing));
		AddActiveTask(
			new TestWorkerTask(WorkerTask.TaskType.Storing),
			WorkerStatusAction.Working);

		Assert.That(eventCount, Is.Zero);
		InvokeNonPublic(typeof(TaskManager), taskManager, "EndTaskStateChangeBatch");

		Assert.That(eventCount, Is.EqualTo(1));
		Assert.That(observedSnapshot.Ready, Is.EqualTo(1));
		Assert.That(observedSnapshot.Returned, Is.EqualTo(1));
		Assert.That(observedSnapshot.Active, Is.EqualTo(1));
	}

	private HumanWorker AddActiveTask(WorkerTask task, WorkerStatusAction action)
	{
		HumanWorker worker = CreateWorker();
		Assert.That(worker.SetTask(task), Is.True);
		worker.SetWorkerAction(action);
		taskManager.AddRestoredInProgressTask(task);
		return worker;
	}

	private static CapsuleRelocationTask CreateCapsuleTask(WorkerTask.TaskType taskType)
	{
		return new CapsuleRelocationTask(
			taskType,
			null,
			null,
			0,
			CapsuleRelocationReason.StateMismatch);
	}

	private HumanWorker CreateWorker()
	{
		GameObject workerObject = CreateGameObject("Task Metrics Test Worker");
		GameObject slotObject = new("SlotRoot");
		slotObject.transform.SetParent(workerObject.transform, false);
		HumanWorker worker = workerObject.AddComponent<HumanWorker>();
		workerObject.AddComponent<CarryBoxAbility>();
		worker.OnPositionSet(
			new int3(nextWorkerPosition++, 0, nextWorkerPosition++),
			FacingDirection.North);
		return worker;
	}

	private T CreateComponent<T>(string objectName) where T : Component
	{
		return CreateGameObject(objectName).AddComponent<T>();
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

	private sealed class TestWorkerTask : WorkerTask
	{
		public TestWorkerTask(TaskType type) : base(type) { }

		public override string GetStatusSummary() => Type.ToString();

		protected override IBaseNode BuildWorkNode()
		{
			return new ActionNode(CompleteImmediately);
		}

		public override bool CheckTaskEnd() => false;

#if UNITY_EDITOR
		public override string ShowStatus() => Type.ToString();
#endif

		private static IBaseNode.NodeState CompleteImmediately(in BTContext context)
		{
			return IBaseNode.NodeState.Success;
		}
	}
}
