using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct RobotFixServiceRequest
{
	public readonly int IncidentId;
	public readonly AIWorker Worker;

	public RobotFixServiceRequest(int incidentId, AIWorker worker)
	{
		IncidentId = incidentId;
		Worker = worker;
	}
}

public interface IInternalRobotFixProvider
{
	uint RobotFixProviderId { get; }
	bool IsRobotFixServiceAvailable { get; }
	bool TryAcceptRobotFixRequest(in RobotFixServiceRequest request);
}

public sealed class RobotFixService : MonoBehaviour
{
	private readonly List<IInternalRobotFixProvider> internalProviders = new();

	public event Action<WorkerServiceHandoff> OnHandoffCompleted;
	public event Action OnServiceAvailabilityChanged;

	private VendorService VendorService => GameContext.HasInstance ? GameContext.Instance.VendorService : null;
	private EconomyService EconomyService => GameContext.HasInstance ? GameContext.Instance.EconomyService : null;

	public void RegisterInternalProvider(IInternalRobotFixProvider provider)
	{
		if (provider == null || internalProviders.Contains(provider))
			return;

		internalProviders.Add(provider);
		OnServiceAvailabilityChanged?.Invoke();
	}

	public void UnregisterInternalProvider(IInternalRobotFixProvider provider)
	{
		if (provider == null || internalProviders.Remove(provider) == false)
			return;

		OnServiceAvailabilityChanged?.Invoke();
	}

	public bool RequestRepair(int incidentId, AIWorker worker)
	{
		if (worker == null || worker.WorkerKind != WorkerKind.Robot)
			return false;

		RemoveMissingInternalProviders();
		if (internalProviders.Count > 0)
		{
			RobotFixServiceRequest request = new(incidentId, worker);
			for (int i = 0; i < internalProviders.Count; ++i)
			{
				IInternalRobotFixProvider provider = internalProviders[i];
				if (provider.IsRobotFixServiceAvailable && provider.TryAcceptRobotFixRequest(in request))
					return true;
			}

			return false;
		}

		if (VendorService == null ||
			VendorService.TryGetActiveVendor(VendorType.Maintenance, out VendorRuntime runtime) == false ||
			runtime?.Vendor is not MaintenanceVendor vendor)
			return false;

		Charge(vendor.ServiceFee, EconomyTransaction.Reason.MaintenanceDispatch);
		OnHandoffCompleted?.Invoke(new WorkerServiceHandoff(
			incidentId,
			WorkerServiceProviderKind.ExternalVendor,
			vendor.VendorId));
		return true;
	}

	public void CompleteInternalHandoff(int incidentId, uint providerId)
	{
		OnHandoffCompleted?.Invoke(new WorkerServiceHandoff(
			incidentId,
			WorkerServiceProviderKind.Internal,
			providerId));
	}

	public void ProcessSubscription(MaintenanceVendor vendor)
	{
		if (vendor != null)
			Charge(vendor.SubscriptionFee, EconomyTransaction.Reason.MaintenanceSubscription);
	}

	private void Charge(int amount, EconomyTransaction.Reason reason)
	{
		if (amount <= 0 || EconomyService == null)
			return;

		EconomyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = -amount,
			reputationDelta = 0f,
			reason = reason,
		});
	}

	private void RemoveMissingInternalProviders()
	{
		for (int i = internalProviders.Count - 1; i >= 0; --i)
		{
			if (internalProviders[i] == null ||
				internalProviders[i] is UnityEngine.Object unityObject && unityObject == null)
				internalProviders.RemoveAt(i);
		}
	}
}
