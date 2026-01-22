using System.Collections.Generic;
using UnityEngine;

public abstract class InfoBlock
{
	public Sprite LineIcon = null;
};

public abstract class SelectionAction
{

}



public class KeyValueBlock : InfoBlock
{
	public readonly string Key;
	public readonly string Value;

	public KeyValueBlock(string key, string value)
	{
		Key = key;
		Value = value;
	}
}

public class ProgressBlock : InfoBlock
{
	public readonly string Label;
	public readonly float Normalized;
	public readonly string Text;

	public ProgressBlock(string label, float normalized01, string text)
	{
		Label = label;
		Normalized = normalized01;
		Text = text;
	}
}

public class ListBlock : InfoBlock
{
	public readonly string Title;
	public IReadOnlyList<string> Lines;

	public ListBlock(string title, IReadOnlyList<string> lines)
	{
		Title = title;
		Lines = lines;
	}
}

public class ItemCellView
{
	public readonly string ItemID;
	public readonly float Percentage;
	public readonly int ItemMount;

	public ItemCellView(string itemID, float percentage, int itemMount)
	{
		ItemID = itemID;
		Percentage = percentage;
		ItemMount = itemMount;
	}
}


public class ItemGridBlock : InfoBlock
{
	public readonly string Title;
	public IReadOnlyList<ItemCellView> Cells;

	public ItemGridBlock(string title, IReadOnlyList<ItemCellView> cells)
	{
		Title = title;
		Cells = cells;
	}
}