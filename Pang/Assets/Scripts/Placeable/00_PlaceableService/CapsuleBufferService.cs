using UnityEngine;
using System.Collections.Generic;

public sealed class CapsuleBufferService : FacilityService<CapsuleBuffer>
{
	// capsule buffer management

	private readonly Dictionary<uint, List<CapsuleBuffer>> registeredBuffers = new();

	protected override bool IsDestinationCandidate(
		CapsuleBuffer facility,
		uint buildingId,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter)
	{
		if (base.IsDestinationCandidate(facility, buildingId, interactionKind, zoneFilter) == false)
			return false;

		return interactionKind switch
		{
			InteractionKind.Put => facility.CanReceiveFromInbound(),
			InteractionKind.Pick => facility.CanDispatchToOutbound(),
			_ => false,
		};
	}

	public bool SetDockState(CapsuleBuffer facility, CapsuleDockState newState)
	{
		if (facility == null)
			return false;

		facility.SetDockState(newState);
		return true;
	}

	public IEnumerable<CapsuleBuffer> GetBuffers(uint buildingId = 0)
	{
		if (buildingId != 0)
		{
			if (registeredBuffers.TryGetValue(buildingId, out List<CapsuleBuffer> buffers) == false)
				yield break;

			for (int i = 0; i < buffers.Count; ++i)
			{
				if (buffers[i] != null)
					yield return buffers[i];
			}

			yield break;
		}

		foreach (var kvp in registeredBuffers)
		{
			List<CapsuleBuffer> buffers = kvp.Value;
			if (buffers == null)
				continue;

			for (int i = 0; i < buffers.Count; ++i)
			{
				if (buffers[i] != null)
					yield return buffers[i];
			}
		}
	}

	protected override void OnRegisterFacility(uint buildingId, CapsuleBuffer facility)
	{
		if (registeredBuffers.TryGetValue(buildingId, out List<CapsuleBuffer> buffers) == false)
		{
			buffers = new List<CapsuleBuffer>();
			registeredBuffers.Add(buildingId, buffers);
		}

		if (buffers.Contains(facility) == false)
			buffers.Add(facility);
	}

	protected override void OnUnregisterFacility(uint buildingId, CapsuleBuffer facility)
	{
		if (registeredBuffers.TryGetValue(buildingId, out List<CapsuleBuffer> buffers) == false)
			return;

		buffers.Remove(facility);
		if (buffers.Count <= 0)
			registeredBuffers.Remove(buildingId);
	}
}
