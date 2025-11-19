using System;
using System.Collections.Generic;

public class Order
{
	public int OrderID;
	public List<OrderLine> Lines;
	public DateTime DeadLine;
	public int Priority;
}

public class OrderLine
{
	public uint ItemID;
	public int Quantity;
}
