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
			List<Column_REF> ref_list = new List<Column_REF>();
			List<Column_GROUP> group_list = new List<Column_GROUP>();
			List<Column_WEIGHT> weight_list = new List<Column_WEIGHT>();
			List<Column_ORDER> order_list = new List<Column_ORDER>();
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column) =>
			{
				switch (column.m_type) {
					case ColumnType.REF: {
						Column_REF cast = (Column_REF)column;
						if (cast.m_refColumn=="" || cast.m_refColumn=="ID_INT" || cast.m_refColumn=="ID_STR")
							ref_list.Add(cast);
						break;
					}
					case ColumnType.GROUP: {
						Column_GROUP cast = (Column_GROUP)column;
						group_list.Add(cast);
						if (cast.m_detailType.m_type == ColumnType.REF)
							ref_list.Add((Column_REF)cast.m_detailType);
						break;
					}
					case ColumnType.WEIGHT: {
						weight_list.Add((Column_WEIGHT)column);
						break;
					}
					case ColumnType.ORDER: {
						order_list.Add((Column_ORDER)column);
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
			foreach (var column in group_list) {
				string ID_type = ID_Enum == null ? "uint" : ID_Enum.EnumName;
				file.Print("  using _{0}_Group = Dictionary<{1}, {2}_Record>;", column.m_name, ID_type, table.m_name);
			}
			foreach (var column in order_list) {
				file.Print("  using _{0}_Order = SortedDictionary<long, {1}_Record>;", column.m_name, table.m_name);
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
			foreach (var column in ref_list)
				file.Print("    public {0}_Record Ref_{1} {{ get {{ return m_ref.m_{1}; }} }}", column.m_refTable, column.m_name);
			file.Print("");
			file.Print("    public long GetInt(uint column) {{ return GetInt(({0}_Column)column); }}", table.m_name);
			file.Print("    public long GetInt({0}_Column column)", table.m_name);
			file.Print("    {");
			file.Print("      switch(column) {");
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column) =>
			{
				string prop = column.m_name;
				switch (column.m_type) {
					case ColumnType.STR:
					case ColumnType.RATE:
						return;
					case ColumnType.ID: {
						prop = column.m_name.Replace("_", "");
						break;
					}
				}
				file.Print("        case {0}_Column._{1}:", table.m_name, column.m_name);
				file.Print("          return (long)get._{0};", prop);
			});
			file.Print("      }");
			file.Print("      throw new ArgumentException(\"invalid column : \" + column.ToString());");
			file.Print("    }");
			file.Print("");
			file.Print("    public string GetStr(uint column) {{ return GetStr(({0}_Column)column); }}", table.m_name);
			file.Print("    public string GetStr({0}_Column column)", table.m_name);
			file.Print("    {");
			file.Print("      switch(column) {");
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column) =>
			{
				string prop = column.m_type!=ColumnType.ID ? column.m_name : column.m_name.Replace("_", "");
				string ret;
				if (column.TypeInfo.fbs == "string")
					ret = String.Format("get._{0}", column.m_name);
				else
					ret = String.Format("get._{0}.ToString()", prop);
				file.Print("        case {0}_Column._{1}:", table.m_name, column.m_name);
				file.Print("          return {0};", ret);
			});
			file.Print("      }");
			file.Print("      throw new ArgumentException(\"invalid column : \" + column.ToString());");
			file.Print("    }");
			file.Print("");
			file.Print("    public double GetReal(uint column) {{ return GetReal(({0}_Column)column); }}", table.m_name);
			file.Print("    public double GetReal({0}_Column column)", table.m_name);
			file.Print("    {");
			file.Print("      switch(column) {");
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column) =>
			{
				string prop = column.m_name;
				switch (column.m_type) {
					case ColumnType.STR:
						return;
					case ColumnType.ID: {
						prop = column.m_name.Replace("_", "");
						break;
					}
				}
				file.Print("        case {0}_Column._{1}:", table.m_name, column.m_name);
				file.Print("          return (double)get._{0};", prop);
			});
			file.Print("      }");
			file.Print("      throw new ArgumentException(\"invalid column : \" + column.ToString());");
			file.Print("    }");
			file.Print("");
			file.Print("    internal struct Ref");
			file.Print("    {");
			foreach (var column in ref_list)
				file.Print("      public {0}_Record m_{1};", column.m_refTable, column.m_name);
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
			foreach (var column in group_list) {
				string group_type = column.TypeInfo.types[TypeMapper.Type.CS];
				if (column.TypeInfo.IsEnum())
					file.Print("    public _{0}_Group _{0}(uint group_ID) {{ return _{0}(({1})group_ID); }}", column.m_name, group_type);
				file.Print("    public _{0}_Group _{0}({1} group_ID) {{ return m_{0}[group_ID]; }}", column.m_name, group_type);
				file.Print("");
			}
			foreach (var column in weight_list) {
				if (column.m_group == null)
					file.Print("    public {0}_Record Pick_{1} {{ get {{ return m_{1}.Pick; }} }}", table.m_name, column.m_name);
				else {
					string group_type = column.m_group.TypeInfo.types[TypeMapper.Type.CS];
					if (column.m_group.TypeInfo.IsEnum())
						file.Print("    public {0}_Record Pick_{1}(uint group_ID) {{ return Pick_{1}(({2})group_ID); }}", table.m_name, column.m_name, group_type);
					file.Print("    public {0}_Record Pick_{1}({2} group_ID) {{ return m_{1}[group_ID].Pick; }}", table.m_name, column.m_name, group_type);
				}
				file.Print("");
			}
			foreach (var column in order_list) {
				if (column.m_group == null)
					file.Print("    public _{0}_Order _{0} {{ get {{ return m_{0}; }} }}", column.m_name);
				else {
					string group_type = column.TypeInfo.types[TypeMapper.Type.CS];
					if (column.TypeInfo.IsEnum())
						file.Print("    public _{0}_Order _{0}(uint group_ID) {{ return _{0}(({1})group_ID); }}", column.m_name, group_type);
					file.Print("    public _{0}_Order _{0}({1} group_ID) {{ return m_{0}[group_ID]; }}", column.m_name, group_type);
				}
				file.Print("");
			}
			file.Print("");
			foreach (var column in group_list) {
				string ID_type = ID_Enum == null ? "uint" : ID_Enum.EnumName;
				string group_type = column.TypeInfo.types[TypeMapper.Type.CS];
				file.Print("    private Dictionary<{0}, _{1}_Group> m_{1} = new Dictionary<{0}, _{1}_Group>();", group_type, column.m_name);
			}
			foreach (var column in weight_list) {
				if (column.m_group == null)
					file.Print("    private StaticDB.Weight<{0}_Record> m_{1} = new StaticDB.Weight<{0}_Record>();", table.m_name, column.m_name);
				else {
					string group_type = column.m_group.TypeInfo.types[TypeMapper.Type.CS];
					file.Print("    private Dictionary<{0}, StaticDB.Weight<{1}_Record>> m_{2} = new Dictionary<{0}, StaticDB.Weight<{1}_Record>>();", group_type, table.m_name, column.m_name);
				}
			}
			foreach (var column in order_list) {
				if (column.m_group == null)
					file.Print("    private _{0}_Order m_{0} = new _{0}_Order();", column.m_name);
				else {
					string group_type = column.TypeInfo.types[TypeMapper.Type.CS];
					file.Print("    private Dictionary<{0}, _{1}_Order> m_{1} = new Dictionary<{0}, _{1}_Order>();", group_type, column.m_name);
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
			foreach (var column in ref_list) {
				file.Print("        {{ var table = ({0}_Table)tables[(uint)TableID_{0}.Value];", column.m_refTable);
				file.Print("          _ref.m_{0} = table[record.get._{0}]; }}", column.m_name);
			}
			file.Print("        record.InitRef(_ref);");
			file.Print("");
			foreach (var column in group_list) {
				string ID_type = ID_Enum == null ? "uint" : ID_Enum.EnumName;
				file.Print("        m_{0}[record.get._{0}][({1})record.get._IDINT] = record;", column.m_name, ID_type);
				file.Print("");
			}
			foreach (var column in weight_list) {
				if (column.m_group == null)
					file.Print("        m_{0}.Add(record.get._{0}, record);", column.m_name);
				else
					file.Print("        m_{0}[record.get._{1}].Add(record.get._{0}, record);", column.m_name, column.m_group.m_name);
				file.Print("");
			}
			foreach (var column in order_list) {
				if (column.m_group == null)
					file.Print("        m_{0}[record.get._{0}] = record;", column.m_name);
				else
					file.Print("        m_{0}[record.get._{1}][record.get._{0}] = record;", column.m_name, column.m_group.m_name);
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
