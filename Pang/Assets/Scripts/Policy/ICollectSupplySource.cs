using System.Collections.Generic;

public interface ICollectSupplySource
{
	IEnumerable<ShelfBase> GetSources(uint itemId);
	IEnumerable<ShelfBase> GetSources(uint buildingId, uint itemId);
}
