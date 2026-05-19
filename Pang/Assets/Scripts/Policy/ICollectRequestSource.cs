using System.Collections.Generic;

public interface ICollectRequestSource<TRequestLine>
{
	IEnumerable<uint> GetRequestedItemIds();
	IEnumerable<TRequestLine> GetRequestLines(uint itemId);

	int GetAllocatableQuantity(TRequestLine requestLine);
	int Allocate(TRequestLine requestLine, int quantity);
	
	WorkLine CreateWorkLine(ShelfBase source, uint itemId, int quantity, TRequestLine requestLine);
}
