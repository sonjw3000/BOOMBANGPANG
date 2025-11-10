using System;
using System.Collections.Generic;

public class CustomQueue<T, CMP> where CMP : IComparable<CMP>
{
	T[] data;
	CMP[] dataCtrl;

	public int Count { get; private set; } = 0;
	public int Capacity { get; private set; } = 1;

	public CustomQueue()
	{
		data = new T[Capacity];
		dataCtrl = new CMP[Capacity];
	}

	public CustomQueue(int capacity)
	{
		Capacity = capacity;
		data = new T[Capacity];
		dataCtrl = new CMP[Capacity];
	}

	public void Enqueue(T value, CMP cmp)
	{
		if (Count >= Capacity)
			Expand();

		data[Count++] = value;

		int now = Count - 1;
		while (now > 0)
		{
			int parent = (now - 1) / 2;
			if (dataCtrl[now].CompareTo(dataCtrl[parent]) < 0)
				break;

			T temp = data[now];
			data[now] = data[parent];
			data[parent] = temp;

			CMP tempCtrl = dataCtrl[now];
			dataCtrl[now] = dataCtrl[parent];
			dataCtrl[parent] = tempCtrl;

			now = parent;
		}
	}

	public T Dequeue()
	{
		if (Count == 0)
			throw new IndexOutOfRangeException();

		T result = data[0];
		data[0] = data[Count - 1];
		data[Count - 1] = default(T);

		dataCtrl[0] = dataCtrl[Count - 1];
		dataCtrl[Count - 1] = default(CMP);

		--Count;
		int now = 0;

		while (now < Count)
		{
			int left = (now * 2) + 1;
			int right = (now * 2) + 2;

			int next = now;

			if (left < Count && dataCtrl[next].CompareTo(dataCtrl[left]) < 0)
				next = left;
			if (right < Count && dataCtrl[next].CompareTo(dataCtrl[right]) < 0)
				next = right;

			if (next == now)
				break;

			T temp = data[now];
			data[now] = data[next];
			data[next] = temp;

			CMP tempCtrl = dataCtrl[now];
			dataCtrl[now] = dataCtrl[next];
			dataCtrl[next] = tempCtrl;

			now = next;
		}

		return result;
	}

	public T Peek()
	{
		if (Count == 0)
			throw new IndexOutOfRangeException();

		return data[0];
	}

	private void Expand()
	{
		T[] newData = new T[Capacity * 2];
		for(int i = 0; i < Count; ++i) 
			newData[i] = data[i];
		data = newData;
		Capacity *= 2;
	}
}