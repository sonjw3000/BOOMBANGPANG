using UnityEngine;

public partial class BoxPool
{
	public BoxPoolSaveData CaptureState()
	{
		BoxPoolSaveData data = new();
		foreach (var box in boxes)
		{
			if (box != null)
			{
				data.Boxes.Add(new BoxReferenceSaveData
				{
					BoxType = box.Type,
					BoxId = box.BoxId,
				});
			}
		}

		return data;
	}

	public void RestoreState(BoxPoolSaveData data)
	{
		boxes.Clear();
		if (data == null)
			return;

		for (int i = data.Boxes.Count - 1; i >= 0; i--)
		{
			BoxReferenceSaveData boxRef = data.Boxes[i];
			if (boxRef != null && GameContext.Instance.BoxMgr.TryGetBox(boxRef.BoxType, boxRef.BoxId, out var box))
				PutBox(box);
		}
	}
}
