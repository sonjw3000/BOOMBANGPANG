using BlackBoardSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class StringExtensions
{
	// 스트링값의 해쉬를 깔쌈하게 지정해줘서 스트링으로 딕셔너리를 만들어도 빠르게 해준다
	//https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function

	public static int TransformToFNV1aHash(this string str)
	{
		uint hash = 2166136261;
		foreach (char c in str)
		{
			hash = (hash ^ c) * 16777619;
		}
		return unchecked((int)hash);
	}
}

namespace BlackBoardSystem
{
	// 참고
	// https://www.youtube.com/watch?app=desktop&v=HNGJ8KOqdYQ
	[Serializable]
	public readonly struct BlackBoardKey<T>// : IEquatable<BlackBoardKey>
	{
		public readonly int hashedKey;
		public readonly string name;

		public BlackBoardKey(string name)
		{
			this.name = name;
			hashedKey = name.TransformToFNV1aHash();
		}

		public override int GetHashCode() => hashedKey;

		public bool Equals(BlackBoardKey<T> other) => hashedKey == other.hashedKey;

		public override bool Equals(object obj) => obj is BlackBoardKey<T> other && Equals(other);
		public override string ToString() => name;

		public static bool operator==(BlackBoardKey<T> left, BlackBoardKey<T> right) => left.hashedKey == right.hashedKey;
		public static bool operator!=(BlackBoardKey<T> left, BlackBoardKey<T> right) => !(left == right);
	}


	public interface IBlackboard
	{
		void Set<T>(BlackBoardKey<T> key, in T value);
		bool TryGet<T>(BlackBoardKey<T> key, out T value);
		bool Remove<T>(BlackBoardKey<T> key);
		void Clear();
	}
	
	public sealed class BlackBoard : IBlackboard
	{
		[SerializeField] private readonly Dictionary<Type, IStorage> _Tables = new();
		private interface IStorage { void Clear(); }

		private sealed class Table<T> : IStorage
		{
			private readonly Dictionary<int, T> _data = new();
			public void Set(int id, in T value) => _data[id] = value;
			public bool TryGet(int id, out T value)
			{
				if (_data.ContainsKey(id))
				{
					_data.TryGetValue(id, out value);
					return true;
				}

				value = default(T);
				return false;
			}
			public bool Remove(int id) => _data.Remove(id);
			public void Clear() => _data.Clear();
		}

		private Table<T> GetTable<T>()
		{
			var type = typeof(T);
			if (_Tables.TryGetValue(type, out var table) == false)
			{
				table = new Table<T>();
				_Tables.Add(type, table);
			}
			return (Table<T>)table;
		}

		public void Set<T>(BlackBoardKey<T> key, in T value) => GetTable<T>().Set(key.hashedKey, value);
		public bool TryGet<T>(BlackBoardKey<T> key, out T value) => GetTable<T>().TryGet(key.hashedKey, out value);
		public bool Remove<T>(BlackBoardKey<T> key) => GetTable<T>().Remove(key.hashedKey);
		public void Clear() { foreach(var t in _Tables.Values) t.Clear(); }

		public void Set<T>(string keyStr, in T value) => GetTable<T>().Set(keyStr.TransformToFNV1aHash(), value);
		public bool TryGet<T>(string keyStr, out T value) => GetTable<T>().TryGet(keyStr.TransformToFNV1aHash(), out value);
		public bool Remove<T>(string keyStr) => GetTable<T>().Remove(keyStr.TransformToFNV1aHash());
	}

	/*
	public sealed class LayeredBlackBoard : IBlackboard
	{
		private readonly IBlackboard Local;
		private readonly IBlackboard Global;

		public LayeredBlackBoard(IBlackboard local, IBlackboard global)
		{
			Local = local; Global = global;
		}

		public void Set<T>(BlackBoardKey<T> key, in T value) => Local.Set(key, in value);
		public bool TryGet<T>(BlackBoardKey<T> key, out T value) => Local.TryGet(key, out value) || Global.TryGet(key, out value);
		public bool TryGetGlobal<T>(BlackBoardKey<T> key, out T value) => Global.TryGet(key, out value);
		public bool Remove<T>(BlackBoardKey<T> key) => Local.Remove(key);
		public void Clear() => Local.Clear();

		public void Set<T>(string keyStr, in T value) => Local.Set<T>(new BlackBoardKey<T>(keyStr), value);
		public bool TryGet<T>(string keyStr, out T value) => Local.TryGet(new BlackBoardKey<T>(keyStr), out value) || Global.TryGet(new BlackBoardKey<T>(keyStr), out value);
		public void TryGetGlobal<T>(string keyStr, in T value) => Global.Set<T>(new BlackBoardKey<T>(keyStr), value);
		public bool Remove<T>(string keyStr) => Local.Remove(new BlackBoardKey<T>(keyStr));
	}
	*/
}

public struct BTContext
{
	public BlackBoard LocalBlackBoard;
	public BlackBoard GlobalBlackBoard;
	public AIWorker Worker;
	public float deltaTime;
}
