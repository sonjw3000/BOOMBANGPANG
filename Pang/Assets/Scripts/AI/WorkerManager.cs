using BlackBoardSystem;
using System.Collections.Generic;
using UnityEngine;
using static WorkerTask;

[DefaultExecutionOrder(-100)]
class WorkerManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	private List<AIWorker> workers;

	private BlackBoard globalBlackboard;

	// todo
	// task들 또한 여기서 관리하자
	// update시에 알아서 분배하자

	private static WorkerManager instance;

	public static WorkerManager Instance 
	{
		get
		{
			if (instance == null)
			{
				Debug.LogError("WorkerManager is NOT initialized!");
				return null;
			}
			return instance;
		}
	}

	private void Awake()
	{
		Debug.Log("WorkerManager Online!");
		if (instance != null && instance != this)
		{
			Destroy(this);
			Debug.Log("WARNNING!! WorkerManager Duplicated");
			return;
		}

		workers = new List<AIWorker>();

		instance = this;
		DontDestroyOnLoad(gameObject);
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
		return workers[0];
		//return null;
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
