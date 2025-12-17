using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using static WorkerTask.TaskType;
// inbound 작업 흐름을 관리
// 깨차

// rocket 착륙
// payload unload
// labeling
// storing

public class InboundWorkflowManager : MonoBehaviour
{
	private StoringPlanner storingPlanner = new StoringItemFriendly();

	[SerializeField, Range(1, 100)] private float maxBoxPercentage = 80.0f;
	[SerializeField] private float storingTaskBuildTime = 10.0f;
	private float timer = 0;

	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;

	public void BuildTaskByPayload(Rocket rocket)
	{
		int3 goalPos = rocket.InteractionPoints[0];

		UnloadingTask task = new UnloadingTask(rocket);

		// unloading은 cargoport로 보내는 것으로 완성
		TaskMgr.TaskQueue[Unloading].AddLast(task);
	}

	public void BuildStoreJob(ShelfBase port, ItemStack item)
	{
		// todo
		// 나중에는 하차 후 분류하는 작업까지 거친 후 선반에 넣는 작업으로 나아가야함
		storingPlanner.BuildStoreJob((CargoPort)port, item);
	}

	// inbound의 task를 연계생성
	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case Unloading:

				break;
			case Storing:

				// nothing to do

				break;
		}
	}

	private void BuildStoreTask()
	{
		while (storingPlanner.BuildStoreTask(maxBoxPercentage, out var task))
		{
			TaskMgr.TaskQueue[Storing].AddLast(task);
		}
	}

	private void Update()
	{
		timer += Time.deltaTime;

		if (timer >= storingTaskBuildTime ||
			false
			)
		{
			timer = 0;
			BuildStoreTask();
		}
	}

}
