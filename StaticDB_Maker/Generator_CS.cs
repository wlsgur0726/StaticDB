using System;
using System.IO;
using System.Collections.Generic;

namespace StaticDB_Maker
{
	partial class Generator
	{
		public static void GenDBCode_CS()
		{
			List<string> tables = new List<string>();
			foreach (var it in Config.TableID)
				tables.Add(it.Key);

			Printer file = new Printer(Path.Combine(Config.Out_CS_Path, Config.Namespace + "_DB.cs"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("");
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			file.Print("  public class {0}_DB : StaticDB.DB", Config.Namespace);
			file.Print("  {");
			file.Print("    public bool InitTables(string dir)");
			file.Print("    {");
			file.Print("      StaticDB.TableInterface[] tables = new StaticDB.TableInterface[{0}];", tables.Count);
			foreach (var table in tables)
				file.Print("      tables[(uint)TableID_{0}.Value] = new {0}_Table();", table);
			file.Print("      return base.InitTables(dir, tables);");
			file.Print("    }");
			file.Print("");
			foreach (var table in tables) {
				file.Print("    {0}_Table {0} {{ get {{ return ({0}_Table)m_tables[(uint)TableID_{0}.Value]; }} }}", table);
				file.Print("");
			}
			file.Print("  }");
			file.Print("}");
			file.Flush();
		}

		public static void GenTableCode_CS(Table table)
		{
			CommonInfo ci = new CommonInfo(table);

			Printer file = new Printer(Path.Combine(Config.Out_CS_Path, table.m_name + "_Table.cs"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("using System;");
			file.Print("using System.Collections.Generic;");
			file.Print("");
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			foreach (var field in ci.group_list) {
				string ID_type = ci.ID_Enum == null ? "uint" : ci.ID_Enum.EnumName;
				file.Print("  using {0}_{1}_Group = Dictionary<{2}, {0}_Record>;", table.m_name, field.m_name, ID_type);
			}
			foreach (var field in ci.weight_list)
				file.Print("  using {0}_{1}_Weight = StaticDB.Weight<{0}_Record>;", table.m_name, field.m_name);
			foreach (var field in ci.order_list)
				file.Print("  using {0}_{1}_Order = SortedDictionary<long, {0}_Record>;", table.m_name, field.m_name);
			foreach (var field in ci.refGroup_list) {
				string ID_type = TypeName(field.m_refTable, "ID", "uint");
				file.Print("  using {0}_{1}_Group = Dictionary<{2}, {0}_Record>;", field.m_refTable, field.m_refField, ID_type);
			}
			foreach (var field in ci.refWeightGroup_list)
				file.Print("  using {0}_{1}_Weight = StaticDB.Weight<{0}_Record>;", field.m_refTable, field.m_refField);
			foreach (var field in ci.refOrderGroup_list)
				file.Print("  using {0}_{1}_Order = SortedDictionary<long, {0}_Record>;", field.m_refTable, field.m_refField);
			file.Print("");
			file.Print("");
			file.Print("");
			file.Print("  public class {0}_Record", table.m_name);
			file.Print("  {");
			file.Print("    private {0}_FBS m_record;", table.m_name);
			file.Print("    public {0}_Record({0}_FBS record) {{ m_record = record; }}", table.m_name);
			file.Print("    public {0}_FBS get {{ get {{ return m_record; }} }}", table.m_name);
			file.Print("");
			foreach (var field in ci.ref_list)
				file.Print("    public {0}_Record Ref_{1} {{ get {{ return m_ref.m_{1}; }} }}", field.m_refTable, field.m_name);
			foreach (var field in ci.refGroup_list)
				file.Print("    public {0}_{1}_Group Ref_{2} {{ get {{ return m_ref.m_{2}; }} }}", field.m_refTable, field.m_refField, field.m_name);
			foreach (var field in ci.refWeightGroup_list)
				file.Print("    public {0}_{1}_Weight Ref_{2} {{ get {{ return m_ref.m_{2}; }} }}", field.m_refTable, field.m_refField, field.m_name);
			foreach (var field in ci.refOrderGroup_list)
				file.Print("    public {0}_{1}_Order Ref_{2} {{ get {{ return m_ref.m_{2}; }} }}", field.m_refTable, field.m_refField, field.m_name);
			file.Print("");
			file.Print("    public long GetInt(uint field) {{ return GetInt(({0}_Field)field); }}", table.m_name);
			file.Print("    public long GetInt({0}_Field field)", table.m_name);
			file.Print("    {");
			file.Print("      switch(field) {");
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				string prop = field.m_name;
				switch (field.m_type) {
					case FieldType.STR:
					case FieldType.RATE:
						return;
					case FieldType.ID: {
						prop = field.m_name.Replace("_", "");
						break;
					}
				}
				file.Print("        case {0}_Field._{1}:", table.m_name, field.m_name);
				file.Print("          return (long)get._{0};", prop);
			});
			file.Print("      }");
			file.Print("      throw new ArgumentException(\"invalid field : \" + field.ToString());");
			file.Print("    }");
			file.Print("");
			file.Print("    public string GetStr(uint field) {{ return GetStr(({0}_Field)field); }}", table.m_name);
			file.Print("    public string GetStr({0}_Field field)", table.m_name);
			file.Print("    {");
			file.Print("      switch(field) {");
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				string prop = field.m_type!=FieldType.ID ? field.m_name : field.m_name.Replace("_", "");
				string ret;
				if (field.TypeInfo.fbs == "string")
					ret = String.Format("get._{0}", field.m_name);
				else
					ret = String.Format("get._{0}.ToString()", prop);
				file.Print("        case {0}_Field._{1}:", table.m_name, field.m_name);
				file.Print("          return {0};", ret);
			});
			file.Print("      }");
			file.Print("      throw new ArgumentException(\"invalid field : \" + field.ToString());");
			file.Print("    }");
			file.Print("");
			file.Print("    public double GetReal(uint field) {{ return GetReal(({0}_Field)field); }}", table.m_name);
			file.Print("    public double GetReal({0}_Field field)", table.m_name);
			file.Print("    {");
			file.Print("      switch(field) {");
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				string prop = field.m_name;
				switch (field.m_type) {
					case FieldType.STR:
						return;
					case FieldType.ID: {
						prop = field.m_name.Replace("_", "");
						break;
					}
				}
				file.Print("        case {0}_Field._{1}:", table.m_name, field.m_name);
				file.Print("          return (double)get._{0};", prop);
			});
			file.Print("      }");
			file.Print("      throw new ArgumentException(\"invalid field : \" + field.ToString());");
			file.Print("    }");
			file.Print("");
			file.Print("    internal class Ref");
			file.Print("    {");
			foreach (var field in ci.ref_list)
				file.Print("      public {0}_Record m_{1} = null;", field.m_refTable, field.m_name);
			foreach (var field in ci.refGroup_list)
				file.Print("      public {0}_{1}_Group m_{2} = null;", field.m_refTable, field.m_refField, field.m_name);
			foreach (var field in ci.refWeightGroup_list)
				file.Print("      public {0}_{1}_Weight m_{2} = null;", field.m_refTable, field.m_refField, field.m_name);
			foreach (var field in ci.refOrderGroup_list)
				file.Print("      public {0}_{1}_Order m_{2} = null;", field.m_refTable, field.m_refField, field.m_name);
			file.Print("    }");
			file.Print("    private Ref m_ref = null;");
			file.Print("    internal void InitRef(Ref r) { m_ref = r; }");
			file.Print("  }");
			file.Print("");
			file.Print("");
			file.Print("");
			file.Print("  public class {0}_Table : StaticDB.Table<{0}_FBS_Data, {0}_Record>", table.m_name);
			file.Print("  {");
			if (ci.ID_Enum != null) {
				file.Print("    public {0}_Record GetRecord({1} ID) {{ return base.GetRecord((uint)ID); }}", table.m_name, ci.ID_Enum.EnumName);
				file.Print("    public {0}_Record this[{1} ID] {{ get {{ return base.GetRecord((uint)ID); }} }}", table.m_name, ci.ID_Enum.EnumName);
				file.Print("");
			}
			foreach (var field in ci.group_list) {
				string group_type = field.TypeInfo.types[TypeMapper.Type.CS];
				if (field.TypeInfo.IsEnum())
					file.Print("    public {0}_{1}_Group _{1}(uint group_ID) {{ return _{1}(({2})group_ID); }}", table.m_name, field.m_name, group_type);
				file.Print("    public {0}_{1}_Group _{1}({2} group_ID) {{ return m_{1}[group_ID]; }}", table.m_name, field.m_name, group_type);
				file.Print("");
			}
			foreach (var field in ci.weight_list) {
				if (field.m_group == null)
					file.Print("    public {0}_{1}_Weight _{1} {{ get {{ return m_{1}; }} }}", table.m_name, field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					if (field.m_group.TypeInfo.IsEnum())
						file.Print("    public {0}_{1}_Weight _{1}(uint group_ID) {{ return _{1}(({2})group_ID); }}", table.m_name, field.m_name, group_type);
					file.Print("    public {0}_{1}_Weight _{1}({2} group_ID) {{ return m_{1}[group_ID]; }}", table.m_name, field.m_name, group_type);
				}
				file.Print("");
			}
			foreach (var field in ci.order_list) {
				if (field.m_group == null)
					file.Print("    public {0}_{1}_Order _{1} {{ get {{ return m_{1}; }} }}", table.m_name, field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					if (field.m_group.TypeInfo.IsEnum())
						file.Print("    public {0}_{1}_Order _{1}(uint group_ID) {{ return _{1}(({2})group_ID); }}", table.m_name, field.m_name, group_type);
					file.Print("    public {0}_{1}_Order _{1}({2} group_ID) {{ return m_{1}[group_ID]; }}", table.m_name, field.m_name, group_type);
				}
				file.Print("");
			}
			file.Print("");
			file.Print("    private Dictionary<uint, {0}_Record.Ref> m__refTable = new Dictionary<uint, {0}_Record.Ref>();", table.m_name);

			foreach (var field in ci.group_list) {
				string ID_type = ci.ID_Enum == null ? "uint" : ci.ID_Enum.EnumName;
				string group_type = field.TypeInfo.types[TypeMapper.Type.CS];
				file.Print("    private Dictionary<{0}, {1}_{2}_Group> m_{2} = new Dictionary<{0}, {1}_{2}_Group>();", group_type, table.m_name, field.m_name);
			}
			foreach (var field in ci.weight_list) {
				if (field.m_group == null)
					file.Print("    private {0}_{1}_Weight m_{1} = new {0}_{1}_Weight();", table.m_name, field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					file.Print("    private Dictionary<{0}, {1}_{2}_Weight> m_{2} = new Dictionary<{0}, {1}_{2}_Weight>();", group_type, table.m_name, field.m_name);
				}
			}
			foreach (var field in ci.order_list) {
				if (field.m_group == null)
					file.Print("    private {0}_{1}_Order m_{1} = new {0}_{1}_Order();", table.m_name, field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					file.Print("    private Dictionary<{0}, {1}_{2}_Order> m_{2} = new Dictionary<{0}, {1}_{2}_Order>();", group_type, table.m_name, field.m_name);
				}
			}
			file.Print("");
			file.Print("    public override string GetTableFileName() {{ return \"{0}.bin\"; }}", table.m_name);
			file.Print("");
			file.Print("    public override void Init(FlatBuffers.ByteBuffer buffer)");
			file.Print("    {");
			file.Print("      m_buffer = buffer;");
			file.Print("      m_data = {0}_FBS_Data.GetRootAs{0}_FBS_Data(m_buffer);", table.m_name);
			file.Print("      m_ID.Clear();");
			file.Print("      for (int i = 0; i<m_data.DataLength; ++i) {");
			file.Print("        var record = m_data.Data(i);");
			file.Print("        m_ID.Add(record.Value._IDINT, new {0}_Record(record.Value));", table.m_name);
			file.Print("      }");
			file.Print("    }");
			file.Print("");
			file.Print("    public override void OnLoaded1(StaticDB.TableInterface[] tables)");
			file.Print("    {");
			file.Print("      foreach (var it in m_ID) {");
			file.Print("        var record = it.Value;");
			file.Print("");
			file.Print("        {0}_Record.Ref __ref = new {0}_Record.Ref();", table.m_name);
			file.Print("        record.InitRef(__ref);");
			file.Print("        m__refTable.Add((uint)it.Key, __ref);");
			foreach (var field in ci.ref_list) {
				file.Print("        {{ var table = ({0}_Table)tables[(uint)TableID_{0}.Value];", field.m_refTable);
				file.Print("          __ref.m_{0} = table[record.get._{0}]; }}", field.m_name);
			}
			file.Print("");
			foreach (var field in ci.group_list) {
				string ID_type = ci.ID_Enum == null ? "uint" : ci.ID_Enum.EnumName;
				file.Print("        m_{0}.Add(record.get._{0}, new {1}_{0}_Group());", field.m_name, table.m_name);
				file.Print("        m_{0}[record.get._{0}].Add(({1})record.get._IDINT, record);", field.m_name, ID_type);
				file.Print("");
			}
			foreach (var field in ci.weight_list) {
				if (field.m_group == null)
					file.Print("        m_{0}.Add(record.get._{0}, record);", field.m_name);
				else {
					file.Print("        m_{0}.Add(record.get._{1}, new {2}_{0}_Weight());", field.m_name, field.m_group.m_name, table.m_name);
					file.Print("        m_{0}[record.get._{1}].Add(record.get._{0}, record);", field.m_name, field.m_group.m_name);
				}
				file.Print("");
			}
			foreach (var field in ci.order_list) {
				if (field.m_group == null)
					file.Print("        m_{0}.Add(record.get._{0}, record);", field.m_name);
				else {
					file.Print("        m_{0}.Add(record.get._{1}, new {2}_{0}_Order());", field.m_name, field.m_group.m_name, table.m_name);
					file.Print("        m_{0}[record.get._{1}].Add(record.get._{0}, record);", field.m_name, field.m_group.m_name);
				}
				file.Print("");
			}
			file.Print("      }");
			file.Print("    }");
			file.Print("");
			file.Print("    public override void OnLoaded2(StaticDB.TableInterface[] tables)");
			file.Print("    {");
			file.Print("      foreach (var it in m_ID) {");
			file.Print("        var record = it.Value;");
			file.Print("");
			file.Print("        var __ref = m__refTable[(uint)it.Key];");
			foreach (var field in ci.refGroup_list) {
				file.Print("        {{ var table = ({0}_Table)tables[(uint)TableID_{0}.Value];", field.m_refTable);
				file.Print("          __ref.m_{0} = table._{1}(record.get._{0}); }}", field.m_name, field.m_refField);
			}
			foreach (var field in ci.refWeightGroup_list) {
				file.Print("        {{ var table = ({0}_Table)tables[(uint)TableID_{0}.Value];", field.m_refTable);
				file.Print("          __ref.m_{0} = table._{1}(record.get._{0}); }}", field.m_name, field.m_refField);
			}
			foreach (var field in ci.refOrderGroup_list) {
				file.Print("        {{ var table = ({0}_Table)tables[(uint)TableID_{0}.Value];", field.m_refTable);
				file.Print("          __ref.m_{0} = table._{1}(record.get._{0}); }}", field.m_name, field.m_refField);
			}
			file.Print("      }");
			file.Print("    }");
			file.Print("  }");
			file.Print("}");
			file.Flush();
		}
	}
}
