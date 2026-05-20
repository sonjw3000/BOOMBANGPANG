using TMPro;

public class RocketDetailContent : ShelfBaseDetailContent<Rocket>
{
	private TextMeshProUGUI stateValue;

	protected override void RefreshExtraInfo(IShelfBaseUIProvider shelfProvider)
	{
		base.RefreshExtraInfo(shelfProvider);

		if (provider is not RocketUIProvider rocketProvider || rocketProvider.Target == null)
			return;

		stateValue ??= AddInfoLine("State");
		stateValue.text = rocketProvider.Target.State.ToString();
	}
}
