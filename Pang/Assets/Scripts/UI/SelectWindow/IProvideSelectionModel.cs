using System.Collections.Generic;
using UnityEngine;


public interface IProvideSelectionModel
{



}

public sealed class SelectionModel
{
	public string title;
	public Sprite icon;

	public readonly List<InfoBlock> blocks = new();
	public readonly List<SelectionAction> actions = new();
}

