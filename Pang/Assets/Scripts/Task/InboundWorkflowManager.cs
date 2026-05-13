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
	[SerializeField] private float storingTaskBuildTime = 10.0f;
	[SerializeField] private int maxStoreTasksPerUpdate = 64;
	private float timer = 0;

	// todo
	// 아래 이것들을 만들어야한다, 위에 저걸 죽여버리고
	// collecting policy
	// placing policy

	//
	//private

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
		UnloadingTask task = new(rocket);

		// unloading은 cargo port로 보내는 것으로 완성
		TaskMgr.EnqueueTask(task);
	}
	
	// ----------------------------------------------------------------
	// store 생성 가능 체크
	private void CheckStoreTaskAvailable()
	{
		timer += Time.deltaTime;

		// store task build 조건 시간으로만 체크
		// cargo port의 상태를 체크해서 상자를 채울 수 있으면 store task를 만들 수 있게 하자
		if (timer >= storingTaskBuildTime ||
			storingPlanner.CanBuildFullTask()
			)
		{
			timer = 0;

			int builtCount = 0;
			while (storingPlanner.BuildStoreTask(out var task))
			{
				if (++builtCount > maxStoreTasksPerUpdate)
				{
					Debug.LogError($"[InboundWorkflow] Aborted storing task build loop after {maxStoreTasksPerUpdate} tasks in one update.");
					break;
				}

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
		{
			OnPortItemAdded(port, itemId);
			storingPlanner.OnPortItemAdded(port, itemId);
		}
		else
		{
			OnPortItemRemoved(port, itemId);
			storingPlanner.OnPortItemRemoved(port, itemId);
		}
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
	
	private void OnPortItemReserved(ShelfBase port, uint itemId, int quantity)
	{
		storingPlanner.OnPortItemReserved(port, itemId, quantity);
	}

	private void OnPortItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		storingPlanner.OnPortItemQuantityChanged(port, itemId, quantityDelta);
	}

	// ----------------------------------------------------------------
	// unity 함수
	private void Start()
	{
		cargoPortService.OnItemPresentChanged += OnPortItemPresentChanged;
		cargoPortService.OnItemQuantityChanged += OnPortItemQuantityChanged;
		cargoPortService.OnReserveQuantityChanged += OnPortItemReserved;
	}

	private void OnDestroy()
	{
		cargoPortService.OnItemPresentChanged -= OnPortItemPresentChanged;
		cargoPortService.OnItemQuantityChanged -= OnPortItemQuantityChanged;
		cargoPortService.OnReserveQuantityChanged -= OnPortItemReserved;
	}

	private void Update()
	{
		CheckStoreTaskAvailable();

	}

	public void ResetRuntimeState()
	{
		cargoPortsByItem.Clear();
		timer = 0.0f;
		storingPlanner = new StoringItemFriendly();
	}
	// ----------------------------------------------------------------
}
