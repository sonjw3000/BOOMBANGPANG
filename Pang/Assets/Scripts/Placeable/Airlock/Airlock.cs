using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public enum AirlockDirection
{
	OutsideToInside,
	InsideToOutside,
}

public enum AirlockState
{
	Idle,
	Reserved,
	Occupied,
	Blocked,
}

public sealed class Airlock : ItemInteraction, IFacilityUserRemovalGuard
{
	[SerializeField] private float entryDelaySeconds = 3.0f;
	[SerializeField] private float transitMoveSpeed = 2.5f;
	[SerializeField] private float exitRetrySeconds = 0.1f;

	private Coroutine entryRoutine;
	private AIWorker reservedWorker;
	private AirlockDirection reservedDirection;
	private AirlockState state = AirlockState.Idle;

	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.Airlock;
	public float EntryDelaySeconds => entryDelaySeconds;
	public AIWorker ReservedWorker => reservedWorker;
	public AirlockDirection ReservedDirection => reservedDirection;
	public AirlockState State => state;
	public bool IsAvailable => state == AirlockState.Idle && reservedWorker == null;

	public bool TryReserve(AIWorker worker, AirlockDirection direction)
	{
		if (worker == null || IsAvailable == false)
			return false;

		reservedWorker = worker;
		reservedDirection = direction;
		state = AirlockState.Reserved;
		return true;
	}

	public bool TryBeginEntry(AIWorker worker)
	{
		if (worker == null || worker != reservedWorker || state != AirlockState.Reserved)
			return false;

		if (TryResolveTransitPoints(direction: reservedDirection, out _, out _, out _) == false)
			return false;

		if (entryRoutine != null)
			StopCoroutine(entryRoutine);

		entryRoutine = StartCoroutine(EntryRoutine(worker));
		return true;
	}

	public bool HasCompletedTransit(AIWorker worker, AirlockDirection direction)
	{
		if (worker == null)
			return false;

		return TryResolveTransitPoints(direction, out _, out _, out int3 exitPoint) &&
			worker.GridPosition.Equals(exitPoint);
	}

	public void Release(AIWorker worker)
	{
		if (worker != null && reservedWorker != null && worker != reservedWorker)
			return;

		RestoreInterruptedWorker();

		if (entryRoutine != null)
		{
			StopCoroutine(entryRoutine);
			entryRoutine = null;
		}

		reservedWorker = null;
		state = AirlockState.Idle;
	}

	public override void OnPositionSet(in int3 pos, FacingDirection direction)
	{
		position = pos;
		facingDirection = direction;
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{
		Release(null);
	}

	public override void OnRemoved()
	{
		Release(null);
	}

	public bool CanUserRemove(out FacilityRemovalFailure failure)
	{
		if (state != AirlockState.Idle || reservedWorker != null)
		{
			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.InTransit,
				"Wait for the airlock transit to finish before removing it.");
			return false;
		}

		failure = FacilityRemovalFailure.None;
		return true;
	}

	private IEnumerator EntryRoutine(AIWorker worker)
	{
		if (TryResolveTransitPoints(reservedDirection, out int3 entryPoint, out int3 chamberPoint, out int3 exitPoint) == false)
		{
			Release(worker);
			yield break;
		}

		worker.RouteFinder?.PauseForExternalTransit();
		worker.SetWorkerTarget(WorkerStatusTarget.Airlock);
		worker.SetWorkerAction(WorkerStatusAction.UsingAirlock);
		worker.enabled = false;
		state = AirlockState.Occupied;

		yield return MoveWorkerTo(worker, ToWorld(entryPoint));
		if (TryResolveFacing(entryPoint, chamberPoint, out FacingDirection entryFacing) && worker.Direction != entryFacing)
		{
			yield return RotateWorkerTo(worker, entryFacing);
		}
		yield return MoveWorkerTo(worker, ToWorld(chamberPoint));
		yield return new WaitForSeconds(entryDelaySeconds);

		while (reservedWorker == worker && GridService != null && GridService.CanRelocateWorkerForTransit(worker, exitPoint) == false)
		{
			state = AirlockState.Blocked;
			yield return new WaitForSeconds(exitRetrySeconds);
		}

		if (reservedWorker != worker)
		{
			entryRoutine = null;
			yield break;
		}

		state = AirlockState.Occupied;
		yield return MoveWorkerTo(worker, ToWorld(exitPoint));

		if (GridService == null || GridService.TryRelocateWorkerForTransit(worker, exitPoint, ResolveExitFacing(entryPoint, exitPoint)) == false)
		{
			Release(worker);
			entryRoutine = null;
			yield break;
		}

		worker.enabled = true;
		Release(worker);

		entryRoutine = null;
	}

	private IEnumerator MoveWorkerTo(AIWorker worker, Vector3 destination)
	{
		if (worker == null)
			yield break;

		while (Vector3.Distance(worker.transform.position, destination) > 0.01f)
		{
			worker.transform.position = Vector3.MoveTowards(
				worker.transform.position,
				destination,
				transitMoveSpeed * Time.deltaTime);
			yield return null;
		}

		worker.transform.position = destination;
	}

	private IEnumerator RotateWorkerTo(AIWorker worker, FacingDirection direction)
	{
		if (worker == null)
			yield break;

		Vector3 forward = direction.ForwardDirection().ToVector3().normalized;
		if (forward == Vector3.zero)
			yield break;

		Quaternion targetRotation = Quaternion.LookRotation(forward);
		float rotationSpeed = worker.RouteFinder != null ? worker.RouteFinder.GetRotationSpeed() : transitMoveSpeed;

		while (true)
		{
			worker.transform.rotation = Quaternion.Slerp(
				worker.transform.rotation,
				targetRotation,
				Time.deltaTime * rotationSpeed);

			float dotProduct = math.dot(worker.transform.forward, forward);
			if (dotProduct >= 0.9999f)
				break;

			yield return null;
		}

		worker.transform.rotation = targetRotation;
		worker.SetDirection(direction);
	}

	private void RestoreInterruptedWorker()
	{
		if (reservedWorker == null)
			return;

		reservedWorker.transform.position = ToWorld(reservedWorker.GridPosition);
		reservedWorker.transform.rotation = Quaternion.Euler(0f, FacingToYaw(reservedWorker.Direction), 0f);
		reservedWorker.enabled = true;
	}

	private bool TryResolveTransitPoints(
		AirlockDirection direction,
		out int3 entryPoint,
		out int3 chamberPoint,
		out int3 exitPoint)
	{
		entryPoint = default;
		chamberPoint = default;
		exitPoint = default;

		if (TryResolveIndoorOutdoorPoints(out int3 indoorPoint, out int3 outdoorPoint) == false)
			return false;

		if (direction == AirlockDirection.OutsideToInside)
		{
			entryPoint = outdoorPoint;
			exitPoint = indoorPoint;
		}
		else
		{
			entryPoint = indoorPoint;
			exitPoint = outdoorPoint;
		}

		chamberPoint = new int3(
			(entryPoint.x + exitPoint.x) / 2,
			position.y,
			(entryPoint.z + exitPoint.z) / 2);

		return true;
	}

	private bool TryResolveIndoorOutdoorPoints(out int3 indoorPoint, out int3 outdoorPoint)
	{
		indoorPoint = default;
		outdoorPoint = default;

		uint buildingId = ResolveOwningBuildingId();
		bool hasIndoor = false;
		bool hasOutdoor = false;

		for (int i = 0; i < interactionPoints.Count; ++i)
		{
			InteractionPoint interactionPoint = interactionPoints[i];
			if ((interactionPoint.InteractionKind & InteractionKind.Enter) == 0)
				continue;

			GridCell cell = GridService?.GetCell(interactionPoint.Point);
			if (cell == null)
				continue;

			if (cell.BuildingId == 0 && hasOutdoor == false)
			{
				outdoorPoint = interactionPoint.Point;
				hasOutdoor = true;
				continue;
			}

			if (buildingId != 0 && cell.BuildingId == buildingId && hasIndoor == false)
			{
				indoorPoint = interactionPoint.Point;
				hasIndoor = true;
			}
		}

		return hasIndoor && hasOutdoor;
	}

	private uint ResolveOwningBuildingId()
	{
		GridCell centerCell = GridService?.GetCell(position);
		if (centerCell != null && centerCell.BuildingId != 0)
			return centerCell.BuildingId;

		for (int i = 0; i < interactionPoints.Count; ++i)
		{
			GridCell cell = GridService?.GetCell(interactionPoints[i].Point);
			if (cell != null && cell.BuildingId != 0)
				return cell.BuildingId;
		}

		return 0;
	}

	private static FacingDirection ResolveExitFacing(in int3 entryPoint, in int3 exitPoint)
	{
		int3 delta = exitPoint - entryPoint;
		if (math.abs(delta.x) >= math.abs(delta.z))
			return delta.x >= 0 ? FacingDirection.East : FacingDirection.West;

		return delta.z >= 0 ? FacingDirection.North : FacingDirection.South;
	}

	private static bool TryResolveFacing(in int3 fromPoint, in int3 toPoint, out FacingDirection direction)
	{
		int3 delta = toPoint - fromPoint;
		if (delta.x == 0 && delta.z == 0)
		{
			direction = FacingDirection.North;
			return false;
		}

		if (math.abs(delta.x) >= math.abs(delta.z))
		{
			direction = delta.x >= 0 ? FacingDirection.East : FacingDirection.West;
			return true;
		}

		direction = delta.z >= 0 ? FacingDirection.North : FacingDirection.South;
		return true;
	}

	private static Vector3 ToWorld(in int3 point)
	{
		return new Vector3(point.x, point.y, point.z);
	}

	private static float FacingToYaw(FacingDirection direction)
	{
		return direction switch
		{
			FacingDirection.North => 0f,
			FacingDirection.East => 90f,
			FacingDirection.South => 180f,
			FacingDirection.West => 270f,
			_ => 0f
		};
	}
}
