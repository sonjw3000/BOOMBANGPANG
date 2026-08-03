using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public enum ChargingType
{
	None = 0,
	Standard = 1,
}

public interface IWorkerInteractionReservation
{
	bool TryGetReservedInteractionPoint(
		AIWorker worker,
		InteractionKind interactionKind,
		out int3 point);
}

public interface IRecoveryFacility :
	IFacility,
	IInteractionPoint,
	IWorkerInteractionReservation
{
	InteractionKind RecoveryInteractionKind { get; }
	int Capacity { get; }
	int ReservedCount { get; }
	int ActiveUserCount { get; }
	float BaseRecoveryPerSecond { get; }

	bool CanServe(AIWorker worker);
	bool TryGetAvailableSlot(AIWorker worker, in int3 from, out int3 point, out int score);
	bool TryReserveSlot(AIWorker worker, in int3 from, out int3 point);
	bool IsReservedBy(AIWorker worker);
	bool TryBeginUse(AIWorker worker);
	void ReleaseWorker(AIWorker worker);
	float GetEffectiveRecoveryPerSecond(AIWorker worker);
}

public abstract class RecoveryFacilityBase :
	MonoBehaviour,
	IRecoveryFacility,
	IGridPlacementEffect,
	IFacilityUserRemovalGuard
{
	[SerializeField] private uint facilityRulePresetId;
	[SerializeField, Min(1)] private int capacity = 1;
	[SerializeField, Min(0.0f)] private float recoveryPerSecond = 1.0f;
	[SerializeField] private HealthState health = new();
	[SerializeField, Range(0.0f, 100.0f)] private float fireIntensity;

	private readonly List<InteractionPoint> interactionPoints = new();
	private readonly Dictionary<InteractionKind, List<int3>> interactionPointMap = new();
	private readonly Dictionary<int3, AIWorker> reservedWorkersByPoint = new();
	private readonly HashSet<AIWorker> activeUsers = new();

	private int3 gridPosition;
	private FacingDirection facingDirection;

	public abstract InteractionKind RecoveryInteractionKind { get; }
	public abstract WorkerStatusTarget BuildingTarget { get; }
	public abstract int PowerConsumption { get; }

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public int Capacity => Mathf.Max(1, capacity);
	public int ReservedCount => reservedWorkersByPoint.Count;
	public int ActiveUserCount => activeUsers.Count;
	public float BaseRecoveryPerSecond => Mathf.Max(0.0f, recoveryPerSecond);
	public IReadOnlyList<InteractionPoint> InteractionPoints => interactionPoints;
	public float Health => health.Health;
	public float MaxHealth => health.MaxHealth;
	public float FireIntensity => fireIntensity;

	protected bool IsOperational =>
		enabled &&
		GameContext.HasInstance &&
		GameContext.Instance.FacilityMgr != null &&
		GameContext.Instance.FacilityMgr.IsInvalidating(this) == false;

	public abstract bool CanServe(AIWorker worker);

	public float ApplyDamage(float amount) => health.ApplyDamage(amount);
	public void RestoreHealth(float value) => health.RestoreHealth(value);
	public void SetFireIntensity(float intensity) => fireIntensity = Mathf.Clamp(intensity, 0.0f, 100.0f);
	public void SetFacilityRulePresetId(uint presetId) => facilityRulePresetId = presetId;

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		gridPosition = position;
		facingDirection = direction;
	}

	public void OnDestroyedBy(in DestroyContext context)
	{
		ReleaseAll();
	}

	public void OnRemoved()
	{
		ReleaseAll();
	}

	private void OnDestroy()
	{
		ReleaseAll();
	}

	public void ClearInteractionPoints()
	{
		ReleaseAll();
		interactionPoints.Clear();
		interactionPointMap.Clear();
	}

	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point)
	{
		interactionPoints.Add(new InteractionPoint(interactionKind, point));

		foreach (InteractionKind value in Enum.GetValues(typeof(InteractionKind)))
		{
			if (value == InteractionKind.None || interactionKind.HasFlag(value) == false)
				continue;

			if (interactionPointMap.TryGetValue(value, out List<int3> points) == false)
			{
				points = new List<int3>();
				interactionPointMap[value] = points;
			}

			points.Add(point);
		}
	}

	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from)
	{
		if (TryGetClosestPoint(interactionKind, from, out int3 point, out _))
			return point;

		return default;
	}

	public bool IsInteractionAvailable(InteractionKind interactionKind)
	{
		return IsOperational &&
			(interactionKind & RecoveryInteractionKind) != 0 &&
			reservedWorkersByPoint.Count < Mathf.Min(Capacity, GetRecoveryPointCount());
	}

	public bool TryGetAvailableSlot(AIWorker worker, in int3 from, out int3 point, out int score)
	{
		point = default;
		score = int.MaxValue;

		if (worker == null || CanServe(worker) == false || IsOperational == false)
			return false;

		if (TryGetReservedInteractionPoint(worker, RecoveryInteractionKind, out point))
		{
			score = ManhattanDistance(from, point);
			return true;
		}

		if (reservedWorkersByPoint.Count >= Capacity)
			return false;

		GridService gridService = GameContext.Instance.GridService;
		for (int i = 0; i < interactionPoints.Count; ++i)
		{
			InteractionPoint interactionPoint = interactionPoints[i];
			if ((interactionPoint.InteractionKind & RecoveryInteractionKind) == 0 ||
				reservedWorkersByPoint.ContainsKey(interactionPoint.Point))
			{
				continue;
			}

			GridCell cell = gridService?.GetCell(interactionPoint.Point);
			if (cell == null ||
				(cell.IsBlocked && interactionPoint.Point.Equals(worker.GridPosition) == false) ||
				(cell.ReservedRoute != null && cell.ReservedRoute != worker.RouteFinder))
			{
				continue;
			}

			int candidateScore = ManhattanDistance(from, interactionPoint.Point);
			if (candidateScore >= score)
				continue;

			point = interactionPoint.Point;
			score = candidateScore;
		}

		return score != int.MaxValue;
	}

	public bool TryReserveSlot(AIWorker worker, in int3 from, out int3 point)
	{
		if (TryGetAvailableSlot(worker, from, out point, out _) == false)
			return false;

		if (TryGetReservedInteractionPoint(worker, RecoveryInteractionKind, out _))
			return true;

		reservedWorkersByPoint[point] = worker;
		return true;
	}

	public bool TryGetReservedInteractionPoint(
		AIWorker worker,
		InteractionKind interactionKind,
		out int3 point)
	{
		point = default;
		if (worker == null || (interactionKind & RecoveryInteractionKind) == 0)
			return false;

		foreach (var pair in reservedWorkersByPoint)
		{
			if (pair.Value != worker)
				continue;

			point = pair.Key;
			return true;
		}

		return false;
	}

	public bool IsReservedBy(AIWorker worker)
	{
		return TryGetReservedInteractionPoint(worker, RecoveryInteractionKind, out _);
	}

	public bool TryBeginUse(AIWorker worker)
	{
		if (worker == null ||
			IsOperational == false ||
			TryGetReservedInteractionPoint(worker, RecoveryInteractionKind, out int3 point) == false ||
			point.Equals(worker.GridPosition) == false)
		{
			return false;
		}

		if (activeUsers.Add(worker))
			OnActiveUsersChanged();

		return true;
	}

	public void ReleaseWorker(AIWorker worker)
	{
		if (worker == null)
			return;

		bool activeChanged = activeUsers.Remove(worker);
		int3 reservedPoint = default;
		bool hasReservation = false;
		foreach (var pair in reservedWorkersByPoint)
		{
			if (pair.Value != worker)
				continue;

			reservedPoint = pair.Key;
			hasReservation = true;
			break;
		}

		if (hasReservation)
			reservedWorkersByPoint.Remove(reservedPoint);

		if (activeChanged)
			OnActiveUsersChanged();
	}

	public float GetEffectiveRecoveryPerSecond(AIWorker worker)
	{
		if (worker == null || activeUsers.Contains(worker) == false)
			return 0.0f;

		return BaseRecoveryPerSecond * Mathf.Max(0.0f, GetOperatingEfficiency());
	}

	public bool CanUserRemove(out FacilityRemovalFailure failure)
	{
		if (activeUsers.Count > 0)
		{
			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.WorkerIsUsing,
				"Wait for workers using this facility to finish.");
			return false;
		}

		if (reservedWorkersByPoint.Count > 0)
		{
			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.HasReservation,
				"Wait for workers travelling to this facility to finish.");
			return false;
		}

		failure = FacilityRemovalFailure.None;
		return true;
	}

	protected virtual float GetOperatingEfficiency() => 1.0f;
	protected virtual void OnActiveUsersChanged() { }

	private void ReleaseAll()
	{
		bool activeChanged = activeUsers.Count > 0;
		activeUsers.Clear();
		reservedWorkersByPoint.Clear();
		if (activeChanged)
			OnActiveUsersChanged();
	}

	private int GetRecoveryPointCount()
	{
		return interactionPointMap.TryGetValue(RecoveryInteractionKind, out List<int3> points)
			? points.Count
			: 0;
	}

	private bool TryGetClosestPoint(
		InteractionKind interactionKind,
		in int3 from,
		out int3 point,
		out int score)
	{
		point = default;
		score = int.MaxValue;
		if (interactionPointMap.TryGetValue(interactionKind, out List<int3> points) == false)
			return false;

		for (int i = 0; i < points.Count; ++i)
		{
			int candidateScore = ManhattanDistance(from, points[i]);
			if (candidateScore >= score)
				continue;

			point = points[i];
			score = candidateScore;
		}

		return score != int.MaxValue;
	}

	private static int ManhattanDistance(in int3 from, in int3 to)
	{
		return math.abs(from.x - to.x) + math.abs(from.y - to.y) + math.abs(from.z - to.z);
	}
}
