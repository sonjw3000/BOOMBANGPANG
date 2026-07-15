using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct MedicalServiceRequest
{
	public readonly int IncidentId;
	public readonly AIWorker Worker;

	public MedicalServiceRequest(int incidentId, AIWorker worker)
	{
		IncidentId = incidentId;
		Worker = worker;
	}
}

public interface IInternalMedicalProvider
{
	uint MedicalProviderId { get; }
	bool IsMedicalServiceAvailable { get; }
	bool TryAcceptMedicalRequest(in MedicalServiceRequest request);
}

public sealed class MedicalService : MonoBehaviour
{
	private readonly List<IInternalMedicalProvider> internalProviders = new();

	public event Action<WorkerServiceHandoff> OnHandoffCompleted;
	public event Action OnServiceAvailabilityChanged;

	private VendorService VendorService => GameContext.HasInstance ? GameContext.Instance.VendorService : null;
	private EconomyService EconomyService => GameContext.HasInstance ? GameContext.Instance.EconomyService : null;

	public void RegisterInternalProvider(IInternalMedicalProvider provider)
	{
		if (provider == null || internalProviders.Contains(provider))
			return;

		internalProviders.Add(provider);
		OnServiceAvailabilityChanged?.Invoke();
	}

	public void UnregisterInternalProvider(IInternalMedicalProvider provider)
	{
		if (provider == null || internalProviders.Remove(provider) == false)
			return;

		OnServiceAvailabilityChanged?.Invoke();
	}

	public bool RequestCare(int incidentId, AIWorker worker)
	{
		if (worker == null || worker.WorkerKind != WorkerKind.Human)
			return false;

		RemoveMissingInternalProviders();
		if (internalProviders.Count > 0)
		{
			MedicalServiceRequest request = new(incidentId, worker);
			for (int i = 0; i < internalProviders.Count; ++i)
			{
				IInternalMedicalProvider provider = internalProviders[i];
				if (provider.IsMedicalServiceAvailable && provider.TryAcceptMedicalRequest(in request))
					return true;
			}

			return false;
		}

		if (VendorService == null ||
			VendorService.TryGetActiveVendor(VendorType.Medical, out VendorRuntime runtime) == false ||
			runtime?.Vendor is not MedicalVendor vendor)
			return false;

		Charge(vendor.ServiceFee, EconomyTransaction.Reason.MedicalDispatch);
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

	public void ProcessSubscription(MedicalVendor vendor)
	{
		if (vendor != null)
			Charge(vendor.SubscriptionFee, EconomyTransaction.Reason.MedicalSubscription);
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
