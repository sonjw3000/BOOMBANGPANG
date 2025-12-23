

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
	protected CargoPortService PortService => GameContext.Instance.WMSys.CargoPorts;

	public abstract void BuildStoreJob();

	public abstract bool BuildStoreTask(float boxPercentage, out StoringTask task);
}

// store by itemid
public sealed class StoringItemFriendly : StoringPlanner
{
	//private Dictionary<CargoPort, List<WorkJob>> pendingJobs;
	private Dictionary<uint, List<WorkLine>> pendingLines = new();


	public override void BuildStoreJob()
	{
		foreach ((var id, var ports) in PortService.CargoPortsByItem)
		{
			// cargo에 있는 item별로 pendlingLine을 모은다
			if (pendingLines.TryGetValue(id, out var lines) == false)
			{
				lines = new();
				pendingLines.Add(id, lines);
			}
			
			// 모든 포트에 있는 해당 ID의 아이템들을 line으로 만든다
			foreach (var port in ports)
			{
				// if port's line is not fully reserved, then build the rest line
				int reserved = port.ReservePicking(id, port.ItemTotals[id]);

				if (reserved <= 0) continue;

				WorkLine line = new WorkLine(port, id, reserved);
				lines.Add(line);
			}
		}
	}

	public override bool BuildStoreTask(float boxPercentage, out StoringTask task)
	{
		task = null;

		// 더이상 task를 만들 line이 없으면 return false
		if (pendingLines.Count == 0) return false;


		// bestItemLine을 찾는다
		int bestItemLineCnt = -1;
		uint bestItemLineID = 0;
		foreach (var kv in pendingLines)
		{
			int c = kv.Value.Count;
			if (c > bestItemLineCnt)
			{
				bestItemLineCnt = c;
				bestItemLineID = kv.Key;
			}
		}

		if (bestItemLineCnt == 0) return false;

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

		return true;
	}

}

