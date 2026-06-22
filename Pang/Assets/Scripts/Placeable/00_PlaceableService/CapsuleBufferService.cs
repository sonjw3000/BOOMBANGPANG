using UnityEngine;
using System.Collections.Generic;

public sealed class CapsuleBufferService : FacilityService<CapsuleBuffer>
{
	// capsule buffer management

	private readonly Dictionary<uint, List<CapsuleBuffer>> registeredBuffers = new();

	protected override void OnRegisterFacility(uint buildingId, CapsuleBuffer facility)
	{

	}

	protected override void OnUnregisterFacility(uint buildingId, CapsuleBuffer facility)
	{
		
	}
}
