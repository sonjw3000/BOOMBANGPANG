using UnityEngine;
using System.Collections.Generic;

public sealed class CapsuleBufferService : FacilityService<CapsuleBuffer>
{
	[SerializeField] private CargoCapsule capsulePrefab;

	// capsule management
	private readonly List<CargoCapsule> capsules = new();
	private readonly Dictionary<uint, CargoCapsule> capsulesByCapsuleId = new();
	private uint nextCapsuleId = 1;

	// capsule buffer management

	private readonly Dictionary<uint, List<CapsuleBuffer>> registeredBuffers = new();

	protected override void OnRegisterFacility(uint buildingId, CapsuleBuffer facility)
	{
		if (facility == null)
			return;

		if (!registeredBuffers.ContainsKey(buildingId))
		{
			registeredBuffers[buildingId] = new List<CapsuleBuffer>();
		}
		registeredBuffers[buildingId].Add(facility);
	}

	protected override void OnUnregisterFacility(uint buildingId, CapsuleBuffer facility) 
	{
		if (facility == null)
			return;
			
		if (registeredBuffers.ContainsKey(buildingId))
		{
			registeredBuffers[buildingId].Remove(facility);
		}
	}
}
