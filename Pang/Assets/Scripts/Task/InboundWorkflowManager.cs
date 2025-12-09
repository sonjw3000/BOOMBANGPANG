using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
// inbound 작업 흐름을 관리
// 깨차

// rocket 착륙
// payload unload
// labeling
// storing

public class InboundWorkflowManager : MonoBehaviour
{
	[SerializeField] int3 inboundBufferZone = new int3(0, 0, 0);
	[SerializeField] int zoneSize = 3;

	private TaskManager taskManager => GameContext.Instance.TaskMgr;

	public int3 InboundBufferZone => inboundBufferZone;
	public int ZoneSize => zoneSize;

	public void BuildTaskByPayload(Rocket rocket)
	{
		int3 goalPos = rocket.PickingPosition;

		UnloadingTask task = new UnloadingTask(rocket);
		// todo
		// 일단은 하차 즉시 중간지점에 적재한다
		// 나중에는 하차 후 분류하는 작업까지 거친 후 선반에 넣는 작업으로 나아가야함

		taskManager.TaskQueue[WorkerTask.TaskType.Unloading].Enqueue(task, 1);
	}

}
