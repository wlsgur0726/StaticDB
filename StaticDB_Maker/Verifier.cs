using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace StaticDB_Maker
{
	public struct Data
	{
		public enum Type
		{
			_UNKNOWN_,
			UINT,
			LONG,
			STR,
			DOUBLE,
		}
		public Type type;
		public object value;
		public string errmsg;

		public bool Err()
		{
			return type == Type._UNKNOWN_
				|| value == null
				|| errmsg.Length != 0;
		}

		public override string ToString()
		{
			return value.ToString();
		}
	}
	public delegate Data Parser(string src);

	class DefaultParser
	{
		public readonly static Parser Unknown = (string src) =>
		{
			Data data;
			data.type = Data.Type._UNKNOWN_;
			data.value = null;
			data.errmsg = "unknown parser";
			return data;
		};

		public readonly static Parser Long = (string src) =>
		{
			Data data;
			try {
				long num = long.Parse(src);
				data.type = Data.Type.LONG;
				data.value = num;
				data.errmsg = "";
			}
			catch (Exception e) {
				data.type = Data.Type._UNKNOWN_;
				data.value = null;
				data.errmsg = e.Message;
			}
			return data;
		};

		public readonly static Parser String = (string src) =>
		{
			Data data;
			data.type = Data.Type.STR;
			data.value = src==null ? "" : src;
			data.errmsg = "";
			return data;
		};

		public readonly static Parser Uint = (string src) =>
		{
			Data data;
			try {
				uint num = uint.Parse(src);
				data.type = Data.Type.UINT;
				data.value = num;
				data.errmsg = "";
			}
			catch (Exception e) {
				data.type = Data.Type._UNKNOWN_;
				data.value = null;
				data.errmsg = e.Message;
			}
			return data;
		};

		public readonly static Parser ID_INT = DefaultParser.Uint;
		public readonly static Parser ID_STR = (string src) =>
		{
			Data data = DefaultParser.String(src);
			if (data.Err())
				return data;
			data.errmsg = Common.CheckNamingRule((string)data.value);
			return data;
		};

		public readonly static Parser ID = (string src) =>
		{
			Data data = DefaultParser.ID_INT(src);
			if (data.Err())
				data = DefaultParser.String(src);
			return data;
		};
	}



	class TableVerifier
	{
		Table m_table;
		int m_columnCount = -1;
		List<bool> m_commentColumns = new List<bool>();


		public TableVerifier(Table table)
		{
			m_table = table;
			InitVerifyDelegateList();
		}

		public void LoadCsvFile()
		{
			string filePath = System.IO.Path.Combine(Config.DB_Path, m_table.m_name + ".csv");
			System.IO.TextReader reader = new System.IO.StreamReader(filePath, Encoding.UTF8);
			CsvParser csv = new CsvParser(reader);
			if (csv == null)
				throw new ParseError("fails to read CSV file, " + filePath);

			for (string[] src = csv.Read(); src != null; src = csv.Read()) {
				int row = m_table.m_records.Count + 1;
				if (row == 1) {
					foreach (string s in src)
						m_commentColumns.Add(Common.FindColumnType(s) == ColumnType.COMMENT);
				}

				Record record = new Record(row);
				bool emptyLine = true;
				for (int i=0; i<src.Length; ++i) {
					if (m_commentColumns[i])
						continue;
					src[i] = src[i].Trim();
					if (src[i].Length > 0) {
						emptyLine = false;
						break;
					}
				}
				if (emptyLine)
					break;
				for (int i=0; i<src.Length; ++i)
					record.Add(new Cell(row, i+1, m_commentColumns[i] ? "" : src[i]));

				// 공백 제거
				int dst = m_columnCount < 0 ? 0 : m_columnCount;
				while (record.Count > dst) {
					int last = record.Count;
					if (record[last].Source.Length > 0)
						break;
					record.Remove(last);
				}
				if (m_columnCount < 0)
					m_columnCount = record.Count;

				// 컬럼 수가 일관적인지 체크
				if (m_columnCount != record.Count) {
					string errmsg = String.Format(
						"unexpected column count, line:{0}, expect:{1}, count:{2}, {3}",
						row, m_columnCount, record.Count, filePath);
					throw new ParseError(errmsg);
				}
				m_table.m_records.Add(record);
			}

			if (m_table.m_records.Count < 2)
				throw new ParseError("invalid schema, " + filePath);
		}

		public bool ParseSchema(Table table)
		{
			bool fail = false;
			Record type_record = m_table.m_records[Config.ColumnTypeRow - 1];
			Record name_record = m_table.m_records[Config.ColumnNameRow - 1];
			for (int col=1; col<=m_columnCount; ++col) {
				if (m_commentColumns[col-1])
					continue;
				try {
					string[] type_tokens = type_record[col].Source.Split('.');
					string col_name = name_record[col].Source;
					if (type_tokens.Length == 0) {
						throw new ParseError("missing type");
					}
					TableSchema.Column col_info = null;
					ColumnType columnType = Common.FindColumnType(type_tokens[0]);
					switch (columnType) {
						case ColumnType.INT:
						case ColumnType.STR: {
							col_info = new Column_Nomal(columnType);
							break;
						}
						case ColumnType.ID: {
							if (type_tokens.Length <= 1)
								throw new ParseError("missing detail type");
							ColumnType detailType = Common.FindColumnType(type_tokens[1]);
							col_info = new Column_ID(detailType);
							break;
						}
						case ColumnType.REF: {
							if (type_tokens.Length <= 1)
								throw new ParseError("missing table name");
							else if (type_tokens.Length <= 2)
								col_info = new Column_REF(type_tokens[1]);
							else if (type_tokens.Length <= 3)
								col_info = new Column_REF(type_tokens[1], type_tokens[2]);
							break;
						}
						case ColumnType.GROUP: {
							if (type_tokens.Length <= 1)
								throw new ParseError("missing detail type");
							ColumnType detailType = Common.FindColumnType(type_tokens[1]);
							switch (detailType) {
								case ColumnType.INT:
								case ColumnType.STR: {
									col_info = new Column_GROUP(new Column_Nomal(detailType));
									break;
								}
								case ColumnType.REF: {
									if (type_tokens.Length <= 2)
										throw new ParseError("missing table name");
									col_info = new Column_GROUP(new Column_REF(type_tokens[2]));
									break;
								}
								default: {
									throw new ParseError("invalid detail type");
								}
							}
							break;
						}
						case ColumnType.ORDER: {
							if (type_tokens.Length <= 1)
								col_info = new Column_ORDER("");
							else
								col_info = new Column_ORDER(type_tokens[1]);
							break;
						}
						case ColumnType.RATIO: {
							if (type_tokens.Length <= 1)
								col_info = new Column_RATIO("");
							else
								col_info = new Column_RATIO(type_tokens[1]);
							break;
						}
						case ColumnType.RATE: {
							if (type_tokens.Length <= 1)
								throw new ParseError("missing denominator");
							long denominator = 0;
							long.TryParse(type_tokens[1], out denominator);
							col_info = new Column_RATE(denominator);
							break;
						}
						case ColumnType.COMMENT: {
							// ignore
							continue;
						}
						default: {
							throw new ParseError("invalid type " + type_tokens[0]);
						}
					} // switch
					if (columnType != ColumnType.ID)
						col_info.m_name = col_name;
					col_info.m_columnNumber = col;
					table.m_schema.RegisterColumn(col_info);
				} // try
				catch (ParseError e) {
					fail = true;
					Common.OnError(table.m_name, Config.ColumnTypeRow, col, e.Message);
				}
			} // for
			if (fail)
				return false;
			return table.m_schema.CheckSchema();
		}

		class VerifyObject
		{
			public delegate bool Delegate(Table table);
			public Delegate m_delegate;
			public Table.State m_target_state;
			public VerifyObject(Table.State s, Delegate d)
			{
				m_target_state = s;
				m_delegate = d;
			}
		}

		private List<VerifyObject> m_verify = new List<VerifyObject>();
		private void InitVerifyDelegateList()
		{
			// Step1 - 스키마 파싱
			m_verify.Add(new VerifyObject(Table.State.Step1_Verified_SchemaGrammar, (Table table) =>
			{
				LoadCsvFile();
				lock (table.m_lock) {
					return ParseSchema(table);
				}
			}));

			// Step2 - ID 인덱싱
			m_verify.Add(new VerifyObject(Table.State.Step2_Complete_ID_Indexing, (Table table) =>
			{
				lock (table.m_lock) {
					Column_ID column_ID_INT = (Column_ID)table.m_schema.FindColumn(Config.ColName_ID_INT);
					Column_ID column_ID_STR = (Column_ID)table.m_schema.FindColumn(Config.ColName_ID_STR);
					for (int row = Config.DataStartRow; row<=m_table.m_records.Count; ++row) {
						// ID.INT
						Record record = m_table.m_records[row - 1];
						uint ID_INT;
						if (column_ID_INT.m_columnNumber == 0) {
							ID_INT = table.m_schema.m_autoIncrID++;
						}
						else {
							int col = column_ID_INT.m_columnNumber;
							Cell cell = record[col];
							if (cell.Parse(column_ID_INT.Parse, table.m_lock) == false)
								throw new ParseError(table.m_name, row, col, cell.ParsedData.errmsg);
							ID_INT = (uint)cell.ParsedData.value;

							Record dup = table.m_records_byInt.Find(ID_INT);
							if (dup != null) {
								string errmsg = String.Format("duplicate ID.INT, row:{0}, ID:{1}", dup.Row, ID_INT);
								throw new ParseError(table.m_name, row, col, errmsg);
							}
						}
						record.ID_INT = ID_INT;
						table.m_records_byInt.Add(ID_INT, record);

						// ID.STR
						while (column_ID_STR != null) {
							int col = column_ID_STR.m_columnNumber;
							Cell cell = record[col];
							if (cell.Source.Length == 0)
								break;
							if (cell.Parse(column_ID_STR.Parse, table.m_lock) == false)
								throw new ParseError(table.m_name, row, col, cell.ParsedData.errmsg);
							string str = (string)cell.ParsedData.value;

							Record dup = table.m_records_byStr.Find(str);
							if (dup != null) {
								string errmsg = String.Format("duplicate ID.STR, row:{0}, ID:{1}", dup.Row, str);
								throw new ParseError(table.m_name, row, col, errmsg);
							}
							record.ID_STR = str;
							table.m_records_byStr.Add(str, record);
							break;
						}
					}
				}
				return true;
			}));

			// Step3 - 참조하는 테이블과 컬럼이 실존하는지 검증
			m_verify.Add(new VerifyObject(Table.State.Step3_Verified_Referance, (Table table) =>
			{
				foreach (var column in table.m_schema.m_columns) {
					int col = column.m_columnNumber;
					Column_REF refinfo = Common.GetRefInfo(column);
					if (refinfo == null)
						continue;

					ReferenceFinder reference = new ReferenceFinder();
					reference.m_startTableName = table.m_name;
					reference.m_targetTableName = refinfo.m_refTable;
					reference.m_targetColumnName = refinfo.m_refColumn;
					string err = reference.Find();
					if (err.Length > 0)
						throw new ParseError(table.m_name, Config.ColumnTypeRow, col, err);
					column.LangType = reference.m_lastFindResult.column.LangType;
				}
				return true;
			}));

			// Step4 - 참조데이터를 제외하고 검증
			m_verify.Add(new VerifyObject(Table.State.Step4_Verified_NomalData, (Table table) =>
			{
				bool success = true;
				foreach (var column in table.m_schema.m_columns) {
					int col = column.m_columnNumber;
					if (column.m_type == ColumnType.ID)
						continue; // Step2 에서 이미 했음
					if (Common.GetRefInfo(column) != null)
						continue;

					for (int i = Config.DataStartRow-1; i<m_table.m_records.Count; ++i) {
						Record record = m_table.m_records[i];
						Cell cell = record[col];
						if (cell.Parse(column.Parse, table.m_lock) == false) {
							success = false;
							Common.OnError(table.m_name, record.Row, col, cell.ParsedData.errmsg);
						}
					}
				}
				return success;
			}));

			// Step5 - 참조데이터 끌어오며 검증
			m_verify.Add(new VerifyObject(Table.State.Step5_Verified_ReferanceData, (Table table) =>
			{
				bool success = true;
				foreach (var column in table.m_schema.m_columns) {
					int col = column.m_columnNumber;
					if (column.m_type == ColumnType.ID)
						continue; // Step2 에서 이미 했음
					if (Common.GetRefInfo(column) == null)
						continue; // Step4 에서 이미 했음

					for (int i = Config.DataStartRow-1; i<m_table.m_records.Count; ++i) {
						Record record = m_table.m_records[i];
						Cell cell = record[col];
						if (cell.Parse(column.Parse, table.m_lock) == false) {
							success = false;
							Common.OnError(table.m_name, record.Row, col, cell.ParsedData.errmsg);
						}
					}
				}
				return success;
			}));

			// 검증 완료
			m_verify.Add(new VerifyObject(Table.State.Step6_VerifyComplete, (Table table) =>
			{
				return true;
			}));
		}

		public bool Verify(Table.State state)
		{
			for (int i=0; i<(int)state; ++i) {
				VerifyObject verify = m_verify[i];
				bool wait = false;
				lock (m_table.m_lock) {
					if (m_table.m_state >= verify.m_target_state)
						continue;
					int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
					if (tid == m_table.m_worker)
						throw new Exception("self-deadlock");
					else if (m_table.m_worker == 0)
						m_table.m_worker = tid;
					else
						wait = true;
				}
				if (wait) {
					for (;;) {
						lock (m_table.m_lock) {
							if (m_table.m_state >= verify.m_target_state)
								break;
							if (m_table.m_worker == 0)
								return false; // 다른 쓰레드에서 시도했으나 실패
						}
						System.Threading.Thread.Sleep(10);
					}
					continue;
				}

				bool success = false;
				try {
					success = verify.m_delegate(m_table);
				}
				catch (ParseError e) {
					Common.OnError(e.Message);
				}
				catch (Exception e) {
					Common.OnError(e.ToString());
				}
				finally {
					lock (m_table.m_lock) {
						if (success)
							m_table.m_state = verify.m_target_state;
						m_table.m_worker = 0;
					}
				}
				if (success == false)
					return false;
			} // for
			return true;
		}
	}


	class Verifier
	{
		public static Verifier s_instance = new Verifier();
		public object m_lock = new object();
		public Dictionary<string, TableVerifier> m_list = new Dictionary<string, TableVerifier>();

		public TableVerifier FindTableVerifier(string table_name)
		{
			lock (m_lock) {
				TableVerifier v;
				if (m_list.TryGetValue(table_name, out v))
					return v;
				Table table = DB.s_instance.FindTable(table_name);
				if (table != null) {
					v = new TableVerifier(table);
					m_list.Add(table_name, v);
					return v;
				}
			}
			return null;
		}
	}
}
