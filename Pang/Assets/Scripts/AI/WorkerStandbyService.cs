using Unity.Mathematics;
using UnityEngine;

public enum StandbyPointResult
{
	Success,
	NotRequired,
	NoZone,
	NoAvailablePoint,
}

public class WorkerStandbyService : MonoBehaviour
{
	private ZoneManager ZoneManager => GameContext.Instance.ZoneMgr;
	private GridService GridService => GameContext.Instance.GridService;

	public StandbyPointResult TryFindStandbyPoint(AIWorker worker, out int3 point)
	{
		point = default;

		if (worker == null)
			return StandbyPointResult.NotRequired;

		if (TryGetStandbyZoneType(worker.TaskType, out ZoneType zoneType) == false)
			return StandbyPointResult.NotRequired;

		if (IsInStandbyZone(worker, zoneType))
			return StandbyPointResult.NotRequired;

		if (ZoneManager == null || ZoneManager.TryGetZones(out var zones, worker.GridPosition.y, zoneType) == false)
			return StandbyPointResult.NoZone;

		int bestDistance = int.MaxValue;
		bool found = false;

		foreach (var zone in zones)
		{
			if (zone == null)
				continue;

			RectInt bounds = zone.Bounds;
			for (int z = bounds.yMin; z < bounds.yMax; ++z)
			{
				for (int x = bounds.xMin; x < bounds.xMax; ++x)
				{
					int3 candidate = new(x, zone.Floor, z);
					if (CanUseAsStandbyPoint(candidate) == false)
						continue;

					int distance = math.abs(candidate.x - worker.GridPosition.x) + math.abs(candidate.z - worker.GridPosition.z);
					if (distance >= bestDistance)
						continue;

					point = candidate;
					bestDistance = distance;
					found = true;
				}
			}
		}

		return found ? StandbyPointResult.Success : StandbyPointResult.NoAvailablePoint;
	}

	public bool TryGetStandbyZoneType(WorkerTask.TaskType taskType, out ZoneType zoneType)
	{
		switch (taskType)
		{
			case WorkerTask.TaskType.IB:
				zoneType = ZoneType.InboundStandby;
				return true;

			case WorkerTask.TaskType.OB:
			case WorkerTask.TaskType.CargoTransfer:
				zoneType = ZoneType.OutboundStandby;
				return true;

			case WorkerTask.TaskType.Picking:
			case WorkerTask.TaskType.Storing:
			case WorkerTask.TaskType.Water:
				zoneType = ZoneType.StorageStandby;
				return true;

			case WorkerTask.TaskType.Unloading:
			case WorkerTask.TaskType.Labeling:
				zoneType = ZoneType.InboundStandby;
				return true;

			case WorkerTask.TaskType.Loading:
				zoneType = ZoneType.OutboundStandby;
				return true;

			default:
				zoneType = default;
				return false;
		}
	}

	public bool IsInStandbyZone(AIWorker worker)
	{
		if (worker == null || TryGetStandbyZoneType(worker.TaskType, out ZoneType zoneType) == false)
			return false;

		return IsInStandbyZone(worker, zoneType);
	}

	private bool IsInStandbyZone(AIWorker worker, ZoneType zoneType)
	{
		if (ZoneManager == null || ZoneManager.TryGetZones(out var zones, worker.GridPosition.y, zoneType) == false)
			return false;

		foreach (var zone in zones)
		{
			if (zone != null && zone.Contains(worker.GridPosition))
				return true;
		}

		return false;
	}

	private bool CanUseAsStandbyPoint(in int3 candidate)
	{
		var cell = GridService?.GetCell(candidate);
		return cell != null && cell.CanPlaceObject;
	}
}
