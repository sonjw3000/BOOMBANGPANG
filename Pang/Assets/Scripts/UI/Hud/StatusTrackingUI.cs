using TMPro;
using System.Collections.Generic;
using UnityEngine;
using static OrderTotalStatus;
using static WorkerTask.TaskType;

public class StatusTrackingUI : MonoBehaviour
{
	private static readonly WorkerStatusAction[] trackedWorkerStatuses =
	{
		WorkerStatusAction.Idle,
		WorkerStatusAction.MovingTo,
		WorkerStatusAction.Working,
		WorkerStatusAction.WaitingForItems,
		WorkerStatusAction.WaitingForTargetBuilding,
		WorkerStatusAction.TrafficBlock,
		WorkerStatusAction.Resting,
		WorkerStatusAction.Charging,
		WorkerStatusAction.HandlingMistake,
		WorkerStatusAction.Collapse,
	};

	[Header("Task Tracking")]
	[SerializeField] private TMP_Text taskUnload;
	[SerializeField] private TMP_Text taskStore;
	[SerializeField] private TMP_Text taskPicking;
	[SerializeField] private TMP_Text taskPacking;
	[SerializeField] private TMP_Text taskLoad;
	[SerializeField] private TMP_Text taskPackingTransfer;

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
		taskPackingTransfer.text = $"{BuildTaskTrackingText(PackingInput, "PackingInput")}\n{BuildTaskTrackingText(PackingOutput, "PackingOutput")}";

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
		string trackingText = $"{label} | Queue: {Metrics.GetQueueLength(taskType)} | Working: {Metrics.GetOnProgressLength(taskType)}";
		List<string> detailParts = BuildStatusParts(status => Metrics.GetTaskWorkerStatusCount(taskType, status));

		if (detailParts.Count == 0)
			return trackingText;

		return $"{trackingText}\n     {string.Join(" | ", detailParts)}";
	}

	private string BuildWorkerSummaryText()
	{
		List<string> summaryParts = BuildStatusParts(Metrics.GetWorkerStatusCount);

		return summaryParts.Count > 0
			? $"Worker Status | {string.Join(" | ", summaryParts)}"
			: "Worker Status | None";
	}

	private static List<string> BuildStatusParts(System.Func<WorkerStatusAction, int> countProvider)
	{
		List<string> parts = new();

		for (int i = 0; i < trackedWorkerStatuses.Length; ++i)
		{
			WorkerStatusAction status = trackedWorkerStatuses[i];
			int value = countProvider(status);

			if (value <= 0)
				continue;

			parts.Add($"{GetStatusLabel(status)}: {value}");
		}

		return parts;
	}

	private static string GetStatusLabel(WorkerStatusAction status)
	{
		return status switch
		{
			WorkerStatusAction.WaitingForItems => "WaitingFor",
			WorkerStatusAction.WaitingForTargetBuilding => "WaitTarget",
			WorkerStatusAction.TrafficBlock => "Blocked",
			_ => status.ToString(),
		};
	}
}
