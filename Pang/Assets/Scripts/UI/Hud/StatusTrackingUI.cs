using TMPro;
using System.Collections.Generic;
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

	[Header("Worker Summary")]
	[SerializeField] private TMP_Text workerSummary;


	private MetricsService Metrics => GameContext.Instance.Metrics;

	private void Update()
	{
		taskUnload.text = BuildTaskTrackingText(Unloading, "Unloading");
		taskStore.text = BuildTaskTrackingText(Storing, "Storing");
		taskPicking.text = BuildTaskTrackingText(Picking, "Picking");
		taskPacking.text = BuildTaskTrackingText(Packing, "Packing");
		taskLoad.text = BuildTaskTrackingText(Loading, "Loading");
		taskWater.text = BuildTaskTrackingText(Water, "Water");

		orderPending.text =		$"Pending: {Metrics.GetOrderStatusLength(Pending)}";
		orderInProgress.text =	$"InProgress: {Metrics.GetOrderStatusLength(InProgress)}";
		orderCompleted.text =	$"Completed: {Metrics.GetOrderStatusLength(Completed)}";
		orderCancelled.text =	$"Cancelled: {Metrics.GetOrderStatusLength(Cancelled)}";

		if (workerSummary != null)
		{
			workerSummary.text = BuildWorkerSummaryText();
		}
	}

	private string BuildTaskTrackingText(WorkerTask.TaskType taskType, string label)
	{
		string trackingText = $"{label}	| Queue: {Metrics.GetQueueLength(taskType)} | Working: {Metrics.GetOnProgressLength(taskType)}";
		List<string> detailParts = new();

		AddDetailPart(detailParts, "WaitInput", Metrics.GetTaskWorkerStatusCount(taskType, WorkerStatusAction.WaitingForItems));
		AddDetailPart(detailParts, "Traffic", Metrics.GetTaskWorkerStatusCount(taskType, WorkerStatusAction.TrafficBlock));
		AddDetailPart(detailParts, "WaitTarget", Metrics.GetTaskWorkerStatusCount(taskType, WorkerStatusAction.WaitingForTargetBuilding));

		if (detailParts.Count == 0)
			return trackingText;

		return $"{trackingText}\n  {string.Join(" | ", detailParts)}";
	}

	private string BuildWorkerSummaryText()
	{
		List<string> summaryParts = new();
		AddDetailPart(summaryParts, "Idle", Metrics.GetWorkerStatusCount(WorkerStatusAction.Idle));
		AddDetailPart(summaryParts, "Moving", Metrics.GetWorkerStatusCount(WorkerStatusAction.MovingTo));
		AddDetailPart(summaryParts, "WaitInput", Metrics.GetWorkerStatusCount(WorkerStatusAction.WaitingForItems));
		AddDetailPart(summaryParts, "Traffic", Metrics.GetWorkerStatusCount(WorkerStatusAction.TrafficBlock));
		AddDetailPart(summaryParts, "WaitTarget", Metrics.GetWorkerStatusCount(WorkerStatusAction.WaitingForTargetBuilding));
		AddDetailPart(summaryParts, "Resting", Metrics.GetWorkerStatusCount(WorkerStatusAction.Resting));
		AddDetailPart(summaryParts, "Charging", Metrics.GetWorkerStatusCount(WorkerStatusAction.Charging));
		AddDetailPart(summaryParts, "HandlingMistake", Metrics.GetWorkerStatusCount(WorkerStatusAction.HandlingMistake));
		AddDetailPart(summaryParts, "Collapse", Metrics.GetWorkerStatusCount(WorkerStatusAction.Collapse));

		return summaryParts.Count > 0
			? $"Workers | {string.Join(" | ", summaryParts)}"
			: "Workers | No active status";
	}

	private static void AddDetailPart(List<string> parts, string label, int value)
	{
		if (value > 0)
		{
			parts.Add($"{label} {value}");
		}
	}
}
