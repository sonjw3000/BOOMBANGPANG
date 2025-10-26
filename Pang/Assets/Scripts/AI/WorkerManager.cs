using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
class WorkerManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	List<AIWorker> Workers;

	// todo
	// task들 또한 여기서 관리하자
	// update시에 알아서 분배하자

	private static WorkerManager _instance;

	public static WorkerManager Instance 
	{
		get
		{
			if (_instance == null)
			{
				Debug.LogError("WorkerManager is NOT initialized!");
				return null;
			}
			return _instance;
		}
	}

	private void Awake()
	{
		Debug.Log("WorkerManager Online!");
		if (_instance != null && _instance != this)
		{
			Destroy(this);
			Debug.Log("WARNNING!! WorkerManager Duplicated");
			return;
		}

		Workers = new List<AIWorker>();

		_instance = this;
		DontDestroyOnLoad(gameObject);
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
		// 목적지 이동중엔 비활성화
		// 
		foreach (var worker in Workers)
		{
			worker.RunBT();
		}
	}
}
