using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaticDB_Maker
{
	public abstract class GenProgramCode
	{
		public abstract void Gen();
	}


	public struct LangType
	{
		public string fbs;
		public string cpp;
		public string cs;
		public string js;

		public static LangType Unknown()
		{
			LangType t;
			t.fbs = "<unknown>";
			t.cpp = "<unknown>";
			t.cs = "<unknown>";
			t.js = "<unknown>";
			return t;
		}

		private static Dictionary<string, LangType> s_fbs2langType = null;
		public static LangType byFBS(string fbs)
		{
			if (s_fbs2langType == null) {
				var map = new Dictionary<string, LangType>();
				{	LangType t;
					t.fbs = "bool"; t.cpp = "bool"; t.cs = "bool"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "byte"; t.cpp = "int8_t"; t.cs = "sbyte"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "ubyte"; t.cpp = "uint8_t"; t.cs = "byte"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "short"; t.cpp = "int16_t"; t.cs = "short"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "ushort"; t.cpp = "uint16_t"; t.cs = "ushort"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "int"; t.cpp = "int32_t"; t.cs = "int"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "uint"; t.cpp = "uint32_t"; t.cs = "uint"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "long"; t.cpp = "int64_t"; t.cs = "long"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "ulong"; t.cpp = "uint64_t"; t.cs = "ulong"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "float"; t.cpp = "float"; t.cs = "float"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "double"; t.cpp = "double"; t.cs = "double"; t.js = "var";
					map.Add(t.fbs, t);	}
				{	LangType t;
					t.fbs = "string"; t.cpp = "string"; t.cs = "string"; t.js = "var";
					map.Add(t.fbs, t);	}
				lock (Common.s_lock) {
					if (s_fbs2langType == null)
						s_fbs2langType = map;
				}
			}
			LangType ret;
			if (s_fbs2langType.TryGetValue(fbs, out ret))
				return ret;
			return Unknown();
		}
	}


	class Printer
	{
		string m_filepath = "";
		string m_buffer = "";

		public Printer(string target_file_path)
		{
			m_filepath = target_file_path;
			FileInfo fi = new FileInfo(target_file_path);
			if (fi.Exists == false)
				File.WriteAllText(m_filepath, "", Encoding.UTF8);
		}

		public void Print(string str)
		{
			m_buffer += str + '\n';
		}

		public void Print(string format, params object[] args)
		{
			m_buffer += String.Format(format, args) + '\n';
		}

		public void Flush()
		{
			string org = File.ReadAllText(m_filepath, Encoding.UTF8);
			if (org == m_buffer)
				return;
			File.WriteAllText(m_filepath, m_buffer, Encoding.UTF8);
			m_buffer = "";
		}
	}

	class TableBuilder
	{
		object m_lock = new object();
		Table m_table;
		TableVerifier m_verifier;

		public TableBuilder(Table table, TableVerifier verifier)
		{
			m_table = table;
			m_verifier = verifier;
		}

		public void GenFBS()
		{
			string enum_fbs_filename = m_table.m_name + "_enum.fbs";
			Printer fbs = new Printer(Path.Combine(Config.Out_FBS_Path, enum_fbs_filename));
			fbs.Print("namespace {0};", Config.Namespace);
			fbs.Print("");
			foreach (var column in m_table.m_schema.m_columns) {
				switch (column.m_type) {
					case ColumnType.ID: {
						Column_ID cast = (Column_ID)column;
						if (cast.m_detailType != ColumnType.STR)
							continue;
						break;
					}
					case ColumnType.GROUP: {
						Column_GROUP cast = (Column_GROUP)column;
						if (cast.m_detailType.m_type != ColumnType.STR)
							continue;
						break;
					}
					default: {
						continue;
					}
				}
				int ID_INT_column = m_table.m_schema.FindColumn("ID_INT").m_columnNumber;
				fbs.Print("enum {0}_{1} : uint", m_table.m_name, column.m_name);
				fbs.Print("{");
				foreach (var it in m_table.m_records_byStr) {
					var record = it.Value;
					string en = record.ID_STR;
					uint id = record.ID_INT;
					fbs.Print("    {0} = {1},", en, id);
				}
				fbs.Print("}");
				fbs.Print("");
			}
			fbs.Flush();

			fbs = new Printer(Path.Combine(Config.Out_FBS_Path, m_table.m_name + ".fbs"));
			fbs.Print("include \"{0}\";", enum_fbs_filename);
			fbs.Print("namespace {0};", Config.Namespace);
			fbs.Print("");
			fbs.Print("table {0}", m_table.m_name);
			fbs.Print("{");
			foreach (var column in m_table.m_schema.m_columns)
				fbs.Print("    {0} : {1};", column.m_name, column.LangType.fbs);
			fbs.Print("}");
			fbs.Print("");
			fbs.Print("table {0}_Table", m_table.m_name);
			fbs.Print("{");
			fbs.Print("    Data : [{0}];", m_table.m_name);
			fbs.Print("}");
			fbs.Print("");
			fbs.Print("root_table {0}_Table;", m_table.m_name);
			fbs.Flush();
		}

		public void GenBin()
		{
			//Printer json = new Printer("");
			//json.Print("{");
			//for (int i = Config.DataStartRow-1; i<m_table.m_records.Count; ++i) {
			//	Record record = m_table.m_records[i];
			//	foreach (var cell in record) {
					
			//	}
			//	string line;
			//}
			//json.Print("}");
		}

		public void GenProgramCode()
		{

		}

		public bool Build()
		{
			try {
				if (m_verifier.Verify(Table.State.Step6_VerifyComplete) == false)
					return false;
				GenFBS();
				GenBin();
				GenProgramCode();
			}
			catch (ParseError e) {
				Common.OnError(e.Message);
				return false;
			}
			catch (Exception e) {
				Common.OnError(e.ToString());
				return false;
			}
			return true;
		}
	}


	class Builder
	{
		public static Builder s_instance = new Builder();

		object m_lock = new object();
		Dictionary<string, TableBuilder> m_list = new Dictionary<string, TableBuilder>();
		public TableBuilder FindBuilder(string table_name)
		{
			lock (m_lock) {
				TableBuilder t;
				if (m_list.TryGetValue(table_name, out t))
					return t;
				var table = DB.s_instance.FindTable(table_name);
				var verifier = Verifier.s_instance.FindTableVerifier(table_name);
				if (table != null && verifier != null) {
					t = new TableBuilder(table, verifier);
					m_list.Add(table_name, t);
					return t;
				}
			}
			return null;
		}
	}
}
