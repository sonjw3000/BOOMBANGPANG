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

	[Tooltip("All active searches share this node-expansion budget per frame.")]
	[SerializeField, Min(1)] private int stepBudgetPerFrame = 500;
	[Tooltip("Maximum search steps per turn before yielding to the next request.")]
	[SerializeField, Min(1)] private int stepsPerTurn = 50;
	[SerializeField] private int plannedPathCongestionCost = 2;
	[SerializeField] private int stalePlannedPathCongestionCost = 6;

	//private ItemPool<PathResultBuffer> resultPool;
	private ItemPool<PathSearchJob> jobPool;
	private ItemPool<SearchBuffer> searchBufferPool;

	private List<PathSearchJob> activeJobs = new();
	private int nextJobIndex;
	private bool isReady;

	public int PlannedPathCongestionCost => plannedPathCongestionCost;
	public int StalePlannedPathCongestionCost => stalePlannedPathCongestionCost;
	public int ActiveJobCount => activeJobs.Count;
	public int LastFrameSearchSteps { get; private set; }

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
		LastFrameSearchSteps = 0;
		int remainingBudget = Mathf.Max(1, stepBudgetPerFrame);
		int turnBudget = Mathf.Max(1, stepsPerTurn);
		// Completion callbacks may append requests. Those requests start next frame.
		int jobsThisFrame = activeJobs.Count;
		while (remainingBudget > 0 && jobsThisFrame > 0)
		{
			if (nextJobIndex >= jobsThisFrame)
				nextJobIndex = 0;
			PathSearchJob job = activeJobs[nextJobIndex];
			bool completed = job.Execute(Mathf.Min(turnBudget, remainingBudget), out int consumedSteps);
			remainingBudget -= consumedSteps;
			LastFrameSearchSteps += consumedSteps;
			if (completed)
			{
				activeJobs.RemoveAt(nextJobIndex);
				--jobsThisFrame;
				try
				{
					job.SetPath();
				}
				finally
				{
					searchBufferPool.Release(job.Buffer);
					job.Setup(null, null);
					jobPool.Release(job);
				}
			}
			else
			{
				++nextJobIndex;
			}
		}
		if (activeJobs.Count == 0)
			nextJobIndex = 0;
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
