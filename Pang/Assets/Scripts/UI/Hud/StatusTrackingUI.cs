using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static OrderTotalStatus;
using static WorkerTask.TaskType;

public class StatusTrackingUI : MonoBehaviour
{
	[Header("Task Tracking")]
	[SerializeField] private TMP_Text taskUnload;
	[SerializeField] private TMP_Text taskStore;
	[SerializeField] private TMP_Text taskPicking;
	[SerializeField] private TMP_Text taskPacking;
	[SerializeField] private TMP_Text taskLoad;
	[SerializeField] private TMP_Text taskWater;

	[Header("Order Counts")]
	[SerializeField] private TMP_Text orderPending;
	[SerializeField] private TMP_Text orderInProgress;
	[SerializeField] private TMP_Text orderCompleted;
	[SerializeField] private TMP_Text orderCancelled;


	private MetricsService Metrics => GameContext.Instance.Metrics;

	private void Update()
	{
		taskUnload.text =	$"Unloading	| Queue: {Metrics.GetQueueLength(Unloading)} | Working: {Metrics.GetOnProgressLength(Unloading)}";
		taskStore.text =	$"Storing	| Queue: {Metrics.GetQueueLength(Storing)} | Working: {Metrics.GetOnProgressLength(Storing)}";
		taskPicking.text =	$"Picking	| Queue: {Metrics.GetQueueLength(Picking)} | Working: {Metrics.GetOnProgressLength(Picking)}";
		taskPacking.text =	$"Packing	| Queue: {Metrics.GetQueueLength(Packing)} | Working: {Metrics.GetOnProgressLength(Packing)}";
		taskLoad.text =		$"Loading	| Queue: {Metrics.GetQueueLength(Loading)} | Working: {Metrics.GetOnProgressLength(Loading)}";
		taskWater.text =	$"Water		| Queue: {Metrics.GetQueueLength(Water)} | Working: {Metrics.GetOnProgressLength(Water)}";

		orderPending.text =		$"Pending: {Metrics.GetOrderStatusLength(Pending)}";
		orderInProgress.text =	$"InProgress: {Metrics.GetOrderStatusLength(InProgress)}";
		orderCompleted.text =	$"Completed: {Metrics.GetOrderStatusLength(Completed)}";
		orderCancelled.text =	$"Cancelled: {Metrics.GetOrderStatusLength(Cancelled)}";
	}
}
