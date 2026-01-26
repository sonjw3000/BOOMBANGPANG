using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.UI;

public sealed class ShelfUIProvider : UIProvider<ShelfBase>
{
	[SerializeField] private Sprite icon;

	public override bool TryBuild(out SelectionModel model)
	{
		if (targetComponent == null)
		{
			model = null;
			return false;
		}

		List<InfoBlock> dataBlock = new List<InfoBlock>();
		dataBlock.Add(new KeyValueBlock("Capacity", targetComponent.MaxSize.ToString()));

		model = new SelectionModel
		{
			title = targetComponent.name,
			icon = this.icon,
			provider = this,
			blocks = dataBlock
		};

		return true;
	}
}
