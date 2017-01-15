using System;
using System.IO;
using System.Collections.Generic;

namespace StaticDB_Maker
{
	partial class Generator
	{
		public static void GenDBCode_CS()
		{

		}

		public static void GenTableCode_CS(Table table)
		{
			List<Field_REF> ref_list = new List<Field_REF>();
			List<Field_GROUP> group_list = new List<Field_GROUP>();
			List<Field_WEIGHT> weight_list = new List<Field_WEIGHT>();
			List<Field_ORDER> order_list = new List<Field_ORDER>();
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				switch (field.m_type) {
					case FieldType.REF: {
						Field_REF cast = (Field_REF)field;
						if (cast.m_refField=="" || cast.m_refField=="ID_INT" || cast.m_refField=="ID_STR")
							ref_list.Add(cast);
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

			Printer file = new Printer(Path.Combine(Config.Out_CS_Path, table.m_name + "_Table.cs"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("using System;");
			file.Print("using System.Collections.Generic;");
			file.Print("");
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			EnumInfo ID_Enum = null;
			if (table.m_enums.TryGetValue(Common.EnumName(table.m_name, "ID"), out ID_Enum)) {
				if (ID_Enum.Build == false)
					ID_Enum = null;
			}
			foreach (var field in group_list) {
				string ID_type = ID_Enum == null ? "uint" : ID_Enum.EnumName;
				file.Print("  using _{0}_Group = Dictionary<{1}, {2}_Record>;", field.m_name, ID_type, table.m_name);
			}
			foreach (var field in order_list) {
				file.Print("  using _{0}_Order = SortedDictionary<long, {1}_Record>;", field.m_name, table.m_name);
			}
			file.Print("");
			file.Print("");
			file.Print("");
			file.Print("  public class {0}_Record", table.m_name);
			file.Print("  {");
			file.Print("    private {0}_FBS m_record;", table.m_name);
			file.Print("    public {0}_Record({0}_FBS record) {{ m_record = record; }}", table.m_name);
			file.Print("    public {0}_FBS get {{ get {{ return m_record; }} }}", table.m_name);
			file.Print("");
			foreach (var field in ref_list)
				file.Print("    public {0}_Record Ref_{1} {{ get {{ return m_ref.m_{1}; }} }}", field.m_refTable, field.m_name);
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
			file.Print("    internal struct Ref");
			file.Print("    {");
			foreach (var field in ref_list)
				file.Print("      public {0}_Record m_{1};", field.m_refTable, field.m_name);
			file.Print("    }");
			file.Print("    private Ref m_ref;");
			file.Print("    internal void InitRef(Ref r) { m_ref = r; }");
			file.Print("  }");
			file.Print("");
			file.Print("");
			file.Print("");
			file.Print("  public class {0}_Table : StaticDB.Table<{0}_FBS_Data, {0}_Record>", table.m_name);
			file.Print("  {");
			if (ID_Enum != null) {
				file.Print("    public {0}_Record GetRecord({1} ID) {{ return base.GetRecord((uint)ID); }}", table.m_name, ID_Enum.EnumName);
				file.Print("    public {0}_Record this[{1} ID] {{ get {{ return base.GetRecord((uint)ID); }} }}", table.m_name, ID_Enum.EnumName);
				file.Print("");
			}
			foreach (var field in group_list) {
				string group_type = field.TypeInfo.types[TypeMapper.Type.CS];
				if (field.TypeInfo.IsEnum())
					file.Print("    public _{0}_Group _{0}(uint group_ID) {{ return _{0}(({1})group_ID); }}", field.m_name, group_type);
				file.Print("    public _{0}_Group _{0}({1} group_ID) {{ return m_{0}[group_ID]; }}", field.m_name, group_type);
				file.Print("");
			}
			foreach (var field in weight_list) {
				if (field.m_group == null)
					file.Print("    public {0}_Record Pick_{1} {{ get {{ return m_{1}.Pick; }} }}", table.m_name, field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					if (field.m_group.TypeInfo.IsEnum())
						file.Print("    public {0}_Record Pick_{1}(uint group_ID) {{ return Pick_{1}(({2})group_ID); }}", table.m_name, field.m_name, group_type);
					file.Print("    public {0}_Record Pick_{1}({2} group_ID) {{ return m_{1}[group_ID].Pick; }}", table.m_name, field.m_name, group_type);
				}
				file.Print("");
			}
			foreach (var field in order_list) {
				if (field.m_group == null)
					file.Print("    public _{0}_Order _{0} {{ get {{ return m_{0}; }} }}", field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					if (field.m_group.TypeInfo.IsEnum())
						file.Print("    public _{0}_Order _{0}(uint group_ID) {{ return _{0}(({1})group_ID); }}", field.m_name, group_type);
					file.Print("    public _{0}_Order _{0}({1} group_ID) {{ return m_{0}[group_ID]; }}", field.m_name, group_type);
				}
				file.Print("");
			}
			file.Print("");
			foreach (var field in group_list) {
				string ID_type = ID_Enum == null ? "uint" : ID_Enum.EnumName;
				string group_type = field.TypeInfo.types[TypeMapper.Type.CS];
				file.Print("    private Dictionary<{0}, _{1}_Group> m_{1} = new Dictionary<{0}, _{1}_Group>();", group_type, field.m_name);
			}
			foreach (var field in weight_list) {
				if (field.m_group == null)
					file.Print("    private StaticDB.Weight<{0}_Record> m_{1} = new StaticDB.Weight<{0}_Record>();", table.m_name, field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					file.Print("    private Dictionary<{0}, StaticDB.Weight<{1}_Record>> m_{2} = new Dictionary<{0}, StaticDB.Weight<{1}_Record>>();", group_type, table.m_name, field.m_name);
				}
			}
			foreach (var field in order_list) {
				if (field.m_group == null)
					file.Print("    private _{0}_Order m_{0} = new _{0}_Order();", field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CS];
					file.Print("    private Dictionary<{0}, _{1}_Order> m_{1} = new Dictionary<{0}, _{1}_Order>();", group_type, field.m_name);
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
			file.Print("    public override void OnLoaded(StaticDB.TableInterface[] tables)");
			file.Print("    {");
			file.Print("      foreach (var it in m_ID) {");
			file.Print("        var record = it.Value;");
			file.Print("");
			file.Print("        {0}_Record.Ref _ref;", table.m_name);
			foreach (var field in ref_list) {
				file.Print("        {{ var table = ({0}_Table)tables[(uint)TableID_{0}.Value];", field.m_refTable);
				file.Print("          _ref.m_{0} = table[record.get._{0}]; }}", field.m_name);
			}
			file.Print("        record.InitRef(_ref);");
			file.Print("");
			foreach (var field in group_list) {
				string ID_type = ID_Enum == null ? "uint" : ID_Enum.EnumName;
				file.Print("        m_{0}[record.get._{0}][({1})record.get._IDINT] = record;", field.m_name, ID_type);
				file.Print("");
			}
			foreach (var field in weight_list) {
				if (field.m_group == null)
					file.Print("        m_{0}.Add(record.get._{0}, record);", field.m_name);
				else
					file.Print("        m_{0}[record.get._{1}].Add(record.get._{0}, record);", field.m_name, field.m_group.m_name);
				file.Print("");
			}
			foreach (var field in order_list) {
				if (field.m_group == null)
					file.Print("        m_{0}[record.get._{0}] = record;", field.m_name);
				else
					file.Print("        m_{0}[record.get._{1}][record.get._{0}] = record;", field.m_name, field.m_group.m_name);
				file.Print("");
			}
			file.Print("      }");
			file.Print("    }");
			file.Print("  }");
			file.Print("}");
			file.Flush();
		}
	}
}
