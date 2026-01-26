using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;


public sealed class SelectionModel
{
	// base info
	public string title;
	public string subtitle;
	public Sprite icon;

	public UIProviderBase provider;

	// info blocks
	public List<InfoBlock> blocks = new List<InfoBlock>();
}

