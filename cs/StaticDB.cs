using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace StaticDB
{
	public interface TableInterface
	{
		string GetTableFileName();
		void Init(FlatBuffers.ByteBuffer buffer);
		void OnLoaded1(TableInterface[] tables);
		void OnLoaded2(TableInterface[] tables);
	}



	public class Weight<RECORD>
	{
		private static Random s_seed = new Random();
		public Weight()
		{
			lock (s_seed) {
				m_random = new Random(s_seed.Next());
			}
		}

		public void Add(uint weight, RECORD record)
		{
			if (weight == 0)
				return;
			m_total += weight;
			if (m_total >= int.MaxValue)
				throw new OverflowException();
			Candidate e;
			e.Bound = m_total;
			e.Record = record;
			m_candidate.Add(e);
		}

		public RECORD Pick
		{
			get
			{
				uint r = (uint)m_random.Next(1, (int)(m_total+1));
				int min = 0;
				int max = m_candidate.Count - 1;
				while (min <= max) {
					int i = (max - min) / 2 + min;
					uint w = m_candidate[i].Bound;
					if (r > w) {
						if (r <= m_candidate[i+1].Bound)
							return m_candidate[i+1].Record;
						min = i + 2;
					}
					else if (r < w) {
						if (i == 0)
							return m_candidate[i].Record;
						if (r > m_candidate[i-1].Bound)
							return m_candidate[i].Record;
						max = i - 1;
					}
					else {
						return m_candidate[i].Record;
					}
				}
				return default(RECORD);
			}
		}

		public uint Total { get { return m_total; } }

		private struct Candidate
		{
			public uint Bound;
			public RECORD Record;
		}
		private List<Candidate> m_candidate = new List<Candidate>();
		private uint m_total = 0;
		private Random m_random;
	}



	public abstract class Table<FBS_DATA, RECORD> 
		: TableInterface
		, IEnumerable<RECORD>
	{
		public abstract string GetTableFileName();
		public abstract void Init(FlatBuffers.ByteBuffer buffer);
		public abstract void OnLoaded1(TableInterface[] tables);
		public abstract void OnLoaded2(TableInterface[] tables);

		public RECORD GetRecord(uint ID) { return m_ID[ID]; }
		public RECORD this[uint ID] { get { return m_ID[ID]; } }

		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
		public IEnumerator<RECORD> GetEnumerator()
		{
			var it = m_ID.GetEnumerator();
			while (it.MoveNext())
				yield return it.Current.Value;
		}

		protected FlatBuffers.ByteBuffer m_buffer = null;
		protected FBS_DATA m_data = default(FBS_DATA);
		protected Dictionary<uint, RECORD> m_ID = new Dictionary<uint, RECORD>();
	}



	public class DB
	{
		protected bool InitTables(string dir, TableInterface[] tables)
		{
			bool[] fail = new bool[tables.Length];

			for (int i=0; i<tables.Length; ++i) {
				try {
					var table = tables[i];
					byte[] data = File.ReadAllBytes(Path.Combine(dir, table.GetTableFileName()));
					table.Init(new FlatBuffers.ByteBuffer(data));
				}
				catch (Exception e) {
					fail[i] = true;
					OnError(e);
				}
			}

			for (int i=0; i<tables.Length; ++i) {
				try {
					if (fail[i])
						continue;
					tables[i].OnLoaded1(tables);
				}
				catch (Exception e) {
					fail[i] = true;
					OnError(e);
				}
			}

			for (int i = 0; i<tables.Length; ++i) {
				try {
					if (fail[i])
						continue;
					tables[i].OnLoaded2(tables);
				}
				catch (Exception e) {
					fail[i] = true;
					OnError(e);
				}
			}

			foreach (bool f in fail) {
				if (f)
					return false;
			}
			m_tables = tables;
			return OnInitComplete();
		}

		protected virtual void OnError(Exception e)
		{
			Console.Error.WriteLine(e.ToString());
		}

		public delegate bool OnInitCompleteDelegate();
		protected OnInitCompleteDelegate OnInitComplete = ()=>
		{
			// if (verify == fail)
			//    return false;
			// return true;
			return true;
		};

		protected TableInterface[] m_tables;
	}
}
