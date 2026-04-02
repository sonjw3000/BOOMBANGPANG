
public interface IItemHandleable
{
	// return true if successfully put item
	public bool MoveToBox(BoxBase item);
	// return true if successfully get item
	public bool BringFromBox(BoxBase item);
}

