﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaticDB_Maker
{
	partial class Generator
	{
		static string TypeName(string table, string name, string default_)
		{
			EnumInfo ei = null;
			if (EnumInfo.Enums.TryGetValue(Common.EnumName(table, name), out ei)) {
				if (ei.Build == false)
					ei = null;
			}
			return ei == null ? default_ : ei.EnumName;
		}

		class CommonInfo
		{
			public List<Field_REF> ref_list = new List<Field_REF>();
			public List<Field_REF> refGroup_list = new List<Field_REF>();
			public List<Field_REF> refWeightGroup_list = new List<Field_REF>();
			public List<Field_REF> refOrderGroup_list = new List<Field_REF>();
			public List<Field_GROUP> group_list = new List<Field_GROUP>();
			public List<Field_WEIGHT> weight_list = new List<Field_WEIGHT>();
			public List<Field_ORDER> order_list = new List<Field_ORDER>();
			public EnumInfo ID_Enum = null;

			public CommonInfo(Table table)
			{
				if (table.m_enums.TryGetValue(Common.EnumName(table.m_name, "ID"), out ID_Enum)) {
					if (ID_Enum.Build == false)
						ID_Enum = null;
				}

				TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
				{
					switch (field.m_type) {
						case FieldType.REF: {
							Field_REF cast = (Field_REF)field;
							if (cast.m_refField=="" || cast.m_refField=="ID_INT" || cast.m_refField=="ID_STR")
								ref_list.Add(cast);
							else if (cast.m_groupRef != null) {
								switch (cast.m_finalRef.m_type) {
									case FieldType.ID:
									case FieldType.GROUP: {
										refGroup_list.Add(cast);
										break;
									}
									case FieldType.WEIGHT: {
										refWeightGroup_list.Add(cast);
										break;
									}
									case FieldType.ORDER: {
										refOrderGroup_list.Add(cast);
										break;
									}
								}
							}
							break;
						}
						case FieldType.GROUP: {
							Field_GROUP cast = (Field_GROUP)field;
							group_list.Add(cast);
							if (cast.m_detailType.m_type == FieldType.REF)
								ref_list.Add((Field_REF)cast.m_detailType);
							break;
						}
						case FieldType.WEIGHT: {
							weight_list.Add((Field_WEIGHT)field);
							break;
						}
						case FieldType.ORDER: {
							order_list.Add((Field_ORDER)field);
							break;
						}
					}
				});
			}
		}
	}


	public class TypeMapper
	{
		public enum Type : int
		{
			CPP = 0,
			CS,
			JS,
			_End_
		}

		public string fbs;
		public Dictionary<Type, string> types = new Dictionary<Type, string>();

		public TypeMapper(string fbs = "<unknown>")
		{
			this.fbs = fbs;
			for (Type i=0; i<Type._End_; ++i)
				types.Add(i, "<unknown>");
		}

		public bool IsEnum()
		{
			if (fbs == "<unknown>")
				return false;
			var t = byFBS(fbs);
			return t.fbs == "<unknown>";
		}

		private static Dictionary<string, TypeMapper> s_fbs2langType = null;
		public static TypeMapper byFBS(string fbs)
		{
			if (s_fbs2langType == null) {
				var map = new Dictionary<string, TypeMapper>();
				{	TypeMapper t = new TypeMapper("bool");
					t.types[Type.CPP] = "bool";
					t.types[Type.CS] = "bool";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("byte");
					t.types[Type.CPP] = "int8_t";
					t.types[Type.CS] = "sbyte";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("ubyte");
					t.types[Type.CPP] = "uint8_t";
					t.types[Type.CS] = "byte";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("short");
					t.types[Type.CPP] = "int16_t";
					t.types[Type.CS] = "short";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("ushort");
					t.types[Type.CPP] = "uint16_t";
					t.types[Type.CS] = "ushort";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("int");
					t.types[Type.CPP] = "int32_t";
					t.types[Type.CS] = "int";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("uint");
					t.types[Type.CPP] = "uint32_t";
					t.types[Type.CS] = "uint";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("long");
					t.types[Type.CPP] = "int64_t";
					t.types[Type.CS] = "long";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("ulong");
					t.types[Type.CPP] = "uint64_t";
					t.types[Type.CS] = "ulong";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("float");
					t.types[Type.CPP] = "float";
					t.types[Type.CS] = "float";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("double");
					t.types[Type.CPP] = "double";
					t.types[Type.CS] = "double";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				{	TypeMapper t = new TypeMapper("string");
					t.types[Type.CPP] = "string";
					t.types[Type.CS] = "string";
					t.types[Type.JS] = "var";
					map.Add(t.fbs, t);	}
				lock (Common.s_lock) {
					if (s_fbs2langType == null)
						s_fbs2langType = map;
				}
			}
			TypeMapper ret;
			if (s_fbs2langType.TryGetValue(fbs, out ret))
				return ret;
			return new TypeMapper();
		}

		public static TypeMapper byEnum(string enum_name)
		{
			TypeMapper ret = new TypeMapper(enum_name);
			ret.types[Type.CPP] = enum_name;
			ret.types[Type.CS] = enum_name;
			ret.types[Type.JS] = "var";
			return ret;
		}

		static Dictionary<Type, List<string>> s_outputInfo = null;
		public static List<string> OutputFiles(Type t)
		{
			if (s_outputInfo == null) {
				Dictionary<Type, List<string>> map = new Dictionary<Type, List<string>>();
				for (Type i=0; i<Type._End_; ++i)
					map.Add(i, new List<string>());
				map[Type.CPP].Add("{0}_generated.h");
				map[Type.CS].Add("{0}.cs");
				map[Type.JS].Add("{0}_generated.js");
				lock (Common.s_lock) {
					if (s_outputInfo == null)
						s_outputInfo = map;
				}
			}
			List<string> ret;
			if (s_outputInfo.TryGetValue(t, out ret))
				return ret;
			return null;
		}

		static Dictionary<Type, string> s_flatcArgs = null;
		public static string Arguments(Type t)
		{
			if (s_flatcArgs == null) {
				Dictionary<Type, string> map = new Dictionary<Type, string>();
				for (Type i = 0; i<Type._End_; ++i)
					map.Add(i, "");
				map[Type.CPP] = "--cpp --scoped-enums";
				map[Type.CS] = "--csharp --gen-onefile";
				map[Type.JS] = "--js";
				lock (Common.s_lock) {
					if (s_flatcArgs == null)
						s_flatcArgs = map;
				}
			}
			string ret;
			if (s_flatcArgs.TryGetValue(t, out ret))
				return ret;
			return null;
		}
	}


	class Printer
	{
		string m_filepath = "";
		string m_buffer = "";

		public static void CreateDirectory(string dir)
		{
			DirectoryInfo di = new DirectoryInfo(dir);
			if (di.Exists == false)
				di.Create();
		}

		public Printer(string target_file_path)
		{
			string dir = Path.GetDirectoryName(target_file_path);
			CreateDirectory(dir);
			m_filepath = target_file_path;
		}

		public string FilePath { get{ return m_filepath; } }

		public void Print(string str)
		{
			m_buffer += str + '\n';
		}

		public void Print(string format, params object[] args)
		{
			m_buffer += String.Format(format, args) + '\n';
		}

		public void Reset(string str = "")
		{
			m_buffer = str;
		}

		public void Flush()
		{
			string org = "";
			try {
				org = File.ReadAllText(m_filepath, Encoding.UTF8);
			}
			catch (Exception) {
			}
			finally {
				if (org != m_buffer)
					File.WriteAllText(m_filepath, m_buffer, Encoding.UTF8);
				Reset();
			}
		}
	}


	class Flatc
	{
		public static void Exec(ProcessStartInfo psi, string table)
		{
			psi.FileName = Config.flatc_Path;
			var flatc = Process.Start(psi);
			flatc.WaitForExit();

			string stdout = "";
			while (flatc.StandardOutput.Peek() >= 0)
				stdout += flatc.StandardOutput.ReadLine() + '\n';
			if (flatc.ExitCode != 0)
				throw new Exception(Common.ErrMsg(table, 0, 0, String.Format("flatc error\n{0}", stdout)));
		}

		public static void JsonToBin(string table)
		{
			ProcessStartInfo psi = Common.DefaultPSI();
			psi.WorkingDirectory = Config.Out_BIN_Path;
			psi.Arguments = String.Format("--binary \"{0}\" \"{1}\"",
				Path.Combine(Config.Out_FBS_Path, table+".fbs"),
				Path.Combine(Config.Temp_Path, table+".json"));

			Printer.CreateDirectory(psi.WorkingDirectory);
			Exec(psi, table);
		}

		public static void CompileFBS(string table, string outdir, TypeMapper.Type type)
		{
			Printer.CreateDirectory(outdir);
			ProcessStartInfo psi = Common.DefaultPSI();
			psi.WorkingDirectory = Config.Out_FBS_Path;
			psi.Arguments = String.Format("{0} -o \"{1}\" {2}.fbs {2}_enum.fbs",
				TypeMapper.Arguments(type), Config.Temp_Path, table);
			Exec(psi, table);

			var files = TypeMapper.OutputFiles(type);
			string[] src = { table, table+"_enum" };
			foreach (var fbs in src) {
				foreach (var format in files) {
					string filename = String.Format(format, fbs);
					Printer flatc = new Printer(Path.Combine(outdir, filename));
					flatc.Reset(File.ReadAllText(Path.Combine(Config.Temp_Path, filename), Encoding.UTF8));
					flatc.Flush();
				}
			}
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

		public delegate void Loop_FBS_Fields_Delegate(TableSchema.Field field);
		public static void Loop_FBS_Fields(Table table, Loop_FBS_Fields_Delegate f)
		{
			foreach (var field in table.m_schema.m_fields) {
				switch (field.m_type) {
					case FieldType.ID: {
						Field_ID cast = (Field_ID)field;
						if (cast.m_detailType==FieldType.STR && cast.m_isTypeName==false)
							continue;
						break;
					}
				}
				f(field);
			}
		}

		public void GenFBS()
		{
			string enum_fbs_filename = m_table.m_name + "_enum.fbs";
			Printer fbs = new Printer(Path.Combine(Config.Out_FBS_Path, enum_fbs_filename));
			fbs.Print("// {0}", Config.AutoGenComment);
			fbs.Print("namespace {0};", Config.Namespace);
			fbs.Print("");
			fbs.Print("enum TableID_{0} : uint {{ Value = {1} }}", m_table.m_name, Config.TableID[m_table.m_name]);
			fbs.Print("");
			fbs.Print("enum {0}_Field : uint", m_table.m_name);
			fbs.Print("{");
			Loop_FBS_Fields(m_table, (TableSchema.Field field) =>
			{
				fbs.Print("  _{0},", field.m_name);
			});
			fbs.Print("}");
			fbs.Print("");
			foreach (var it in m_table.m_enums) {
				EnumInfo ei = it.Value;
				if (ei.Build == false)
					continue;
				fbs.Print("enum {0} : uint", ei.EnumName);
				fbs.Print("{");
				foreach (var en in ei.NumToName)
					fbs.Print("  _{0} = {1},", en.Value, en.Key);
				fbs.Print("}");
				fbs.Print("");
			}
			fbs.Flush();

			fbs = new Printer(Path.Combine(Config.Out_FBS_Path, m_table.m_name + ".fbs"));
			fbs.Print("// {0}", Config.AutoGenComment);
			fbs.Print("include \"{0}\";", enum_fbs_filename);
			foreach (var refTable in m_table.m_refTables)
				fbs.Print("include \"{0}_enum.fbs\";", refTable);
			fbs.Print("namespace {0};", Config.Namespace);
			fbs.Print("");
			fbs.Print("table {0}_FBS", m_table.m_name);
			fbs.Print("{");
			Loop_FBS_Fields(m_table, (TableSchema.Field field) =>
			{
				var type = field.TypeInfo;
				if (field.m_type==FieldType.ID && ((Field_ID)field).m_detailType==FieldType.INT)
					type = TypeMapper.byFBS("uint");
				string print = String.Format("  _{0} : {1}", field.m_name, type.fbs);
				if (type.IsEnum()) {
					var first = EnumInfo.Enums[type.fbs].NumToName.First();
					print += " = _" + first.Value;
				}
				print += ';';
				fbs.Print(print);
			});
			fbs.Print("}");
			fbs.Print("");
			fbs.Print("table {0}_FBS_Data", m_table.m_name);
			fbs.Print("{");
			fbs.Print("  Data : [{0}_FBS];", m_table.m_name);
			fbs.Print("}");
			fbs.Print("");
			fbs.Print("root_type {0}_FBS_Data;", m_table.m_name);
			fbs.Flush();
		}

		public void GenProgramCode()
		{
			if (Config.Out_CPP_Path.Length > 0) {
				Flatc.CompileFBS(m_table.m_name, Config.Out_CPP_Path, TypeMapper.Type.CPP);
				Generator.GenTableCode_CPP(m_table);
			}
			if (Config.Out_CS_Path.Length > 0) {
				Flatc.CompileFBS(m_table.m_name, Config.Out_CS_Path, TypeMapper.Type.CS);
				Generator.GenTableCode_CS(m_table);
			}
			if (Config.Out_JS_Path.Length > 0) {
				Flatc.CompileFBS(m_table.m_name, Config.Out_JS_Path, TypeMapper.Type.JS);
				Generator.GenTableCode_JS(m_table);
			}
		}

		public void GenBin()
		{
			Printer json = new Printer(Path.Combine(Config.Temp_Path, m_table.m_name + ".json"));
			json.Print("{Data:[");
			for (int i = Config.DataStartRow-1; i<m_table.m_records.Count; ++i) {
				Record record = m_table.m_records[i];
				string line = " {_ID_INT:" + record.ID_INT + ',';
				Loop_FBS_Fields(m_table, (TableSchema.Field field) =>
				{
					if (field.m_type == FieldType.ID) {
						if (((Field_ID)field).m_detailType == FieldType.INT)
							return;
					}
					bool isStr = field.TypeInfo.fbs == "string";
					line += '_' + field.m_name + ':';
					if (isStr)
						line += '"';
					line += record[field.m_fieldNumber].ParsedData.ToString();
					if (isStr)
						line += '"';
					line += ",";
				});
				line += "},";
				json.Print(line);
			}
			json.Print("]}");
			json.Flush();
			Flatc.JsonToBin(m_table.m_name);
		}

		public bool Build_Step1()
		{
			try {
				if (m_verifier.Verify(Table.State.VerifyComplete) == false)
					return false;
				GenFBS();
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

		public bool Build_Step2()
		{
			try {
				GenProgramCode();
				GenBin();
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

		public enum State
		{
			_NotStarted_ = 0,
			Step1_GenFBS,
			Step2_GenOthers,
			Complete
		}

		public bool CheckState(State s)
		{
			lock (m_lock) {
				return s <= m_state;
			}
		}
		public void SetState(State s)
		{
			lock (m_lock) {
				if (m_state < s)
					m_state = s;
			}
		}

		object m_lock = new object();
		State m_state = State._NotStarted_;
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
