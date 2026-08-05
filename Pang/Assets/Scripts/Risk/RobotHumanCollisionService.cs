using Unity.Mathematics;
using UnityEngine;

public enum RobotHumanCollisionResult
{
	NotApplicable,
	DuplicateIgnored,
	HumanRelocated,
	BlockedByCasualty,
}

public sealed class RobotHumanCollisionService : MonoBehaviour
{
	[SerializeField, Min(0.0f)] private float robotHealthDamage = 5.0f;
	[SerializeField, Range(0.0f, 1.0f)] private float robotWearDamage = 0.02f;

	private static readonly int3[] CardinalDirections =
	{
		new(1, 0, 0),
		new(-1, 0, 0),
		new(0, 0, 1),
		new(0, 0, -1),
	};

	private GridService gridService;
	private WorkplaceIncidentService incidentService;
	private GameTime gameTime;
	private HudEventManager hudEventManager;

	public void Initialize(
		GridService grid,
		WorkplaceIncidentService incidents,
		GameTime time,
		HudEventManager hudEvents)
	{
		gridService = grid;
		incidentService = incidents;
		gameTime = time;
		hudEventManager = hudEvents;
	}

	public RobotHumanCollisionResult TryResolve(
		RobotWorker robot,
		HumanWorker human,
		in int3 collisionCell)
	{
		if (robot == null ||
			human == null ||
			gridService == null ||
			incidentService == null ||
			robot.IsPlayerOverride ||
			robot.IsOperational == false)
		{
			return RobotHumanCollisionResult.NotApplicable;
		}

		if (human.IsOperational == false)
			return RobotHumanCollisionResult.BlockedByCasualty;

		FindRoute humanRoute = human.RouteFinder;
		GridCell collisionGridCell = gridService.GetCell(collisionCell);
		if (humanRoute == null ||
			collisionGridCell == null ||
			collisionGridCell.ReservedRoute != humanRoute ||
			human.GridPosition.Equals(collisionCell) == false)
		{
			return RobotHumanCollisionResult.NotApplicable;
		}

		ulong simulationTick = gameTime?.SimulationTicksPassed ?? 0;
		if (incidentService.TryPrepareRobotCollision(robot, human, collisionCell, simulationTick) == false)
			return RobotHumanCollisionResult.DuplicateIgnored;

		bool relocated = TryRelocateHuman(human);
		if (human.EnterIncapacitatedState(WorkerOperationalState.Knockout) == false)
		{
			incidentService.CancelPreparedRobotCollision(human);
			return human.IsOperational
				? RobotHumanCollisionResult.NotApplicable
				: RobotHumanCollisionResult.BlockedByCasualty;
		}

		incidentService.CompletePreparedRobotCollision(human);
		robot.ApplyDamage(robotHealthDamage);
		robot.ApplyWear(robotWearDamage);

		string outcome = relocated ? "casualty relocated" : "route blocked by casualty";
		string message = $"Robot-human collision at {collisionCell}: {robot.Name} / {human.Name} ({outcome}).";
		Debug.LogWarning($"[RobotHumanCollision] {message}");
		hudEventManager?.Publish(HudEventType.Error, message, human);

		return relocated
			? RobotHumanCollisionResult.HumanRelocated
			: RobotHumanCollisionResult.BlockedByCasualty;
	}

	private bool TryRelocateHuman(HumanWorker human)
	{
		int3 origin = human.GridPosition;
		for (int i = 0; i < CardinalDirections.Length; ++i)
		{
			int3 candidate = origin + CardinalDirections[i];
			if (gridService.IsSameRegion(origin, candidate) == false)
				continue;

			if (gridService.TryRelocateWorker(
				human,
				candidate,
				human.Direction,
				out _))
			{
				return true;
			}
		}

		return false;
	}
}
