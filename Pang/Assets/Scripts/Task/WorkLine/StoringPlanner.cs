

// 전략들
// 1. cargo친화
//		하나의 cargo의 물품을 최대한 담아서 shelf에 저장
// 2. 물품 친화
//		cargo들을 돌며 최대한 같은 종류의 물품을 박스에 담아 shelf에 저장
// 3. 클러스터링 (구현 난이도 UP, 매우 후순위)
//		특정 아이템을 기준으로 가까운 shelf에 위치한 아이템들을 최대한 모아서 이동

using System.Collections.Generic;

public abstract class StoringPlanner
{
	static protected int jobID = 1;

	protected InboundWorkflowManager IBManager => GameContext.Instance.IBWorkflowMgr;
	protected CargoPortService CargoPortService => GameContext.Instance.WMSys.CargoPorts;

	public abstract void BuildStoreJob(CargoPort port, ItemStack item);

	public abstract bool BuildStoreTask(float boxPercentage, out StoringTask task);
}

// store by itemid
public sealed class StoringItemFriendly : StoringPlanner
{
	//private Dictionary<CargoPort, List<WorkJob>> pendingJobs;
	private Dictionary<uint, List<WorkLine>> pendingLines;

	private int bestItemLineCnt = -1;
	private uint bestItemLineID = 0;

	public override void BuildStoreJob(CargoPort port, ItemStack item)
	{
		uint itemID = item.ItemID;
		WorkLine line = new WorkLine(port, itemID, item.Quantity);

		if (pendingLines.ContainsKey(itemID) == false)
			pendingLines[itemID] = new();

		pendingLines[itemID].Add(line);

		if (pendingLines[itemID].Count > bestItemLineCnt)
		{
			bestItemLineCnt = pendingLines[itemID].Count;
			bestItemLineID = itemID;
		}
	}

	public override bool BuildStoreTask(float boxPercentage, out StoringTask task)
	{
		task = null;

		if (pendingLines.Count == 0) return false;

		// 가장 item이 많은 line을 찾는다

		// todo
		// boxPercentage에 의해 job의 Line을 제한한다
		int removed = pendingLines[bestItemLineID].Count;

		List<WorkLine> line = new(pendingLines[bestItemLineID]);
		pendingLines[bestItemLineID].RemoveRange(0, removed);

		WorkJob job = new WorkJob(jobID++, line);

		task = new StoringTask(job);

		if (pendingLines[bestItemLineID].Count == 0)
		{
			pendingLines.Remove(bestItemLineID);
		}

		// bestItemLine을 다시 찾는다
		bestItemLineCnt = -1;
		foreach (var kv in pendingLines)
		{
			int c = kv.Value.Count;
			if (c > bestItemLineCnt)
			{
				bestItemLineCnt = c;
				bestItemLineID = kv.Key;
			}
		}

		// 더이상 task를 만들 line이 없으면 return false
		return pendingLines.Count > 0;
	}

}

