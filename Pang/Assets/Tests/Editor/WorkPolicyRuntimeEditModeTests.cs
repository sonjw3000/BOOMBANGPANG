using System;
using System.Reflection;
using AYellowpaper.SerializedCollections;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WorkPolicyRuntimeEditModeTests
{
	private static readonly WorkerPolicyType[] policyTypes =
		(WorkerPolicyType[])Enum.GetValues(typeof(WorkerPolicyType));
	private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
	private GameObject owner;
	private WorkPolicyService service;
	private WorkPolicy policy;
	private HumanWorker worker;

	[SetUp]
	public void SetUp()
	{
		owner = new GameObject("Work Policy Runtime Test");
		owner.SetActive(false);
		service = owner.AddComponent<WorkPolicyService>();
		worker = owner.AddComponent<HumanWorker>();
		policy = ScriptableObject.CreateInstance<WorkPolicy>();
		policy.moveSpeed = new SerializedDictionary<WorkerPolicyType, float>();
		foreach (WorkerPolicyType type in policyTypes) policy.moveSpeed[type] = 10;
		SetField("workPolicy", policy);
	}

	[TearDown]
	public void TearDown()
	{
		Object.DestroyImmediate(owner);
		Object.DestroyImmediate(policy);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void UninitializedQueries_ReturnDefaultsWithoutCreatingEntries(bool nullDictionaries)
	{
		var move = nullDictionaries ? null : new SerializedDictionary<WorkerPolicyType, float>();
		var work = nullDictionaries ? null : new SerializedDictionary<WorkerPolicyType, float>();
		SetField("moveSpeedMultipliers", move);
		SetField("workSpeedMultipliers", work);
		foreach (WorkerPolicyType type in policyTypes)
		{
			Assert.That(service.GetMoveSpeedMultiplier(type), Is.EqualTo(1));
			Assert.That(service.GetWorkSpeedMultiplier(type), Is.EqualTo(1));
		}
		Assert.That(GetField("moveSpeedMultipliers"), Is.SameAs(move));
		Assert.That(GetField("workSpeedMultipliers"), Is.SameAs(work));
		if (!nullDictionaries)
		{
			Assert.That(move, Is.Empty);
			Assert.That(work, Is.Empty);
		}
	}

	[Test]
	public void Settings_ClampAndImmediatelyAffectQueriesAndMovementSpeed()
	{
		foreach (WorkerPolicyType type in policyTypes)
		{
			service.SetMoveSpeedMultiplier(type, 10);
			service.SetWorkSpeedMultiplier(type, 0);
			Assert.That(service.GetMoveSpeedMultiplier(type), Is.EqualTo(2));
			Assert.That(service.GetWorkSpeedMultiplier(type), Is.EqualTo(0.5f));
		}
		service.SetMoveSpeedMultiplier(worker.WorkerPolicyType, 1.5f);
		Assert.That(service.GetMoveSpeed(worker), Is.EqualTo(15 * worker.GetMoveSpeedMultiplier()).Within(0.0001f));
	}

	[Test]
	public void CaptureRestoreAndLegacyMissingValues_PreserveSettingsAndDefaults()
	{
		service.SetMoveSpeedMultiplier(policyTypes[0], 1.4f);
		service.SetWorkSpeedMultiplier(policyTypes[1], 0.7f);
		WorkPolicyRuntimeSaveData saved = service.CaptureState();
		Assert.That(saved.MoveSpeedMultipliers.Count, Is.EqualTo(policyTypes.Length));
		Assert.That(saved.WorkSpeedMultipliers.Count, Is.EqualTo(policyTypes.Length));
		service.ResetRuntimeState();
		service.RestoreState(saved);
		Assert.That(service.GetMoveSpeedMultiplier(policyTypes[0]), Is.EqualTo(1.4f));
		Assert.That(service.GetWorkSpeedMultiplier(policyTypes[1]), Is.EqualTo(0.7f));
		service.RestoreState(new WorkPolicyRuntimeSaveData
		{
			MoveSpeedMultipliers = new()
			{
				new WorkerPolicyTypeFloatSaveData { WorkerPolicyType = policyTypes[0], Value = 10 },
				new WorkerPolicyTypeFloatSaveData { WorkerPolicyType = policyTypes[1], Value = -1 },
			},
			WorkSpeedMultipliers = null,
		});
		Assert.That(service.GetMoveSpeedMultiplier(policyTypes[0]), Is.EqualTo(2));
		Assert.That(service.GetMoveSpeedMultiplier(policyTypes[1]), Is.EqualTo(1));
		foreach (WorkerPolicyType type in policyTypes)
			Assert.That(service.GetWorkSpeedMultiplier(type), Is.EqualTo(1));
		service.RestoreState(null);
		foreach (WorkerPolicyType type in policyTypes)
			Assert.That(service.GetMoveSpeedMultiplier(type), Is.EqualTo(1));
	}

	[Test]
	public void InitializationAndInspectorValidation_FillMissingEntriesWithoutOverwritingValues()
	{
		var move = new SerializedDictionary<WorkerPolicyType, float> { [policyTypes[0]] = 1.7f };
		SetField("moveSpeedMultipliers", move);
		SetField("workSpeedMultipliers", null);
		typeof(WorkPolicyService).GetMethod("Awake", PrivateInstance).Invoke(service, null);
		Assert.That(move.Count, Is.EqualTo(policyTypes.Length));
		Assert.That(service.GetMoveSpeedMultiplier(policyTypes[0]), Is.EqualTo(1.7f));
		move.Remove(policyTypes[1]);
		typeof(WorkPolicyService).GetMethod("OnValidate", PrivateInstance).Invoke(service, null);
		Assert.That(move[policyTypes[1]], Is.EqualTo(1));
		Assert.That(move[policyTypes[0]], Is.EqualTo(1.7f));
	}

	[Test]
	public void WarmMovementAndMultiplierQueries_AllocateNoManagedMemory()
	{
		service.ResetRuntimeState();
		ReadSpeeds(10);
		long before = GC.GetAllocatedBytesForCurrentThread();
		float result = ReadSpeeds(1000);
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.That(result, Is.GreaterThan(0));
		Assert.That(allocated, Is.Zero);
	}

	private float ReadSpeeds(int count)
	{
		float result = 0;
		for (int i = 0; i < count; ++i)
		{
			result += service.GetMoveSpeed(worker);
			foreach (WorkerPolicyType type in policyTypes)
			{
				result += service.GetMoveSpeedMultiplier(type);
				result += service.GetWorkSpeedMultiplier(type);
			}
		}
		return result;
	}

	private void SetField(string name, object value) =>
		typeof(WorkPolicyService).GetField(name, PrivateInstance).SetValue(service, value);
	private object GetField(string name) =>
		typeof(WorkPolicyService).GetField(name, PrivateInstance).GetValue(service);
}
