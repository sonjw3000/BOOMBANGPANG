using System.Collections.Generic;
using UnityEngine;

// 1. 병목 측정

public class MetricsService : MonoBehaviour
{
	private IReadOnlyDictionary<WorkerTask.TaskType, LinkedList<WorkerTask>> TaskQueue
		=> GameContext.Instance.TaskMgr.TaskQueue;
	private IReadOnlyDictionary<WorkerTask.TaskType, LinkedList<WorkerTask>> TaskOnProgress
		=> GameContext.Instance.TaskMgr.TaskOnProgress;
	private IReadOnlyDictionary<OrderTotalStatus, LinkedList<Order>> OrderStatusMap
		=> GameContext.Instance.OrderMgr.OrderStatusMap;

	// workers
	public int GetQueueLength(WorkerTask.TaskType type) => TaskQueue[type].Count;
	public int GetOnProgressLength(WorkerTask.TaskType type) => TaskOnProgress[type].Count;

	// orders
	public int GetOrderStatusLength(OrderTotalStatus status) => OrderStatusMap[status].Count;
}
