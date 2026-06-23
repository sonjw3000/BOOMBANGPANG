using UnityEngine;
using System.Collections.Generic;

public sealed class CapsuleBufferService : FacilityService<CapsuleBuffer>
{
	// capsule buffer management

	private readonly Dictionary<uint, List<CapsuleBuffer>> registeredBuffers = new();

	public bool SetBufferState(CapsuleBuffer facility, CapsuleBufferState newState)
	{
		if (facility == null)
			return false;

		facility.SetBufferState(newState);
		return true;
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
