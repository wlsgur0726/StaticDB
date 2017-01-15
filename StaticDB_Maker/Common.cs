﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaticDB_Maker
{
	class Config
	{
		public static readonly int ColumnTypeRow = 1;
		public static readonly int ColumnNameRow = 2;
		public static readonly int DataStartRow = 3;

		public static readonly string ColName_ID_INT = "ID_INT";
		public static readonly string ColName_ID_STR = "ID_STR";

		public static readonly string AutoGenComment = "automatically generated by the StaticDB_Maker, do not modify";

		public static string Temp_Path = Path.Combine(
			Path.GetTempPath(),
			"StaticDB",
			String.Format("{0}_{1:D4}", DateTime.Now.Ticks, (new Random()).Next()%10000));

		public static string flatc_Path = "";
		public static string DB_Path = "";
		public static string Out_BIN_Path = "";
		public static string Out_FBS_Path = "";
		public static string Out_CPP_Path = "";
		public static string Out_CS_Path = "";
		public static string Out_JS_Path = "";
		public static HashSet<string> Target_Table = new HashSet<string>();
		public static Dictionary<string, int> TableID = new Dictionary<string, int>();

		public static string Namespace = "";
	}


	enum ColumnType
	{
		_UNKNOWN_,
		INT,
		STR,
		ID,
		REF,
		GROUP,
		ORDER,
		WEIGHT,
		RATE,
		COMMENT,
	}


	class Common
	{
		public static object s_lock = new object();

		public delegate void OnErrorDelegate(string msg);
		public static OnErrorDelegate s_onError = (string msg) =>
		{
			Console.Error.WriteLine(msg);
		};

		public static void OnError(string msg)
		{
			s_onError(msg);
		}

		public static void OnError(string table, int row, int col, string msg)
		{
			s_onError(ErrMsg(table, row, col, msg));
		}

		public static string ErrMsg(string table, int row, int col, string msg)
		{
			return String.Format("[table:{0}/row:{1}/col:{2}] {3}", table, row, col, msg);
		}

		delegate bool CheckChar(char c);
		public static string CheckNamingRule(string name)
		{
			if (name.Length == 0)
				return "empty name";
			CheckChar isAlphabet = (char c) => { return (c>='a' && c<='z') || (c>='A' && c<='Z'); };
			CheckChar isNumber = (char c) => { return (c>='0' && c<='9'); };
			CheckChar isUnderbar = (char c) => { return c == '_'; };
			if (!isAlphabet(name[0]))
				return "invalid first character, " + name;
			for (int i = 1; i<name.Length; ++i) {
				char c = name[i];
				if (!isAlphabet(c) && !isNumber(c) && !isUnderbar(c))
					return String.Format("invalid character, {0}[{1}]:{2}", name, i, c);
			}
			return "";
		}

		private static Dictionary<string, ColumnType> s_Str2ColumnType = null;
		public static ColumnType FindColumnType(string str)
		{
			if (s_Str2ColumnType == null) {
				Dictionary<string, ColumnType> map = new Dictionary<string, ColumnType>();
				map.Add("INT",		ColumnType.INT);
				map.Add("STR",		ColumnType.STR);
				map.Add("ID",		ColumnType.ID);
				map.Add("REF",		ColumnType.REF);
				map.Add("GROUP",	ColumnType.GROUP);
				map.Add("WEIGHT",	ColumnType.WEIGHT);
				map.Add("ORDER",	ColumnType.ORDER);
				map.Add("RATE",		ColumnType.RATE);
				map.Add("COMMENT",	ColumnType.COMMENT);
				lock (s_lock) {
					if (s_Str2ColumnType == null)
						s_Str2ColumnType = map;
				}
			}
			ColumnType ret;
			if (s_Str2ColumnType.TryGetValue(str, out ret))
				return ret;
			return ColumnType._UNKNOWN_;
		}

		public static Column_REF GetRefInfo(TableSchema.Column column)
		{
			switch (column.m_type) {
				case ColumnType.REF: {
					return (Column_REF)column;
				}
				case ColumnType.GROUP: {
					Column_GROUP cast = (Column_GROUP)column;
					if (cast.m_detailType.m_type == ColumnType.REF)
						return (Column_REF)cast.m_detailType;
					break;
				}
			}
			return null;
		}

		public static string EnumName(string table, string name)
		{
			return table + '_' + name;
		}

		public static ProcessStartInfo DefaultPSI()
		{
			ProcessStartInfo psi = new ProcessStartInfo();
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			return psi;
		}
	}

	class ParseError : Exception
	{
		public ParseError(string msg) : base(msg) { }
		public ParseError(string table, int row, int col, string msg) : base(Common.ErrMsg(table, row, col, msg)) { }
	}


	class ReferenceFinder
	{
		public Table m_startTable = null;
		public string m_targetTableName = "";
		public string m_targetColumnName = "";
		public OnFindArgs m_lastFindResult;

		public struct OnFindArgs
		{
			public Table table;
			public TableSchema.Column column;
			public TableVerifier verifier;
			public int number;
			public bool isLast;
		}
		public delegate void OnFindDelegate(OnFindArgs args);
		public OnFindDelegate OnFind = (OnFindArgs args) => { };

		public string Find()
		{
			List<string> refLog = new List<string>();
			HashSet<string> refLogFinder = new HashSet<string>();
			string table_name = m_targetTableName;
			string column_name = m_targetColumnName;

			try {
				bool loop = true;
				for (int i = 1; loop; ++i) {
					loop = false;
					string refkey = table_name + '/' + column_name;
					refLog.Add(refkey);
					if (refLogFinder.Add(refkey) == false) {
						throw new ParseError("detected recursive reference");
					}
					Table table = DB.s_instance.FindTable(table_name);
					if (table == null) {
						throw new ParseError(table_name + " does not exists");
					}
					TableVerifier verifier = Verifier.s_instance.FindTableVerifier(table_name);
					if (false == verifier.Verify(Table.State.Step2_Complete_ID_Indexing)) {
						throw new ParseError("fail verify, table:" + table_name);
					}
					var column = table.m_schema.FindColumn(column_name);
					if (column == null) {
						if (column_name.Length > 0) {
							throw new ParseError(String.Format("{0} column does not exists in {1} table",
								column_name, table_name));
						}
						column = table.m_schema.FindColumn(Config.ColName_ID_INT);
					}
					else if (column.m_type == ColumnType.ID) {
						Column_ID cast = (Column_ID)column;
						if (cast.m_detailType == ColumnType.STR)
							column = table.m_schema.FindColumn(Config.ColName_ID_INT);
					}
					else {
						Column_REF next_ref = Common.GetRefInfo(column);
						if (next_ref != null) {
							table_name = next_ref.m_refTable;
							column_name = next_ref.m_refColumn;
							loop = true;
						}
					}
					m_startTable.m_refTables.Add(table.m_name);
					m_lastFindResult.table = table;
					m_lastFindResult.column = column;
					m_lastFindResult.verifier = verifier;
					m_lastFindResult.number = i;
					m_lastFindResult.isLast = !loop;
					OnFind(m_lastFindResult);
				} // for
			} // try
			catch (ParseError e) {
				string err = e.Message + "\n  " + (m_startTable==null ? "" : m_startTable.m_name);
				foreach (var s in refLog)
					err += " -> " + s;
				return err;
			}
			return "";
		}
	}

	class PullReferenceData : ReferenceFinder
	{
		public Data m_ID;
		public Data m_result = DefaultParser.Unknown("");
		public PullReferenceData()
		{
			OnFind = (ReferenceFinder.OnFindArgs args) =>
			{
				if (args.column.m_type == ColumnType.GROUP) {
					args.verifier.Verify(Table.State.Step5_Verified_GroupReferance);
					var groups = ((Column_GROUP)args.column).m_groups;
					uint groupID = 0;
					bool isValidGroupID = false;
					switch (m_ID.type) {
						case Data.Type.UINT: {
							groupID = (uint)m_ID.value;
							isValidGroupID = groups.Contains(groupID);
							break;
						}
						case Data.Type.STR: {
							string enum_name;
							var next_ref = Common.GetRefInfo(args.column);
							if (next_ref != null)
								enum_name = Common.EnumName(next_ref.m_refTable, "ID");
							else
								enum_name = Common.EnumName(args.table.m_name, args.column.m_name);
							EnumInfo ei;
							if (EnumInfo.Enums.TryGetValue(enum_name, out ei)) {
								groupID = ei.NameToNum[m_ID.ToString()];
								isValidGroupID = groups.Contains(groupID);
							}
							break;
						}
					}

					if (isValidGroupID == false) {
						throw new ParseError(String.Format("group(ID:{0}) does not exists in {1} table",
							m_ID.ToString(), args.table.m_name));
					}

					if (args.isLast) {
						args.verifier.Verify(Table.State.Step4_Verified_NomalData);
						m_result.type = Data.Type.UINT;
						m_result.value = groupID;
					}
				}
				else {
					Record record = null;
					switch (m_ID.type) {
						case Data.Type.UINT: {
							record = args.table.m_records_byInt.Find((uint)m_ID.value);
							break;
						}
						case Data.Type.STR: {
							record = args.table.m_records_byStr.Find((string)m_ID.value);
							break;
						}
					}

					if (record == null) {
						throw new ParseError(String.Format("record(ID:{0}) does not exists in {1} table",
							m_ID.ToString(), args.table.m_name));
					}

					if (args.isLast) {
						args.verifier.Verify(Table.State.Step4_Verified_NomalData);
						if (args.column.m_type == ColumnType.ID)
							m_result = args.column.Parse(record.ID_INT.ToString());
						else
							m_result = args.column.Parse(record[args.column.m_columnNumber].Source);
					}
					else {
						m_ID = DefaultParser.ID(record[args.column.m_columnNumber].Source);
						if (m_ID.Err())
							throw new ParseError(m_ID.errmsg);
					}
				}
			};
		}

		public Data Parse(string src)
		{
			Data ID = DefaultParser.ID(src);
			m_result = DefaultParser.Unknown("");
			if (ID.Err()) {
				m_result.errmsg = "parsing error, " + ID.errmsg;
				return m_result;
			}
			m_ID = ID;
			string err = Find();
			m_result.errmsg = err;
			return m_result;
		}
	}


	class TableSchema
	{
		public abstract class Column
		{
			public Table m_owner = null; 
			public ColumnType m_type;
			public int m_columnNumber = 0; // 소스상의 컬럼 번호
			public int m_lastErrorRow = 0;
			public string m_name = "";
			public int m_columnIndex = 0;
			public Parser Parse = DefaultParser.Unknown;
			public TypeMapper TypeInfo = new TypeMapper();

			public abstract string OnRegister(TableSchema schema);
		}

		public Table m_owner = null;
		public List<Column> m_columns = new List<Column>();
		public Dictionary<string, int> m_name2index = new Dictionary<string, int>();
		public uint m_autoIncrID = 0;

		public TableSchema(Table owner)
		{
			m_owner = owner;
		}

		public void RegisterColumn(Column column)
		{
			column.m_columnIndex = m_columns.Count;
			column.m_owner = m_owner;
			m_columns.Add(column);
		}

		public Column FindColumn(int columnID)
		{
			return m_columns[columnID];
		}

		public Column FindColumn(string name)
		{
			int columnID;
			if (m_name2index.TryGetValue(name, out columnID) == false)
				return null;
			return m_columns[columnID];
		}

		public bool CheckSchema()
		{
			// ID 타입이 하나 이하인지 체크
			bool success = true;
			{
				List<Column> ID_INT = new List<Column>();
				List<Column> ID_STR = new List<Column>();
				foreach (var column in m_columns) {
					if (column.m_type != ColumnType.ID)
						continue;
					switch (((Column_ID)column).m_detailType) {
						case ColumnType.INT: ID_INT.Add(column); break;
						case ColumnType.STR: ID_STR.Add(column); break;
					}
				}

				if (ID_INT.Count == 0)
					RegisterColumn(new Column_ID(ColumnType.INT, false));
				else if (ID_INT.Count > 1) {
					success = false;
					for (int i=1; i<ID_INT.Count; ++i) {
						Common.OnError(
							m_owner.m_name,
							Config.ColumnTypeRow,
							ID_INT[i].m_columnNumber,
							"duplicate ID.INT type");
					}
				}

				if (ID_STR.Count > 1) {
					success = false;
					for (int i=1; i<ID_STR.Count; ++i) {
						Common.OnError(
							m_owner.m_name,
							Config.ColumnTypeRow,
							ID_STR[i].m_columnNumber,
							"duplicate ID.STR type");
					}
				}
			}
			if (success == false)
				return false;

			// 컬럼명 체크
			foreach (var column in m_columns) {
				try {
					string e = Common.CheckNamingRule(column.m_name);
					if (e.Length > 0) {
						column.m_lastErrorRow = Config.ColumnNameRow;
						throw new ParseError(e);
					}
					if (m_name2index.ContainsKey(column.m_name)) {
						column.m_lastErrorRow = Config.ColumnNameRow;
						throw new ParseError("dupicate column name " + column.m_name);
					}
					m_name2index.Add(column.m_name, column.m_columnIndex);
				}
				catch (ParseError e) {
					success = false;
					Common.OnError(m_owner.m_name, column.m_lastErrorRow, column.m_columnNumber, e.Message);
				}
			}
			if (success == false)
				return false;

			// 각 컬럼 별 추가적인 체크
			foreach (var column in m_columns) {
				string e = column.OnRegister(this);
				if (e.Length > 0) {
					success = false;
					Common.OnError(m_owner.m_name, column.m_lastErrorRow, column.m_columnNumber, e);
				}
			}
			return success;
		}
	}


	class Column_Nomal : TableSchema.Column
	{
		public Column_Nomal(ColumnType type)
		{
			m_type = type;
			switch (m_type) {
				case ColumnType.INT: {
					Parse = DefaultParser.Long;
					TypeInfo = TypeMapper.byFBS("long");
					break;
				}
				case ColumnType.STR:
				case ColumnType.COMMENT: {
					Parse = DefaultParser.String;
					TypeInfo = TypeMapper.byFBS("string");
					break;
				}
				default: {
					throw new ParseError("invalid type " + m_type.ToString());
				}
			}
		}
		public override string OnRegister(TableSchema schema)
		{
			return "";
		}
	}

	class Column_ID : TableSchema.Column
	{
		public ColumnType m_detailType = ColumnType._UNKNOWN_;
		public bool m_isTypeName = false;
		public Column_ID(ColumnType detailType, bool isTypeName)
		{
			m_type = ColumnType.ID;
			m_detailType = detailType;
			m_isTypeName = isTypeName;
			switch (m_detailType) {
				case ColumnType.INT: {
					m_name = Config.ColName_ID_INT;
					break;
				}
				case ColumnType.STR: {
					m_name = Config.ColName_ID_STR;
					break;
				}
				default: {
					m_name = "";
					throw new ParseError("invalid detail ID type");
				}
			}
		}
		public override string OnRegister(TableSchema schema)
		{
			switch (m_detailType) {
				case ColumnType.INT: {
					Parse = DefaultParser.ID_INT;
					TypeInfo = TypeMapper.byFBS("uint");
					break;
				}
				case ColumnType.STR: {
					Parse = DefaultParser.ID_STR;
					if (m_isTypeName)
						TypeInfo = TypeMapper.byEnum(Common.EnumName(m_owner.m_name, "ID"));
					else
						TypeInfo = TypeMapper.byFBS("uint");
					break;
				}
			}
			return "";
		}
	}

	class Column_REF : TableSchema.Column
	{
		public string m_refTable = "";
		public string m_refColumn = "";
		public Column_REF(string refTable, string refColumn = "")
		{
			m_type = ColumnType.REF;
			m_refTable = refTable;
			m_refColumn = refColumn;
		}
		public override string OnRegister(TableSchema schema)
		{
			if (Config.TableID.ContainsKey(m_refTable) == false) {
				m_lastErrorRow = Config.ColumnTypeRow;
				return "table " + m_refTable + " does not exists";
			}
			Parse = (string src) =>
			{
				PullReferenceData reference = new PullReferenceData();
				reference.m_startTable = m_owner;
				reference.m_targetTableName = m_refTable;
				reference.m_targetColumnName = m_refColumn;
				return reference.Parse(src);
			};
			return "";
		}
	}
	
	class Column_GROUP : TableSchema.Column
	{
		public TableSchema.Column m_detailType = null;
		public bool m_isTypeName = false;
		public HashSet<uint> m_groups = new HashSet<uint>();
		public Column_GROUP(TableSchema.Column detailType, bool isTypeName)
		{
			m_type = ColumnType.GROUP;
			m_detailType = detailType;
			m_isTypeName = isTypeName;
		}
		public override string OnRegister(TableSchema schema)
		{
			m_detailType.m_owner = m_owner;
			m_detailType.m_name = m_name;
			switch (m_detailType.m_type) {
				case ColumnType.INT: {
					Parse = (string src) =>
					{
						var data = DefaultParser.ID_INT(src);
						if (data.Err())
							return data;
						uint groupID = (uint)data.value;
						m_groups.Add(groupID);
						return data;
					};
					TypeInfo = TypeMapper.byFBS("uint");
					break;
				}
				case ColumnType.STR: {
					Parse = (string src) =>
					{
						var data = DefaultParser.ID_STR(src);
						if (data.Err())
							return data;
						string enum_name = Common.EnumName(m_owner.m_name, m_name);
						EnumInfo ei;
						if (m_owner.m_enums.TryGetValue(enum_name, out ei) == false) {
							ei = new EnumInfo(enum_name, m_isTypeName);
							m_owner.m_enums.Add(enum_name, ei);
						}
						string en = data.value.ToString();
						ei.Add(en);
						uint groupID = ei.NameToNum[en];

						data.type = Data.Type.UINT;
						data.value = groupID;
						m_groups.Add(groupID);
						return data;
					};
					if (m_isTypeName)
						TypeInfo = TypeMapper.byEnum(Common.EnumName(m_owner.m_name, m_name));
					else
						TypeInfo = TypeMapper.byFBS("uint");
					break;
				}
				case ColumnType.REF: {
					Parse = (string src) =>
					{
						Column_REF refType = (Column_REF)m_detailType;
						var data = refType.Parse(src);
						if (data.Err())
							return data;
						uint groupID = (uint)data.value;
						m_groups.Add(groupID);
						return data;
					};
					break;
				}
				default: {
					m_lastErrorRow = Config.ColumnTypeRow;
					return "invalid detail GROUP type : " + m_detailType.ToString();
				}
			}
			return m_detailType.OnRegister(schema);
		}
	}

	class Column_ORDER : TableSchema.Column
	{
		public string m_groupName;
		public Column_GROUP m_group = null;
		public Column_ORDER(string groupName)
		{
			m_type = ColumnType.ORDER;
			m_groupName = groupName;
			Parse = DefaultParser.Long;
			TypeInfo = TypeMapper.byFBS("long");
		}
		public override string OnRegister(TableSchema schema)
		{
			if (m_groupName.Length > 0) {
				TableSchema.Column column = schema.FindColumn(m_groupName);
				if (column == null)
					return "invalid group name : " + m_groupName;
				if (column is Column_GROUP == false)
					return m_groupName + " is not GROUP";
				m_group = (Column_GROUP)column;
			}
			return "";
		}
	}

	class Column_WEIGHT : TableSchema.Column
	{
		public string m_groupName;
		public Column_GROUP m_group = null;
		public Column_WEIGHT(string groupName)
		{
			m_type = ColumnType.WEIGHT;
			m_groupName = groupName;
			Parse = DefaultParser.Uint;
			TypeInfo = TypeMapper.byFBS("uint");
		}
		public override string OnRegister(TableSchema schema)
		{
			if (m_groupName.Length > 0) {
				TableSchema.Column column = schema.FindColumn(m_groupName);
				if (column == null)
					return "invalid group name : " + m_groupName;
				if (column is Column_GROUP == false)
					return m_groupName + " is not GROUP";
				m_group = (Column_GROUP)column;
			}
			return "";
		}
	}

	class Column_RATE : TableSchema.Column
	{
		public long m_denominator;
		public Column_RATE(long denominator)
		{
			m_type = ColumnType.RATE;
			m_denominator = denominator;
			TypeInfo = TypeMapper.byFBS("double");
		}
		public override string OnRegister(TableSchema schema)
		{
			if (m_denominator <= 0) {
				m_lastErrorRow = Config.ColumnTypeRow;
				return "invalid denominator value : " + m_denominator;
			}
			Parse = (string src) =>
			{
				Data data;
				try {
					long num = long.Parse(src);
					data.type = Data.Type.DOUBLE;
					data.value = num / (double)m_denominator;
					data.errmsg = "";
				}
				catch (Exception e) {
					data.type = Data.Type._UNKNOWN_;
					data.value = null;
					data.errmsg = e.Message;
				}
				return data;
			};
			return "";
		}
	}


	class EnumInfo
	{
		public static Dictionary<string, EnumInfo> Enums = new Dictionary<string, EnumInfo>();

		public string EnumName;
		public SortedDictionary<uint, string> NumToName = new SortedDictionary<uint, string>();
		public Dictionary<string, uint> NameToNum = new Dictionary<string, uint>();
		public bool Build = false;

		public EnumInfo(string name, bool build)
		{
			EnumName = name;
			Build = build;
			lock (Enums) {
				Enums.Add(EnumName, this);
			}
		}
		public void Add(string name, uint number)
		{
			if (NameToNum.ContainsKey(name))
				return;
			NumToName.Add(number, name);
			NameToNum.Add(name, number);
		}
		public void Add(string name)
		{
			Add(name, (uint)NameToNum.Count);
		}
	}


	class Cell
	{
		public int Row = 0;
		public int Col = 0;
		public string Source = "";
		public Data ParsedData;
		public Cell(int row, int col, string src)
		{
			Row = row;
			Col = col;
			Source = src;
			ParsedData.type = Data.Type._UNKNOWN_;
		}
		public bool Parse(Parser parser, object lockObj)
		{
			Data data = parser(Source);
			lock (lockObj) {
				ParsedData = data;
				return ParsedData.Err() == false;
			}
		}
	}

	class Record : SortedDictionary<int, Cell>
	{
		public uint ID_INT = 0;
		public string ID_STR = "";
		public int Row;

		public Record(int row)
		{
			Row = row;
		}
		public void Add(Cell cell)
		{
			cell.Row = Row;
			base.Add(cell.Col, cell);
		}
	}

	class Table
	{
		public enum State
		{
			_NotLoded_ = 0,
			Step1_Verified_SchemaGrammar,
			Step2_Complete_ID_Indexing,
			Step3_Verified_Referance,
			Step4_Verified_NomalData,
			Step5_Verified_GroupReferance,
			Step6_Verified_ReferanceData,
			Step7_Complete_AdditionalVerify,
			VerifyComplete,
		}

		public object m_lock = new object();
		public int m_worker = 0;
		public State m_state = State._NotLoded_;
		public string m_name;
		public TableSchema m_schema;
		public Dictionary<string, EnumInfo> m_enums = new Dictionary<string, EnumInfo>();
		public HashSet<string> m_refTables = new HashSet<string>();

		public class Records<T> : Dictionary<T, Record>
		{
			public Record Find(T key)
			{
				Record record;
				if (TryGetValue(key, out record))
					return record;
				return null;
			}
		}
		public List<Record> m_records = new List<Record>();
		public Records<int> m_records_byRow = new Records<int>();
		public Records<uint> m_records_byInt = new Records<uint>();
		public Records<string> m_records_byStr = new Records<string>();

		public Table(string name)
		{
			m_name = name;
			m_schema = new TableSchema(this);
		}
	}

	class DB
	{
		public static DB s_instance = new DB();
		public object m_lock = new object();
		public Dictionary<string, Table> m_tables = new Dictionary<string, Table>();
		public Table FindTable(string table_name)
		{
			lock (m_lock) {
				Table table;
				if (m_tables.TryGetValue(table_name, out table))
					return table;
				if (Config.TableID.ContainsKey(table_name)) {
					table = new Table(table_name);
					m_tables.Add(table_name, table);
					return table;
				}
			}
			Common.OnError("table " + table_name + " does not exists");
			return null;
		}
	}

}
