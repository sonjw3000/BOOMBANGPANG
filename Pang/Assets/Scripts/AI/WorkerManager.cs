using System.Collections.Generic;
using UnityEngine;

class WorkerManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	List<AIWorker> Workers = new List<AIWorker>();

	// todo
	// task들 또한 여기서 관리하자
	// update시에 알아서 분배하자

	public static WorkerManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(Instance);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(Instance);
	}

	public void RegisterWorker(AIWorker worker)
	{
		Workers.Add(worker);
	}

	public void UnregisterWorker(AIWorker worker)
	{
		Workers.Remove(worker);
	}

	private void Update()
	{
		// todo
		// 타이밍별로 정리해두고 관리해야 함
		// 
		foreach (var worker in Workers)
		{
			//worker.Update();
		}
	}
}
