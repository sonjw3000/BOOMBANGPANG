using System;
using System.Collections.Generic;

public class ArrayHeap<T>
{
	private readonly List<T> heap;
	private readonly Comparison<T> comparison;

	// update new index when swap
	private readonly Action<T, int> onIndexSwapped;
	public int Count => heap.Count;

	public ArrayHeap(Comparison<T> comparison, Action<T, int> onIndexSwapped = null)
	{
		this.comparison = comparison;
		this.onIndexSwapped = onIndexSwapped;
		this.heap = new List<T>();
	}

	public void Reset()
	{
		heap.Clear();
	}

	private void SwapItem(int indexA, int indexB)
	{
		T temp = heap[indexA];
		heap[indexA] = heap[indexB];
		heap[indexB] = temp;

		onIndexSwapped?.Invoke(heap[indexA], indexB);
		onIndexSwapped?.Invoke(heap[indexB], indexA);
	}

	public void Push(T item)
	{
		heap.Add(item);
		int currentIndex = heap.Count - 1;
		while (currentIndex > 0)
		{
			int parentIndex = (currentIndex - 1) / 2;
			if (comparison(heap[currentIndex], heap[parentIndex]) < 0)
			{
				SwapItem(currentIndex, parentIndex);
				currentIndex = parentIndex;
			}
			else
			{
				break;
			}
		}
	}

	public bool Peek(out T item)
	{
		if (heap.Count == 0)
		{
			item = default;
			return false;
		}
		item = heap[0];
		return true;
	}

	public bool Pop(out T item)
	{
		if (heap.Count == 0)
		{
			item = default;
			return false;
		}
		
		item = heap[0];

		T lastItem = heap[heap.Count - 1];
		heap.RemoveAt(heap.Count - 1);

		if (heap.Count > 0)
		{
			heap[0] = lastItem;
			int currentIndex = 0;

			while (true)
			{
				int leftChildIndex = 2 * currentIndex + 1;
				int rightChildIndex = 2 * currentIndex + 2;
				int smallestIndex = currentIndex;
				if (leftChildIndex < heap.Count && comparison(heap[leftChildIndex], heap[smallestIndex]) < 0)
				{
					smallestIndex = leftChildIndex;
				}
				if (rightChildIndex < heap.Count && comparison(heap[rightChildIndex], heap[smallestIndex]) < 0)
				{
					smallestIndex = rightChildIndex;
				}
				if (smallestIndex != currentIndex)
				{
					SwapItem(currentIndex, smallestIndex);
					currentIndex = smallestIndex;
				}
				else
				{
					break;
				}
			}
		}

		return true;
	}

	public bool DecreaseKey(int index)
	{
		if (index < 0 || index >= heap.Count)
		{
			return false;
		}

		int currentIndex = index;
		while (currentIndex > 0)
		{
			int parentIndex = (currentIndex - 1) / 2;
			if (comparison(heap[currentIndex], heap[parentIndex]) < 0)
			{
				SwapItem(currentIndex, parentIndex);
				currentIndex = parentIndex;
			}
			else
			{
				break;
			}
		}

		return true;
	}

}