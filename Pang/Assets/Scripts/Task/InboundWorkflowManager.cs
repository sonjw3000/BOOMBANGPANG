using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static WorkerTask.TaskType;
// inbound 작업 흐름을 관리
// 깨차

// rocket 착륙
// payload unload
// labeling
// storing

public class InboundWorkflowManager : MonoBehaviour, IBoundManager
{
	// inbound manager's cargo port service
	[SerializeField] CargoPortService cargoPortService;
	private readonly Dictionary<uint, List<CargoPort>> cargoPortsByItem = new();

	// for storing policy
	[SerializeField, Range(1, 100)] private float maxBoxPercentage = 80.0f;
	[SerializeField] private float storingTaskBuildTime = 10.0f;
	private float timer = 0;

	// todo
	// 아래 이것들을 만들어야한다, 위에 저걸 죽여버리고
	// collecting policy
	// placing policy

	// 일단은 근접 우선으로 설정
	private IPlacingPolicy placingPolicy = new NearestPlacingPolicy();
	private StoringPlanner storingPlanner = new StoringItemFriendly();

	public CargoPortService CargoPorts => cargoPortService;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	public Dictionary<uint, List<CargoPort>> CargoPortsByItem => cargoPortsByItem;
	public IPlacingPolicy PlacingPolicy => placingPolicy;

	// ----------------------------------------------------------------
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
	
	// ----------------------------------------------------------------
	// payload로 task 생성
	public void BuildTaskByPayload(Rocket rocket)
	{
		int3 goalPos = rocket.InteractionPoints[0];

		UnloadingTask task = new(rocket);

		// unloading은 cargoport로 보내는 것으로 완성
		TaskMgr.EnqueueTask(task);
	}
	
	// ----------------------------------------------------------------
	// store 생성 가능 체크
	private void CheckStoreTaskAvailable()
	{
		timer += Time.deltaTime;

		// store task 생성 조건
		// 마지막 태스크 생성 후타이머
		// 1개의 박스를 일정 퍼센트까지 채울 수 있을만큼의 물량
		// 기타 등등
		if (timer >= storingTaskBuildTime ||
			false
			)
		{
			timer = 0;
			storingPlanner.BuildStoreJob();

			while (storingPlanner.BuildStoreTask(maxBoxPercentage, out var task))
			{
				//Debug.Log("StoreTask Built!");

				if (task != null)
					TaskMgr.EnqueueTask(task);
			}
		}
	}
	
	// ----------------------------------------------------------------
	// --- 이벤트 핸들러들 ----
	private void OnPortItemPresentChanged(ShelfBase port, uint itemId, bool present)
	{
		if (present)
			OnPortItemAdded(port, itemId);
		else
			OnPortItemRemoved(port, itemId);
	}
	
	private void OnPortItemAdded(ShelfBase port, uint itemId)
	{
		if (cargoPortsByItem.TryGetValue(itemId, out var ports) == false)
		{
			ports = new();
			cargoPortsByItem.Add(itemId, ports);
		}

		ports.Add((CargoPort)port);
	}

	private void OnPortItemRemoved(ShelfBase port, uint itemId)
	{
		if (cargoPortsByItem.TryGetValue(itemId, out var ports) == false)
		{
			// should not happen
			Debug.LogError("ERROR!! No id here but tried to remove port");
			cargoPortsByItem[itemId] = new();
		}
		cargoPortsByItem[itemId].Remove((CargoPort)port);
	}
	
	// ----------------------------------------------------------------
	// unity 함수
	private void Start()
	{
		cargoPortService.OnItemPresentChanged += OnPortItemPresentChanged;
	}

	private void Update()
	{
		CheckStoreTaskAvailable();

	}
	// ----------------------------------------------------------------
}
