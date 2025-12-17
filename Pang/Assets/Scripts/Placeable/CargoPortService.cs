
using System.Collections.Generic;
using Unity.Mathematics;

public class CargoPortService
{
	private List<CargoPort> cargoPorts = new();

	private InboundWorkflowManager IBMgr => GameContext.Instance.IBWorkflowMgr;

	public CargoPort GetClosestAvailablePort(in int3 pos)
	{
		CargoPort port = null;
		int maxDist = int.MaxValue;

		foreach (CargoPort p in cargoPorts)
		{
			int3 portPos = p.GridPosition;
			int3 posDiff = pos - portPos;
			int dist =
				posDiff.x * posDiff.x +
				posDiff.y * posDiff.y +
				posDiff.z * posDiff.z;

			if (dist < maxDist)
			{
				maxDist = dist;
				port = p;
			}
		}

		return port;
	}

	public void RegisterPort(CargoPort port)
	{
		port.OnItemAdded += OnPortItemAdded;
		cargoPorts.Add(port);
	}

	public void UnregisterPort(CargoPort port)
	{
		port.OnItemAdded -= OnPortItemAdded;
		cargoPorts.Remove(port);
	}

	public void OnPortItemAdded(ShelfBase port, ItemStack item)
	{
		IBMgr.BuildStoreJob(port, item);
	}
}
