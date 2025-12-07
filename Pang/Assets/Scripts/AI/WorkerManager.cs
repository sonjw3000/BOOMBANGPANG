using BlackBoardSystem;
using System.Collections.Generic;
using UnityEngine;
using static WorkerTask;

[DefaultExecutionOrder(-100)]
public class WorkerManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	[SerializeField] private List<AIWorker> workers;

	// todo
	// 작업자들의 선호 작업 종류로 분류해서 관리
	// ex) 이전 작업이 피킹 -> 피킹에 배치될 확률 높임
	// ex) 이전 작업 Unlodaing -> Unloading에 배치될 확률 높임

	// todo
	// 전역 블랙보드의 관리는 다른곳에 넘겨야함
	private BlackBoard globalBlackboard;

	private void Awake()
	{
		//workers = new List<AIWorker>();
	}

	public void RegisterWorker(AIWorker worker)
	{
		workers.Add(worker);
	}

	public void UnregisterWorker(AIWorker worker)
	{
		workers.Remove(worker);
	}

	public AIWorker GetAvailableWorkers(WorkerTask taskData)
	{
		// is available worker there?
		// todo
		// 태스크의 조건과 알맞은 작업자를 돌려줌
		foreach (var worker in workers)
		{
			if (worker.CurrentTask == null)
			{
				return worker;
			}
		}

		return null;
	}

	private void Update()
	{
		// todo
		// 타이밍별로 정리해두고 관리해야 함
		// 목적지 이동중엔 비활성화
		// 
		foreach (var worker in workers)
		{
			if (worker.enabled)
				worker.RunBT(globalBlackboard);
		}
	}
}
