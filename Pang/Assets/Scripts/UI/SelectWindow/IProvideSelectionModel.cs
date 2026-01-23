using System.Collections.Generic;
using UnityEngine;


public sealed class SelectionModel
{
	public string title;
	public string subtitle;
	public Sprite icon;

	public readonly List<InfoBlock> blocks = new();
	public readonly List<SelectionAction> actions = new();
}

