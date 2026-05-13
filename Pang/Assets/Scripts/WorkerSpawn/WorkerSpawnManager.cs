using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct WorkerSpawnDefinition
{
	[SerializeField] private ZoneType zoneType;
	[SerializeField] private PlaceableDefinition placeableDefinition;
	[SerializeField] private FacingDirection facingDirection;

	public ZoneType ZoneType => zoneType;
	public PlaceableDefinition PlaceableDefinition => placeableDefinition;
	public FacingDirection FacingDirection => facingDirection;
}

public class WorkerSpawnManager : MonoBehaviour
{
	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private Transform spawnedWorkerRoot;
	[SerializeField] private int workerSpawnFloor = 0;
	[SerializeField] private int randomSearchCountPerZone = 12;
	[SerializeField] private List<WorkerSpawnDefinition> spawnDefinitions = new();

	public Transform SpawnedWorkerRoot => spawnedWorkerRoot;

	private GridService GridService => GameContext.Instance.GridService;
	private ZoneManager ZoneManager
	{
		get
		{
			if (zoneManager == null && GameContext.HasInstance)
				zoneManager = GameContext.Instance.ZoneMgr;

			return zoneManager;
		}
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		if (zoneManager == null)
			Debug.LogError("No ZoneManager!!");

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

		if (ZoneManager == null)
		{
			Debug.LogWarning("Worker spawn request failed: ZoneManager is not assigned");
			return false;
		}

		ZoneType zoneType = archetype.AbilityDefinition.workerType.ToSpawnZoneType();
		if (TryGetSpawnDefinition(zoneType, out var spawnDefinition) == false)
		{
			Debug.LogWarning($"Worker spawn definition is missing for {zoneType}");
			return false;
		}

		if (TryGetSpawnPoint(zoneType, spawnDefinition, out var spawnZone, out var spawnPoint) == false)
		{
			//Debug.LogWarning($"No available spawn point for {archetype.name} ({zoneType})");
			return false;
		}

		if (TryInstallWorker(archetype, spawnDefinition, spawnPoint, out spawnedWorker) == false)
			return false;

		Debug.Log($"Spawned worker by {requester?.name ?? "UnknownRequester"} at {spawnPoint} in zone {spawnZone.DisplayName}");
		return true;
	}

	private bool TryGetSpawnDefinition(ZoneType zoneType, out WorkerSpawnDefinition result)
	{
		foreach (var definition in spawnDefinitions)
		{
			if (definition.ZoneType == zoneType)
			{
				result = definition;
				return true;
			}
		}

		result = default;
		return false;
	}

	private bool TryGetSpawnPoint(ZoneType zoneType, in WorkerSpawnDefinition spawnDefinition, out ZoneArea spawnZone, out int3 spawnPoint)
	{
		spawnZone = null;
		spawnPoint = default;

		if (ZoneManager.TryGetZones(out var zones, workerSpawnFloor, zoneType) == false)
			return false;

		int startIndex = UnityEngine.Random.Range(0, zones.Count);
		for (int i = 0; i < zones.Count; ++i)
		{
			var zone = zones[(startIndex + i) % zones.Count];
			if (TryFindSpawnPoint(zone, spawnDefinition, out spawnPoint))
			{
				spawnZone = zone;
				return true;
			}
		}

		return false;
	}

	private bool TryFindSpawnPoint(ZoneArea zone, in WorkerSpawnDefinition spawnDefinition, out int3 spawnPoint)
	{
		for (int i = 0; i < Mathf.Max(1, randomSearchCountPerZone); ++i)
		{
			zone.GetRandomPoint(out var candidatePoint);
			if (CanInstall(candidatePoint, spawnDefinition))
			{
				spawnPoint = candidatePoint;
				return true;
			}
		}

		for (int z = zone.Bounds.yMin; z < zone.Bounds.yMax; ++z)
		{
			for (int x = zone.Bounds.xMin; x < zone.Bounds.xMax; ++x)
			{
				var candidatePoint = new int3(x, zone.Floor, z);
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

		return true;
	}
}
