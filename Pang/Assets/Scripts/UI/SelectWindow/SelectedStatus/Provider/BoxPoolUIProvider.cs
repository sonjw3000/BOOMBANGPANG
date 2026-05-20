using System.Collections.Generic;
using UnityEngine;

public class BoxPoolUIProvider : UIProvider<BoxPool>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Shelf";
    public override string Subtitle => "Box Pool";
	public override Sprite Icon => null; // Placeholder for shelf icon

	public int CurrentBoxCount => currentTarget != null ? currentTarget.CurrentBoxCount : 0;
	public int MaxBoxCount => currentTarget != null ? currentTarget.MaxStackCount : 0;

	public IEnumerable<string> GetBoxSummaries()
	{
		if (currentTarget?.Boxes == null)
			yield break;

		foreach (BoxBase box in currentTarget.Boxes)
		{
			if (box == null)
				continue;

			float fillPercent = box.MaxSize <= 0.0f ? 0.0f : (box.TotalSize / box.MaxSize) * 100.0f;
			yield return $"Box #{box.BoxId} / {box.Type} / {fillPercent:0.0}%";
		}
	}

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Current Boxes", $"{CurrentBoxCount}"));
		infoBlocks.Add(new KeyValueBlock("Max Boxes", $"{MaxBoxCount}"));
	}

	public override void OnUpdate()
	{
		(infoBlocks[0] as KeyValueBlock)?.UpdateValue($"{CurrentBoxCount}");
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue($"{MaxBoxCount}");
	}

}
