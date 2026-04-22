using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// pathfinding의 비용을 일괄적으로 관리하기 위해 중앙화
// 네비게이션의 혼잡 경로를 모방하는 기능 또한 구현 예정

public class PathFindingService : MonoBehaviour
{
	private GridService GridService => GameContext.Instance.GridService;
	private int3 GridSize => GridService.MapSize;

	[SerializeField] private int moveCost = 10;
	[SerializeField] private int rotateCost = 10;
	[SerializeField] private int activeJobLimit = 5;
	[SerializeField] private int stepBudgetPerFrame = 500;

	//private ItemPool<PathResultBuffer> resultPool;
	private ItemPool<PathSearchJob> jobPool;
	private ItemPool<SearchBuffer> searchBufferPool;

	private List<PathSearchJob> activeJobs = new();

	private void Start()
	{
		//resultPool = new(5, () => { return new(); });
		jobPool = new(5, () => { return new(); });
		searchBufferPool = new(5, () => { return new(GridSize); });
	}

	private void Update()
	{
		// do path find by round robin

		for (int i = activeJobs.Count - 1; i >= 0; --i)
		{
			var job = activeJobs[i];

			if (job.Execute(int.MaxValue, out var result))
			{
				job.Setup(null, null);

				activeJobs.RemoveAt(i);
				searchBufferPool.Release(job.Buffer);
				//resultPool.Release(job.Result);
				jobPool.Release(job);
			}
		}
	}



	public void RequestRoute(PathRequest request)
	{
		var searchBuffer = searchBufferPool.Get();
		var job = jobPool.Get();
		
		job.Setup(request, searchBuffer);

		activeJobs.Add(job);
	}

}

