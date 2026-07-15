using UnityEngine;
using System;
using System.Collections.Generic;

public partial class OrderDeliveryService : MonoBehaviour
{
	private class DeliveryProgress
	{
		public BoxBase Cargo = null;
		public float TimeRemain = 0.0f;

		public DeliveryProgress(BoxBase box, float duration)
		{
			Cargo = box;
			TimeRemain = duration;
		}
	}

	private static OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private static BoxManager BoxMgr => GameContext.Instance.BoxMgr;
	private static OutboundWorkflowService OutboundWorkflow => GameContext.Instance.OBWorkflowSvc;

	private List<DeliveryProgress> deliveryProgresses = new();

	private void Update()
	{
		// reduce time remain
		for (int i = deliveryProgresses.Count - 1; i >= 0; --i)
		{
			var progress = deliveryProgresses[i];

			progress.TimeRemain -= Time.deltaTime;

			if (progress.TimeRemain > 0)
				continue;

			int reported = OutboundWorkflow.ReportOutboundProgressFromManifest(progress.Cargo, PackageOutboundStage.Completed);
			if (reported <= 0)
				Debug.LogWarning("[OrderDeliveryService] Delivered cargo without manifest completion progress.");

			Debug.Log("Cargo Delivered!");

			progress.Cargo.OnInvalidated -= HandleDeliveryCargoInvalidated;
			BoxMgr.DisableBox(progress.Cargo);
			deliveryProgresses.RemoveAt(i);
		}
	}

	public void DeliverCargo(BoxBase box, float duration)
	{
		if (box == null)
			return;

		box.OnInvalidated -= HandleDeliveryCargoInvalidated;
		box.OnInvalidated += HandleDeliveryCargoInvalidated;
		deliveryProgresses.Add(new(box, duration));
	}

	private void HandleDeliveryCargoInvalidated(BoxBase box)
	{
		if (box == null)
			return;

		box.OnInvalidated -= HandleDeliveryCargoInvalidated;
		for (int i = deliveryProgresses.Count - 1; i >= 0; --i)
		{
			if (deliveryProgresses[i].Cargo == box)
				deliveryProgresses.RemoveAt(i);
		}
	}

}
