using System;
using System.Collections.Generic;
using UnityEngine;

public enum OrderStatus
{
	Pending,
	Allocated,
	Picking,
	Packaging,
	WaitingForShipping,
	Shipping,
	IndDelivery,
	Completed,
	Cancelled,
	Delayed,
}

public enum OrderTotalStatus
{
	Pending,
	InProgress,
	Completed,
	Cancelled,
}

public enum OrderDestination
{
	None,
	Mars,
	Titan,
}

public class Order
{
	public int OrderID;
	public List<OrderLine> Lines;
	public Tuple<int, int> DeadLine;
	public int Priority;
	public OrderDestination Destination = OrderDestination.None;

	private OrderTotalStatus status = OrderTotalStatus.Pending;

	public OrderTotalStatus Status => status;

	public void RestoreStatus(OrderTotalStatus status)
	{
		this.status = status;
	}

	public OrderTotalStatus RecalculateStatus()
	{
		if (Lines == null || Lines.Count == 0)
		{
			status = OrderTotalStatus.Pending;
			return status;
		}

		bool anyStarted = false;
		bool allFinal = true;
		bool allCancelled = true;

		foreach (var line in Lines)
		{
			if (line == null)
				continue;

			OrderStatus lineStatus = line.Status;
			if (lineStatus != OrderStatus.Pending)
				anyStarted = true;

			if (line.IsFinal == false)
				allFinal = false;

			if (lineStatus != OrderStatus.Cancelled)
				allCancelled = false;
		}

		if (allCancelled)
		{
			status = OrderTotalStatus.Cancelled;
		}
		else if (allFinal)
		{
			status = OrderTotalStatus.Completed;
		}
		else if (anyStarted)
		{
			status = OrderTotalStatus.InProgress;
		}
		else
		{
			status = OrderTotalStatus.Pending;
		}

		return status;
	}
}

public partial class OrderLine
{
	public int SaveId { get; set; }
	public readonly Order ParentOrder;
	public readonly uint ItemID;
	public readonly int Quantity;
	public readonly Assets.Scripts.Contract.ContractRuntime SourceContract;

	public int StartWeek;
	public int DueWeek;
	public int BaseReward;
	public int DelayPenalty;
	public float ReputationChange;

	private bool isCancelled = false;

	public int PickingAllocatedQuantity { get; private set; }
	public int PickingCompletedQuantity { get; private set; }
	public int PackagingCompletedQuantity { get; private set; }
	public int WaitingForShippingQuantity { get; private set; }
	public int ShippingQuantity { get; private set; }
	public int InDeliveryQuantity { get; private set; }
	public int CompletedQuantity { get; private set; }

	public OrderStatus Status => isCancelled ? OrderStatus.Cancelled : EvaluateStatus();
	public bool IsFinal => Status == OrderStatus.Completed || Status == OrderStatus.Cancelled;
	public bool CanAllocatePicking => isCancelled == false && GetPickingAllocatableQuantity() > 0;

	public OrderLine(Order parentOrder, uint itemID, int quantity, Assets.Scripts.Contract.ContractRuntime sourceContract)
	{
		ParentOrder = parentOrder;
		ItemID = itemID;
		Quantity = quantity;
		SourceContract = sourceContract;
	}

	public int GetPickingAllocatableQuantity()
	{
		return ClampInt(Quantity - PickingCompletedQuantity - PickingAllocatedQuantity, 0, Quantity);
	}

	public int TryAllocatePicking(int quantity)
	{
		if (isCancelled)
			return 0;

		int actual = ClampInt(quantity, 0, GetPickingAllocatableQuantity());
		PickingAllocatedQuantity += actual;
		return actual;
	}

	public int ReleasePickingAllocation(int quantity)
	{
		int actual = ClampInt(quantity, 0, PickingAllocatedQuantity);
		PickingAllocatedQuantity -= actual;
		return actual;
	}

	public int ReportPickingCompleted(int quantity)
	{
		int actual = ClampInt(quantity, 0, PickingAllocatedQuantity);
		PickingAllocatedQuantity -= actual;
		PickingCompletedQuantity += actual;
		ClampProgress();
		return actual;
	}

	public int ReportPackagingCompleted(int quantity)
	{
		int actual = ClampInt(quantity, 0, PickingCompletedQuantity - PackagingCompletedQuantity);
		PackagingCompletedQuantity += actual;
		ClampProgress();
		return actual;
	}

	public int ReportWaitingForShipping(int quantity)
	{
		int actual = ClampInt(quantity, 0, PackagingCompletedQuantity - WaitingForShippingQuantity);
		WaitingForShippingQuantity += actual;
		ClampProgress();
		return actual;
	}

	public int ReportShipping(int quantity)
	{
		int actual = ClampInt(quantity, 0, WaitingForShippingQuantity - ShippingQuantity);
		ShippingQuantity += actual;
		ClampProgress();
		return actual;
	}

	public int ReportInDelivery(int quantity)
	{
		int actual = ClampInt(quantity, 0, ShippingQuantity - InDeliveryQuantity);
		InDeliveryQuantity += actual;
		ClampProgress();
		return actual;
	}

	public int ReportCompleted(int quantity)
	{
		int actual = ClampInt(quantity, 0, InDeliveryQuantity - CompletedQuantity);
		CompletedQuantity += actual;
		ClampProgress();
		return actual;
	}

	public int RollbackDestroyedCargo(
		int pickedQuantity,
		int packedQuantity,
		PackageOutboundStage outboundStage)
	{
		if (outboundStage == PackageOutboundStage.Completed)
			return 0;

		int actualPicked = ClampInt(pickedQuantity, 0, PickingCompletedQuantity);
		int actualPacked = ClampInt(packedQuantity, 0, Mathf.Min(actualPicked, PackagingCompletedQuantity));

		PickingCompletedQuantity -= actualPicked;
		PackagingCompletedQuantity -= actualPacked;

		if (outboundStage >= PackageOutboundStage.WaitingForShipping)
			WaitingForShippingQuantity -= Mathf.Min(actualPacked, WaitingForShippingQuantity);
		if (outboundStage >= PackageOutboundStage.Shipping)
			ShippingQuantity -= Mathf.Min(actualPacked, ShippingQuantity);
		if (outboundStage >= PackageOutboundStage.InDelivery)
			InDeliveryQuantity -= Mathf.Min(actualPacked, InDeliveryQuantity);

		ClampProgress();
		return actualPicked;
	}

	public void Cancel()
	{
		isCancelled = true;
	}

	public int GetProgressQuantityForStatus(OrderStatus status)
	{
		return status switch
		{
			OrderStatus.Allocated => PickingAllocatedQuantity,
			OrderStatus.Picking => PickingCompletedQuantity,
			OrderStatus.Packaging => PackagingCompletedQuantity,
			OrderStatus.WaitingForShipping => WaitingForShippingQuantity,
			OrderStatus.Shipping => ShippingQuantity,
			OrderStatus.IndDelivery => InDeliveryQuantity,
			OrderStatus.Completed => CompletedQuantity,
			_ => 0,
		};
	}

	private OrderStatus EvaluateStatus()
	{
		if (CompletedQuantity >= Quantity)
			return OrderStatus.Completed;

		if (InDeliveryQuantity > 0)
			return OrderStatus.IndDelivery;

		if (ShippingQuantity > 0)
			return OrderStatus.Shipping;

		if (WaitingForShippingQuantity > 0)
			return OrderStatus.WaitingForShipping;

		if (PackagingCompletedQuantity > 0)
			return OrderStatus.Packaging;

		if (PickingCompletedQuantity > 0)
			return OrderStatus.Picking;

		if (PickingAllocatedQuantity > 0)
			return OrderStatus.Allocated;

		return OrderStatus.Pending;
	}

	private void RestoreLegacyProgress(OrderStatus status)
	{
		PickingAllocatedQuantity = 0;
		PickingCompletedQuantity = 0;
		PackagingCompletedQuantity = 0;
		WaitingForShippingQuantity = 0;
		ShippingQuantity = 0;
		InDeliveryQuantity = 0;
		CompletedQuantity = 0;

		switch (status)
		{
			case OrderStatus.Allocated:
				PickingAllocatedQuantity = Quantity;
				break;
			case OrderStatus.Picking:
				PickingCompletedQuantity = Quantity;
				break;
			case OrderStatus.Packaging:
				PickingCompletedQuantity = Quantity;
				PackagingCompletedQuantity = Quantity;
				break;
			case OrderStatus.WaitingForShipping:
				PickingCompletedQuantity = Quantity;
				PackagingCompletedQuantity = Quantity;
				WaitingForShippingQuantity = Quantity;
				break;
			case OrderStatus.Shipping:
				PickingCompletedQuantity = Quantity;
				PackagingCompletedQuantity = Quantity;
				WaitingForShippingQuantity = Quantity;
				ShippingQuantity = Quantity;
				break;
			case OrderStatus.IndDelivery:
				PickingCompletedQuantity = Quantity;
				PackagingCompletedQuantity = Quantity;
				WaitingForShippingQuantity = Quantity;
				ShippingQuantity = Quantity;
				InDeliveryQuantity = Quantity;
				break;
			case OrderStatus.Completed:
				PickingCompletedQuantity = Quantity;
				PackagingCompletedQuantity = Quantity;
				WaitingForShippingQuantity = Quantity;
				ShippingQuantity = Quantity;
				InDeliveryQuantity = Quantity;
				CompletedQuantity = Quantity;
				break;
		}

		ClampProgress();
	}

	private void ClampProgress()
	{
		PickingCompletedQuantity = ClampInt(PickingCompletedQuantity, 0, Quantity);
		PickingAllocatedQuantity = ClampInt(PickingAllocatedQuantity, 0, Quantity - PickingCompletedQuantity);
		PackagingCompletedQuantity = ClampInt(PackagingCompletedQuantity, 0, PickingCompletedQuantity);
		WaitingForShippingQuantity = ClampInt(WaitingForShippingQuantity, 0, PackagingCompletedQuantity);
		ShippingQuantity = ClampInt(ShippingQuantity, 0, WaitingForShippingQuantity);
		InDeliveryQuantity = ClampInt(InDeliveryQuantity, 0, ShippingQuantity);
		CompletedQuantity = ClampInt(CompletedQuantity, 0, InDeliveryQuantity);
	}

	private static bool IsLegacyProgressEmpty(
		int pickingAllocatedQuantity,
		int pickingCompletedQuantity,
		int packagingCompletedQuantity,
		int waitingForShippingQuantity,
		int shippingQuantity,
		int inDeliveryQuantity,
		int completedQuantity)
	{
		return pickingAllocatedQuantity == 0 &&
			pickingCompletedQuantity == 0 &&
			packagingCompletedQuantity == 0 &&
			waitingForShippingQuantity == 0 &&
			shippingQuantity == 0 &&
			inDeliveryQuantity == 0 &&
			completedQuantity == 0;
	}

	private static int ClampInt(int value, int min, int max)
	{
		if (value < min)
			return min;

		if (value > max)
			return max;

		return value;
	}
}
