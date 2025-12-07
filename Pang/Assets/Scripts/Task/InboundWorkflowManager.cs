using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
// inbound 작업 흐름을 관리
// 깨차


public class InboundWorkflowManager : MonoBehaviour
{
	[SerializeField] int3 unloadingZone = new int3(0, 0, 0);


	public void BuildTaskByPayload(Rocket rocket)
	{
		int3 goalPos = rocket.PickingPosition;

		var payload = rocket.GetPayload();

		UnloadingTask task = new UnloadingTask(unloadingZone);
		// todo
		// 일단은 하차 즉시 선반에 적재한다
		// 나중에는 하차 후 분류하는 작업까지 거친 후 선반에 넣는 작업으로 나아가야함


	}


}