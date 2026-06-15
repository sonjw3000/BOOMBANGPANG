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

public sealed class Airlock : ItemInteraction
{
	[SerializeField] private float entryDelaySeconds = 3.0f;

	private Coroutine entryRoutine;
	private AIWorker reservedWorker;
	private AirlockDirection reservedDirection;
	private AirlockState state = AirlockState.Idle;

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

		if (entryRoutine != null)
			StopCoroutine(entryRoutine);

		entryRoutine = StartCoroutine(EntryRoutine(worker));
		return true;
	}

	public void Release(AIWorker worker)
	{
		if (worker != null && reservedWorker != null && worker != reservedWorker)
			return;

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

	private IEnumerator EntryRoutine(AIWorker worker)
	{
		state = AirlockState.Occupied;
		yield return new WaitForSeconds(entryDelaySeconds);

		if (reservedWorker == worker)
			Release(worker);

		entryRoutine = null;
	}
}
