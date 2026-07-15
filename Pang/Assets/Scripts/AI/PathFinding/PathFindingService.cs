using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// pathfinding의 비용을 일괄적으로 관리하기 위해 중앙화
// 네비게이션의 혼잡 경로를 모방하는 기능 또한 구현 예정

public class PathFindingService : MonoBehaviour
{
	private GridService GridService => GameContext.Instance.GridService;
	private int3 GridSize => GridService.MapSize;

	[SerializeField] private int activeJobLimit = 5;
	[SerializeField] private int stepBudgetPerFrame = 500;
	[SerializeField] private int plannedPathCongestionCost = 2;
	[SerializeField] private int stalePlannedPathCongestionCost = 6;

	//private ItemPool<PathResultBuffer> resultPool;
	private ItemPool<PathSearchJob> jobPool;
	private ItemPool<SearchBuffer> searchBufferPool;

	private List<PathSearchJob> activeJobs = new();
	private bool isReady;

	public int PlannedPathCongestionCost => plannedPathCongestionCost;
	public int StalePlannedPathCongestionCost => stalePlannedPathCongestionCost;

	private void Start()
	{
		//resultPool = new(5, () => { return new(); });
		jobPool = new(5, () => { return new(); });
		searchBufferPool = new(5, () => { return new(GridSize); });
		PathResultBuffer.InitializePool(100);
		isReady = true;
	}

	private void Update()
	{
		// do path find by round robin

		for (int i = activeJobs.Count - 1; i >= 0; --i)
		{
			var job = activeJobs[i];

			if (job.Execute(stepBudgetPerFrame))
			{
				job.SetPath();

				activeJobs.RemoveAt(i);
				searchBufferPool.Release(job.Buffer);
				
				job.Setup(null, null);

				//resultPool.Release(job.Result);
				jobPool.Release(job);
			}
			else
			{
				if (job.Buffer.OpenSet.Peek(out int idx))
				{
					Debug.Log("Pending, Job's Open Set: " + job.Buffer.OpenSet.Count);
					ref PathNodeRecord record = ref job.Buffer.GetStateRecordByStateIndex(idx);
					int3 pos = job.Buffer.GetPosition(idx);
					FacingDirection dir = job.Buffer.GetFacingDirection(idx);

					Debug.Log($"Current Node: {pos} G: {record.GCost}, H: {record.HCost}, Direction: {dir}");
				}
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

	public bool RequestPreviewRoute(in int3 startPosition, in int3 endPosition,
		Action<IReadOnlyList<int3>> completed, Func<int3, bool> canTraverseBlockedCell = null)
	{
		if (isReady == false || completed == null || startPosition.y != endPosition.y)
			return false;

		RequestRoute(new PathRequest(
			startPosition,
			endPosition,
			FacingDirection.North,
			result =>
			{
				List<int3> positions = new(result?.Path?.Count ?? 0);
				try
				{
					if (result?.Path != null)
					{
						foreach (PathNode node in result.Path)
							positions.Add(node.Position);
					}
				}
				finally
				{
					result?.Clear();
				}

				completed(positions);
			},
			canTraverseBlockedCell));
		return true;
	}

}
