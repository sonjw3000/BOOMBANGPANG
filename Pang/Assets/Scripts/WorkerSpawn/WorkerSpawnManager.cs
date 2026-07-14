using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct WorkerSpawnDefinition
{
	[FormerlySerializedAs("zoneType")]
	[SerializeField] private WorkerKind workerKind;
	[SerializeField] private PlaceableDefinition placeableDefinition;
	[SerializeField] private FacingDirection facingDirection;

	public WorkerKind WorkerKind => workerKind;
	public PlaceableDefinition PlaceableDefinition => placeableDefinition;
	public FacingDirection FacingDirection => facingDirection;
}

public class WorkerSpawnManager : MonoBehaviour
{
	[FormerlySerializedAs("zoneManager")]
	[SerializeField] private AreaManager areaManager;
	[SerializeField] private Transform spawnedWorkerRoot;
	[SerializeField] private int workerSpawnFloor = 0;
	[FormerlySerializedAs("randomSearchCountPerZone")]
	[SerializeField] private int randomSearchCountPerArea = 12;
	[SerializeField] private List<WorkerSpawnDefinition> spawnDefinitions = new();

	public Transform SpawnedWorkerRoot => spawnedWorkerRoot;

	private GridService GridService => GameContext.Instance.GridService;
	private AreaManager AreaManager
	{
		get
		{
			if (areaManager == null && GameContext.HasInstance)
				areaManager = GameContext.Instance.AreaMgr;

			return areaManager;
		}
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		if (spawnedWorkerRoot == null)
			Debug.LogError("No WorkerRoot!!");
	}
#endif

	public bool TrySpawnWorker(WorkerArchetype archetype, UnityEngine.Object requester, out AIWorker spawnedWorker)
	{
		spawnedWorker = null;
		
		if (archetype == null)
		{
			Debug.LogWarning("Worker spawn request failed: archetype is null");
			return false;
		}

		if (AreaManager == null)
		{
			Debug.LogWarning("Worker spawn request failed: AreaManager is not assigned");
			return false;
		}

		archetype.AbilityDefinition.EnsureIdentityInitialized();
		WorkerKind workerKind = archetype.AbilityDefinition.WorkerKind;
		if (TryGetSpawnDefinition(workerKind, out var spawnDefinition) == false)
		{
			Debug.LogWarning($"Worker spawn definition is missing for {workerKind}");
			return false;
		}

		if (TryGetSpawnPoint(spawnDefinition, out Area spawnArea, out int3 spawnPoint) == false)
		{
			//Debug.LogWarning($"No available spawn point for {archetype.name}.");
			return false;
		}

		if (TryInstallWorker(archetype, spawnDefinition, spawnPoint, out spawnedWorker) == false)
			return false;

		Debug.Log($"Spawned worker by {requester?.name ?? "UnknownRequester"} at {spawnPoint} in area {spawnArea.DisplayName}");
		return true;
	}

	private bool TryGetSpawnDefinition(WorkerKind workerKind, out WorkerSpawnDefinition result)
	{
		foreach (var definition in spawnDefinitions)
		{
			if (definition.WorkerKind == workerKind)
			{
				result = definition;
				return true;
			}
		}

		result = default;
		return false;
	}

	private bool TryGetSpawnPoint(in WorkerSpawnDefinition spawnDefinition, out Area spawnArea, out int3 spawnPoint)
	{
		spawnArea = null;
		spawnPoint = default;

		if (AreaManager.TryGetAreas(out var areas, workerSpawnFloor, AreaType.WorkerSpawn) == false)
			return false;

		int startIndex = UnityEngine.Random.Range(0, areas.Count);
		for (int i = 0; i < areas.Count; ++i)
		{
			Area area = areas[(startIndex + i) % areas.Count];
			if (TryFindSpawnPoint(area, spawnDefinition, out spawnPoint))
			{
				spawnArea = area;
				return true;
			}
		}

		return false;
	}

	private bool TryFindSpawnPoint(Area area, in WorkerSpawnDefinition spawnDefinition, out int3 spawnPoint)
	{
		for (int i = 0; i < Mathf.Max(1, randomSearchCountPerArea); ++i)
		{
			area.GetRandomPoint(out var candidatePoint);
			if (CanInstall(candidatePoint, spawnDefinition))
			{
				spawnPoint = candidatePoint;
				return true;
			}
		}

		for (int z = area.Bounds.yMin; z < area.Bounds.yMax; ++z)
		{
			for (int x = area.Bounds.xMin; x < area.Bounds.xMax; ++x)
			{
				var candidatePoint = new int3(x, area.Floor, z);
				if (CanInstall(candidatePoint, spawnDefinition))
				{
					spawnPoint = candidatePoint;
					return true;
				}
			}
		}

		spawnPoint = default;
		return false;
	}

	private bool CanInstall(in int3 candidatePoint, in WorkerSpawnDefinition spawnDefinition)
	{
		if (spawnDefinition.PlaceableDefinition == null)
			return false;

		List<int3> possible = new();
		List<int3> blocked = new();
		PlacementContext context = new(candidatePoint, spawnDefinition.FacingDirection, spawnDefinition.PlaceableDefinition, PlacementEvent.WorkerSpawn);
		return GridService.OnCheckInstallable(context, possible, blocked) && blocked.Count == 0;
	}

	private bool TryInstallWorker(WorkerArchetype archetype, in WorkerSpawnDefinition spawnDefinition, in int3 spawnPoint, out AIWorker spawnedWorker)
	{
		spawnedWorker = null;

		if (spawnDefinition.PlaceableDefinition == null || spawnDefinition.PlaceableDefinition.prefab == null)
		{
			Debug.LogWarning("Worker spawn install failed: placeable definition is missing");
			return false;
		}

		GameObject workerObject = Instantiate(spawnDefinition.PlaceableDefinition.prefab);
		if (spawnedWorkerRoot != null)
			workerObject.transform.SetParent(spawnedWorkerRoot, true);

		if (workerObject.TryGetComponent<AIWorker>(out spawnedWorker) == false)
		{
			Debug.LogWarning($"Spawn prefab {spawnDefinition.PlaceableDefinition.name} does not contain AIWorker");
			Destroy(workerObject);
			return false;
		}

		spawnedWorker.ApplyArchetype(archetype);

		PlacementContext context = new(spawnPoint, spawnDefinition.FacingDirection, spawnDefinition.PlaceableDefinition, PlacementEvent.WorkerSpawn, workerObject);
		if (GridService.OnInstall(context) == false)
		{
			Destroy(workerObject);
			spawnedWorker = null;
			return false;
		}

		int currentWeek = GameContext.Instance.GameTime != null
			? GameContext.Instance.GameTime.WeeksPassed
			: 0;
		spawnedWorker.MarkHired(currentWeek);

		return true;
	}
}
