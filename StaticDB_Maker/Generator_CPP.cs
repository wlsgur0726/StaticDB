using System;
using System.IO;
using System.Collections.Generic;

namespace StaticDB_Maker
{
	partial class Generator
	{
		public static void GenDBCode_CPP()
		{
			List<string> tables = new List<string>();
			foreach (var it in Config.TableID)
				tables.Add(it.Key);

			Printer file = new Printer(Path.Combine(Config.Out_CPP_Path, Config.Namespace + "_DB.h"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("#pragma once");
			foreach (var table in tables)
				file.Print("#include \"{0}_Table.h\"", table);
			file.Print("");
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			file.Print("  class {0}_DB : public StaticDB::DB", Config.Namespace);
			file.Print("  {");
			file.Print("  protected:");
			file.Print("    virtual bool OnInitComplete() override;");
			file.Print("");
			file.Print("  public:");
			file.Print("    bool InitTables(const std::wstring& dir)");
			file.Print("    {");
			file.Print("      StaticDB::Tables tables;");
			file.Print("      tables.resize({0});", tables.Count);
			foreach (var table in tables)
				file.Print("      tables[StaticDB::GetTableIndex(TableID_{0}::Value)] = StaticDB::Table_ptr(new {0}_Table);", table);
			file.Print("      return StaticDB::DB::InitTables(dir, std::move(tables));");
			file.Print("    }");
			file.Print("");
			foreach (var table in tables) {
				file.Print("    inline const {0}_Table& {0}() const", table);
				file.Print("    {");
				file.Print("      auto ptr = m_tables[StaticDB::GetTableIndex(TableID_{0}::Value)].get();", table);
				file.Print("      return *static_cast<const {0}_Table*>(ptr);", table);
				file.Print("    }");
				file.Print("");
			}
			file.Print("  };");
			file.Print("}");
			file.Flush();
		}


		public static void GenTableCode_CPP(Table table)
		{
			List<Column_REF> ref_list = new List<Column_REF>();
			List<Column_GROUP> group_list = new List<Column_GROUP>();
			List<Column_WEIGHT> ratio_list = new List<Column_WEIGHT>();
			List<Column_ORDER> order_list = new List<Column_ORDER>();
			foreach (var column in table.m_schema.m_columns) {
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
						ratio_list.Add((Column_WEIGHT)column);
						break;
					}
					case ColumnType.ORDER: {
						order_list.Add((Column_ORDER)column);
						break;
					}
				}
			}

			Printer file = new Printer(Path.Combine(Config.Out_CPP_Path, table.m_name + "_Table.h"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("#pragma once");
			file.Print("#include \"StaticDB.h\"");
			file.Print("#include \"{0}_generated.h\"", table.m_name);
			foreach (var column in ref_list)
				file.Print("#include \"{0}_Table.h\"", column.m_refTable);
			file.Print("");
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			file.Print("  class {0}_Table;", table.m_name);
			foreach (var column in ref_list) {
				file.Print("  class {0}_Record;", column.m_refTable);
				file.Print("  class {0}_Table;", column.m_refTable);
			}
			file.Print("");
			file.Print("");
			file.Print("");
			file.Print("  class {0}_Record : public StaticDB::Record<{0}_FBS>", table.m_name);
			file.Print("  {");
			file.Print("  public:");
			file.Print("    friend class ::{0}::{1}_Table;", Config.Namespace, table.m_name);
			file.Print("    {0}_Record(const {0}_FBS* p = nullptr) : StaticDB::Record<{0}_FBS>(p) {{}}", table.m_name);
			file.Print("");
			foreach (var column in ref_list)
				file.Print("    inline const {0}_Record& Ref_{1}() const {{ return *m_ref->m_{1}; }}", column.m_refTable, column.m_name);
			file.Print("");
			file.Print("    inline int64_t GetInt({0}_Column column) const", table.m_name);
			file.Print("    {");
			file.Print("      switch(column) {");
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column, string name) =>
			{
				switch (column.m_type) {
					case ColumnType.STR:
					case ColumnType.RATE:
						return;
				}
				file.Print("        case {0}_Column::_{1}:", table.m_name, name);
				file.Print("          return static_cast<int64_t>(get()->_{0}());", name);
			});
			file.Print("      }");
			file.Print("      assert(false); // invalid column");
			file.Print("      return 0;");
			file.Print("    }");
			file.Print("");
			file.Print("    inline std::string GetStr({0}_Column column) const", table.m_name);
			file.Print("    {");
			file.Print("      switch(column) {");
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column, string name) =>
			{
				string ret;
				if (column.TypeInfo.fbs == "string")
					ret = String.Format("get()->_{0}()->str()", name);
				else if (column.TypeInfo.IsEnum())
					ret = String.Format("std::to_string(static_cast<uint32_t>(get()->_{0}()))", name);
				else
					ret = String.Format("std::to_string(get()->_{0}())", name);
				file.Print("        case {0}_Column::_{1}:", table.m_name, name);
				file.Print("          return {0};", ret);
			});
			file.Print("      }");
			file.Print("      assert(false); // invalid column");
			file.Print("      return std::string();");
			file.Print("    }");
			file.Print("");
			file.Print("    inline double GetReal({0}_Column column) const", table.m_name);
			file.Print("    {");
			file.Print("      switch(column) {");
			TableBuilder.Loop_FBS_Columns(table, (TableSchema.Column column, string name) =>
			{
				switch (column.m_type) {
					case ColumnType.STR:
						return;
				}
				file.Print("        case {0}_Column::_{1}:", table.m_name, name);
				file.Print("          return static_cast<double>(get()->_{0}());", name);
			});
			file.Print("      }");
			file.Print("      assert(false); // invalid column");
			file.Print("      return 0;");
			file.Print("    }");
			file.Print("");
			file.Print("  private:");
			file.Print("    struct Ref");
			file.Print("    {");
			foreach (var column in ref_list)
				file.Print("      const {0}_Record* m_{1} = nullptr;", column.m_refTable, column.m_name);
			file.Print("    };");
			file.Print("    const Ref* m_ref = nullptr;");
			file.Print("  };");
			file.Print("");
			file.Print("");
			file.Print("");
			file.Print("  class {0}_Table : public StaticDB::Table<{0}_FBS_Data, {0}_Record>", table.m_name);
			file.Print("  {");
			file.Print("  public:");
			file.Print("    typedef StaticDB::Table<{0}_FBS_Data, {0}_Record> BaseType;", table.m_name);
			file.Print("");
			file.Print("    virtual const wchar_t* GetTableFileName() const override");
			file.Print("    {");
			file.Print("      return L\"{0}.bin\";", table.m_name);
			file.Print("    }");
			file.Print("");
			EnumInfo ID = null;
			{
				if (table.m_enums.TryGetValue(Common.EnumName(table.m_name, "ID"), out ID)) {
					file.Print("    inline const Record& GetRecord({0} ID) const", ID.EnumName);
					file.Print("    {");
					file.Print("      return BaseType::GetRecord(static_cast<uint32_t>(ID));");
					file.Print("    }");
					file.Print("");
					file.Print("    inline const Record& operator[]({0} ID) const", ID.EnumName);
					file.Print("    {");
					file.Print("      return BaseType::GetRecord(static_cast<uint32_t>(ID));");
					file.Print("    }");
					file.Print("");
				}
			}
			foreach (var column in group_list) {
				string ID_type = ID == null ? "uint32_t" : ID.EnumName;
				string group_type = column.TypeInfo.types[TypeMapper.Type.CPP];
				file.Print("    typedef StaticDB::HashMap<{0}, {1}_Record> _{2}_Group;", ID_type, table.m_name, column.m_name);
				file.Print("    const _{0}_Group& _{0}({1} group_ID) const", column.m_name, group_type);
				file.Print("    {");
				file.Print("      auto it = m_{0}.find(group_ID);", column.m_name);
				file.Print("      if (it == m_{0}.end())", column.m_name);
				file.Print("        return StaticDB::Null<_{0}_Group>();", column.m_name);
				file.Print("      return it->second;");
				file.Print("    }");
				file.Print("");
			}
			foreach (var column in ratio_list) {
				if (column.m_group == null) {
					file.Print("    const {0}_Record& Pick_{1}() const", table.m_name, column.m_name);
					file.Print("    {");
					file.Print("      return m_{0}.Pick();", column.m_name);
					file.Print("    }");
				}
				else {
					string group_type = column.m_group.TypeInfo.types[TypeMapper.Type.CPP];
					file.Print("    const {0}_Record& Pick_{1}({2} group_ID) const", table.m_name, column.m_name, group_type);
					file.Print("    {");
					file.Print("      auto it = m_{0}.find(group_ID);", column.m_name);
					file.Print("      if (it == m_{0}.end())", column.m_name);
					file.Print("        return StaticDB::Null<{0}_Record>();", table.m_name);
					file.Print("      return it->second.Pick();");
					file.Print("    }");
				}
				file.Print("");
			}
			foreach (var column in order_list) {
				file.Print("    typedef std::map<uint32_t, {0}_Record> _{1}_Order;", table.m_name, column.m_name);
				if (column.m_group == null) {
					file.Print("    const _{0}_Order& _{0}() const", column.m_name);
					file.Print("    {");
					file.Print("      return m_{0};", column.m_name);
					file.Print("    }");
				}
				else {
					string group_type = column.TypeInfo.types[TypeMapper.Type.CPP];
					file.Print("    const _{0}_Order& _{0}({1} group_ID) const", column.m_name, group_type);
					file.Print("    {");
					file.Print("      auto it = m_{0}.find(group_ID);", column.m_name);
					file.Print("      if (it == m_{0}.end()", column.m_name);
					file.Print("        return StaticDB::Null<_{0}_Order>();", column.m_name);
					file.Print("      return it->second;");
					file.Print("    }");
				}
				file.Print("");
			}
			file.Print("");
			file.Print("  private:");
			file.Print("    typedef typename {0}_Record::Ref Ref;", table.m_name);
			file.Print("    std::unordered_map<uint32_t, std::unique_ptr<Ref>> m__refTable;");
			foreach (var column in group_list) {
				string ID_type = ID == null ? "uint32_t" : ID.EnumName;
				string group_type = column.TypeInfo.types[TypeMapper.Type.CPP];
				file.Print("    std::unordered_map<{0}, _{1}_Group> m_{1};", group_type, column.m_name);
			}
			foreach (var column in ratio_list) {
				if (column.m_group == null)
					file.Print("    StaticDB::Weight<{0}_Record> m_{1};", table.m_name, column.m_name);
				else {
					string group_type = column.m_group.TypeInfo.types[TypeMapper.Type.CPP];
					file.Print("    std::unordered_map<{0}, StaticDB::Weight<{1}_Record>> m_{2};", group_type, table.m_name, column.m_name);
				}
			}
			foreach (var column in order_list) {
				if (column.m_group == null)
					file.Print("    {0}_Order m_{0};", column.m_name);
				else {
					string group_type = column.TypeInfo.types[TypeMapper.Type.CPP];
					file.Print("    std::unordered_map<{0}, _{1}_Order> m_{1};", group_type, column.m_name);
				}
			}
			file.Print("");
			file.Print("    virtual void OnLoaded(const StaticDB::Tables& tables) override");
			file.Print("    {");
			file.Print("      for (auto& it : m_ID) {");
			file.Print("        auto& record = it.second;");
			file.Print("        auto& ref = m__refTable[it.first];");
			file.Print("        ref.reset(new Ref);");
			file.Print("        record.m_ref = ref.get();");
			file.Print("");
			foreach (var column in ref_list) {
				file.Print("        auto& ref_{0} = StaticDB::GetRecord<{1}_Table>(tables, TableID_{1}::Value, record->_{0}());", column.m_name, column.m_refTable);
				file.Print("        ref->m_{0} = &ref_{0};", column.m_name);
				file.Print("        if (ref->m_{0} == nullptr)", column.m_name);
				file.Print("          throw StaticDB::Message(\"null record, table:{0}, ID:\", static_cast<uint32_t>(record->_{1}()));", column.m_refTable, column.m_name);
				file.Print("");
			}
			foreach (var column in group_list) {
				file.Print("        m_{0}[record->_{0}()][record->_ID()] = record;", column.m_name);
				file.Print("");
			}
			foreach (var column in ratio_list) {
				if (column.m_group == null)
					file.Print("        m_{0}.Add(record->_{0}(), &record);", column.m_name);
				else
					file.Print("        m_{0}[record->_{1}()].Add(record->_{0}(), &record);", column.m_name, column.m_group.m_name);
				file.Print("");
			}
			foreach (var column in order_list) {
				if (column.m_group == null)
					file.Print("        m_{0}[record->_{0}()] = record;", column.m_name);
				else
					file.Print("        m_{0}[record->_{1}()][record->_{0}()] = record;", column.m_name, column.m_group.m_name);
				file.Print("");
			}
			file.Print("      }");
			file.Print("    }");
			file.Print("  };");
			file.Print("}");
			file.Flush();
		}
	}
}

