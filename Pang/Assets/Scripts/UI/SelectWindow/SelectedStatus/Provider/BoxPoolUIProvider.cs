using System.Collections.Generic;
using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public class BoxPoolUIProvider : UIProvider<BoxPool>, ISelectionInspectorProvider
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

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Boxes", GetBoxesVersion, BuildBoxesPanel);
		model.AddOverview("Current Boxes", () => CurrentBoxCount.ToString());
		model.AddOverview("Max Boxes", () => MaxBoxCount.ToString());
		model.AddOverview("Available", () => Mathf.Max(0, MaxBoxCount - CurrentBoxCount).ToString());
		model.AddAction("Add Personal Box", AddPersonalBox, CanAddPersonalBox);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}

	private int GetBoxesVersion()
	{
		unchecked
		{
			int version = CurrentBoxCount;
			if (currentTarget?.Boxes == null) return version;
			foreach (BoxBase box in currentTarget.Boxes)
			{
				if (box == null) continue;
				version = version * 31 + (int)box.BoxId;
				version = version * 31 + SelectionDetailContentUtility.GetItemContainerVersion(box);
			}
			return version;
		}
	}

	private SelectionDetailPanelModel BuildBoxesPanel()
	{
		SelectionDetailPanelModel panel = new()
		{
			Title = "BOXES",
			Summary = $"{CurrentBoxCount} / {MaxBoxCount} slots",
		};
		if (currentTarget?.Boxes == null) return panel;
		foreach (BoxBase box in currentTarget.Boxes)
		{
			if (box == null) continue;
			float filled = box.MaxSize <= 0.0f ? 0.0f : box.TotalSize / box.MaxSize * 100.0f;
			string secondary = $"Filled {filled:0.0}% · {box.TotalSize:0.0} / {box.MaxSize:0.0} units";
			if (ItemContainerDisplayUtility.CanDisplayTemperature)
				secondary += $" · {box.CurrentTemperatureCelsius:0.0} °C";
			panel.Rows.Add(new SelectionDetailRow
			{
				Primary = $"Box #{box.BoxId}",
				Trailing = box.Type.ToString(),
				Secondary = secondary,
			});
		}
		return panel;
	}

	private bool CanAddPersonalBox() => currentTarget != null && currentTarget.CanPutBox() && GameContext.HasInstance && GameContext.Instance.BoxMgr != null;

	private void AddPersonalBox()
	{
		if (CanAddPersonalBox() == false) return;
		BoxManager boxManager = GameContext.Instance.BoxMgr;
		if (boxManager.GetNewBox(BoxType.Personal, out BoxBase box) && currentTarget.PutBox(box) == false)
			boxManager.DisableBox(box);
	}

}
