using System.Linq;

public class ToteBox : BoxBase
{

	protected override void UpdateSize()
	{
		size = stacks.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
	}

}
