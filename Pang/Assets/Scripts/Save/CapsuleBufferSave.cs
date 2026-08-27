using UnityEngine;

public partial class CapsuleBuffer
{
	public CapsuleBufferSaveData CaptureState()
	{
		CapsuleBufferSaveData data = new()
		{
			RetainEmptyCapsule = RetainEmptyCapsule,
		};
		if (DockedCapsule != null)
		{
			data.Box = new BoxReferenceSaveData
			{
				BoxType = DockedCapsule.Type,
				BoxId = DockedCapsule.BoxId,
			};
		}

		return data;
	}

	public void RestoreState(CapsuleBufferSaveData data)
	{
		if (data == null)
			return;

		SetRetainEmptyCapsule(data.RetainEmptyCapsule);

		if (data.Box != null)
		{
			if (GameContext.Instance.BoxMgr.TryGetBox(data.Box.BoxType, data.Box.BoxId, out var box))
				PutBox(box);
		}
	}
}
