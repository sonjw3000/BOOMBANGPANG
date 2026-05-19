using System.Collections.Generic;

public interface ICollectSupplySource
{
	IEnumerable<ShelfBase> GetSources(uint itemId);
}
