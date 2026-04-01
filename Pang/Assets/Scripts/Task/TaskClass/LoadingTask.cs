using UnityEngine;
using Unity.Mathematics;
using static IBaseNode;
using static IBaseNode.NodeState;

public class LoadingTask : WorkerTask
{
	private bool isLoadEnd = false;

	private CargoPort targetPort = null;

	static private LaunchStationService LaunchStations => GameContext.Instance.OBWorkflowMgr.LaunchStations;

	public LoadingTask(CargoPort cargoPort) : base(TaskType.Loading)
	{
		this.targetPort = cargoPort;
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();

		if (carryBox == null)
		{
			Debug.LogError("No carryBox ability but assigned to ccc!!");
		}
	}


	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();

		root.Add(AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Cargo,
			setGoal: SetLoadTarget,
			interact: PickCargo
			));

		root.Add(AIWorker.BuildCarryMoveInteract(
			boxRequirement: BoxType.Cargo,
			setGoal: SetLaunchStation,
			interact: StoreCargo
			));


		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isLoadEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[LoadingTask] : ";
	}
#endif

	static private NodeState SetLoadTarget(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;

		if (task.targetPort == null)
		{
			Debug.LogError("No available load port found!");
			return Failure;
		}

		ctx.LocalBlackBoard.Set<int3>("goalPos", task.targetPort.GetClosestInteractionPoint(InteractionKind.Put, ctx.Worker.GridPosition));

		return Success;
	}

	static private NodeState PickCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;

		if (task.targetPort == null)
		{
			Debug.LogError("No available load port found!");
			return Failure;
		}

		BoxBase box = ctx.Worker.GetComponent<CarryBoxAbility>().CarringBox;
		task.targetPort.PickCargo(box);
		task.targetPort.SetInputReady(true);

		return Success;
	}

	static private NodeState SetLaunchStation(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var launchStation = LaunchStations.GetClosestAvailableTarget(ctx.Worker.GridPosition);
		if (launchStation == null)
		{
			Debug.LogError("No available launch station found!");
			return Failure;
		}

		ctx.LocalBlackBoard.Set<int3>("goalPos", launchStation.GetClosestInteractionPoint(InteractionKind.Put, ctx.Worker.GridPosition));
		return Success;
	}

	static private NodeState StoreCargo(in BTContext ctx)
	{
		var task = (LoadingTask)ctx.Worker.CurrentTask;
		var carryAbility = ctx.Worker.GetComponent<CarryBoxAbility>();
		BoxBase box = carryAbility.CarringBox;
		
		var launchStation = LaunchStations.GetClosestAvailableTarget(ctx.Worker.GridPosition);
		if (launchStation == null)
		{
			Debug.LogError("No available launch station found!");
			return Failure;
		}

		launchStation.TryGetStoreablePad(out var pad);
		pad.StoreCargo(box);

		task.isLoadEnd = true;
		return Success;
	}

}
