using Unity.Mathematics;
using UnityEngine;

public partial class Rocket
{
	public RocketSaveData CaptureState()
	{
		return new RocketSaveData
		{
			State = state,
			LandingPoint = new Int3SaveData(landingPoint.x, landingPoint.y, landingPoint.z),
			FallingSpeed = fallingSpeed,
			LaunchSpeed = launchSpeed,
			LaunchHeight = launchHeight,
			WorldPosition = new Vector3SaveData(transform.position.x, transform.position.y, transform.position.z),
			ForwardVector = new Vector3SaveData(forwardVector.x, forwardVector.y, forwardVector.z),
			DockedCapsule = DockedCapsule != null
				? new BoxReferenceSaveData
				{
					BoxType = DockedCapsule.Type,
					BoxId = DockedCapsule.BoxId,
				}
				: null,
		};
	}

	public void RestoreState(RocketSaveData data)
	{
		if (data == null)
			return;

		state = data.State;
		landingPoint = new int3(data.LandingPoint.X, data.LandingPoint.Y, data.LandingPoint.Z);
		fallingSpeed = data.FallingSpeed;
		launchSpeed = data.LaunchSpeed;
		launchHeight = data.LaunchHeight;
		transform.position = new Vector3(data.WorldPosition.X, data.WorldPosition.Y, data.WorldPosition.Z);
		forwardVector = new Vector3(data.ForwardVector.X, data.ForwardVector.Y, data.ForwardVector.Z);

		if (data.DockedCapsule != null &&
			DockedCapsule == null &&
			GameContext.Instance.BoxMgr.TryGetBox(data.DockedCapsule.BoxType, data.DockedCapsule.BoxId, out var box) &&
			box is CargoCapsule capsule)
		{
			TryDockCapsule(capsule);
		}
	}
}
