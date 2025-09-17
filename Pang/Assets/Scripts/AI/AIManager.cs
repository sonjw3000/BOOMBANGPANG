using System.Collections.Generic;
using UnityEngine;

class AIManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	List<AIWorker> Workers = new List<AIWorker>();

	public static AIManager Instance { get; private set; }

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
			worker.Update();
		}
	}
}
