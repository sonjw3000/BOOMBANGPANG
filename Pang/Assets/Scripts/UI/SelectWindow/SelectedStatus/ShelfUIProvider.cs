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

		model = new SelectionModel
		{
			title = targetComponent.name,
			icon = this.icon,
		};

		return true;
	}
}
