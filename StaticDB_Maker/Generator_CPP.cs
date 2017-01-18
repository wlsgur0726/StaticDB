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

			file = new Printer(Path.Combine(Config.Out_CPP_Path, Config.Namespace + "_DB.cpp"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("#include \"{0}_DB.h\"", Config.Namespace);
			foreach (var table in tables)
				file.Print("#include \"{0}_Table.inl\"", table);
			file.Flush();
		}


		public static void GenTableCode_CPP(Table table)
		{
			CommonInfo ci = new CommonInfo(table);

			Printer file = new Printer(Path.Combine(Config.Out_CPP_Path, table.m_name + "_Table.h"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("#pragma once");
			file.Print("#include \"StaticDB.h\"");
			file.Print("#include \"{0}_generated.h\"", table.m_name);
			foreach (var field in ci.ref_list)
				file.Print("#include \"{0}_Table.h\"", field.m_refTable);
			file.Print("");
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			file.Print("  class {0}_Table;", table.m_name);
			foreach (var field in ci.ref_list) {
				file.Print("  class {0}_Record;", field.m_refTable);
				file.Print("  class {0}_Table;", field.m_refTable);
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
			foreach (var field in ci.ref_list) {
				file.Print("    inline const {0}_Record& Ref_{1}() const {{ return *m_ref->m_{1}; }}",
					field.m_refTable, field.m_name);
				file.Print("");
			}
			foreach (var field in ci.refGroup_list) {
				string ID_type = TypeName(field.m_refTable, "ID", "uint32_t");
				file.Print("    typedef StaticDB::HashMap<{0}, {1}_Record> {1}_{2}_Group;", ID_type, field.m_refTable, field.m_refField);
				file.Print("    inline const {0}_{1}_Group& Ref_{2}() const {{ return *m_ref->m_{2}; }}",
					field.m_refTable, field.m_refField, field.m_name);
				file.Print("");
			}
			foreach (var field in ci.refWeightGroup_list) {
				file.Print("    typedef StaticDB::Weight<{0}_Record> {0}_{1}_Weight;", field.m_refTable, field.m_refField);
				file.Print("    inline const {0}_{1}_Weight& Ref_{2}() const {{ return *m_ref->m_{2}; }}",
					field.m_refTable, field.m_refField, field.m_name);
				file.Print("");
			}
			foreach (var field in ci.refOrderGroup_list) {
				file.Print("    typedef std::map<int64_t, {0}_Record> {0}_{1}_Order;", field.m_refTable, field.m_refField);
				file.Print("    inline const {0}_{1}_Order& Ref_{2}() const {{ return *m_ref->m_{2}; }}",
					field.m_refTable, field.m_refField, field.m_name);
				file.Print("");
			}
			file.Print("    inline int64_t GetInt(uint32_t field) const {{ return GetInt(static_cast<{0}_Field>(field)); }}", table.m_name);
			file.Print("    inline int64_t GetInt({0}_Field field) const", table.m_name);
			file.Print("    {");
			file.Print("      switch(field) {");
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				switch (field.m_type) {
					case FieldType.STR:
					case FieldType.RATE:
						return;
				}
				file.Print("        case {0}_Field::_{1}:", table.m_name, field.m_name);
				file.Print("          return static_cast<int64_t>(ref()._{0}());", field.m_name);
			});
			file.Print("      }");
			file.Print("      assert(false); // invalid field");
			file.Print("      return 0;");
			file.Print("    }");
			file.Print("");
			file.Print("    inline std::string GetStr(uint32_t field) const {{ return GetStr(static_cast<{0}_Field>(field)); }}", table.m_name);
			file.Print("    inline std::string GetStr({0}_Field field) const", table.m_name);
			file.Print("    {");
			file.Print("      switch(field) {");
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				string ret;
				if (field.TypeInfo.fbs == "string")
					ret = String.Format("ref()._{0}()->str()", field.m_name);
				else if (field.TypeInfo.IsEnum())
					ret = String.Format("std::to_string(static_cast<uint32_t>(ref()._{0}()))", field.m_name);
				else
					ret = String.Format("std::to_string(ref()._{0}())", field.m_name);
				file.Print("        case {0}_Field::_{1}:", table.m_name, field.m_name);
				file.Print("          return {0};", ret);
			});
			file.Print("      }");
			file.Print("      assert(false); // invalid field");
			file.Print("      return std::string();");
			file.Print("    }");
			file.Print("");
			file.Print("    inline double GetReal(uint32_t field) const {{ return GetReal(static_cast<{0}_Field>(field)); }}", table.m_name);
			file.Print("    inline double GetReal({0}_Field field) const", table.m_name);
			file.Print("    {");
			file.Print("      switch(field) {");
			TableBuilder.Loop_FBS_Fields(table, (TableSchema.Field field) =>
			{
				switch (field.m_type) {
					case FieldType.STR:
						return;
				}
				file.Print("        case {0}_Field::_{1}:", table.m_name, field.m_name);
				file.Print("          return static_cast<double>(ref()._{0}());", field.m_name);
			});
			file.Print("      }");
			file.Print("      assert(false); // invalid field");
			file.Print("      return 0;");
			file.Print("    }");
			file.Print("");
			file.Print("  private:");
			file.Print("    struct Ref");
			file.Print("    {");
			foreach (var field in ci.ref_list)
				file.Print("      const {0}_Record* m_{1} = nullptr;", field.m_refTable, field.m_name);
			foreach (var field in ci.refGroup_list)
				file.Print("      const {0}_{1}_Group* m_{2} = nullptr;", field.m_refTable, field.m_refField, field.m_name);
			foreach (var field in ci.refWeightGroup_list)
				file.Print("      const {0}_{1}_Weight* m_{2} = nullptr;", field.m_refTable, field.m_refField, field.m_name);
			foreach (var field in ci.refOrderGroup_list)
				file.Print("      const {0}_{1}_Order* m_{2} = nullptr;", field.m_refTable, field.m_refField, field.m_name);
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
			file.Print("    using BaseType::GetRecord;");
			file.Print("    using BaseType::operator[];");
			if (ci.ID_Enum != null) {
				file.Print("    inline const Record& GetRecord({0} ID) const {{ return BaseType::GetRecord(static_cast<uint32_t>(ID)); }}", ci.ID_Enum.EnumName);
				file.Print("    inline const Record& operator[]({0} ID) const {{ return BaseType::GetRecord(static_cast<uint32_t>(ID)); }}", ci.ID_Enum.EnumName);
				file.Print("");
			}
			foreach (var field in ci.group_list) {
				string ID_type = ci.ID_Enum == null ? "uint32_t" : ci.ID_Enum.EnumName;
				string group_type = field.TypeInfo.types[TypeMapper.Type.CPP];
				file.Print("    typedef StaticDB::HashMap<{0}, {1}_Record> _{2}_Group;", ID_type, table.m_name, field.m_name);
				if (field.TypeInfo.IsEnum())
					file.Print("    inline const _{0}_Group& _{0}(uint32_t group_ID) const {{ return _{0}(static_cast<{1}>(group_ID)); }}", field.m_name, group_type);
				file.Print("    inline const _{0}_Group& _{0}({1} group_ID) const {{ return m_{0}[group_ID]; }}", field.m_name, group_type);
				file.Print("");
			}
			foreach (var field in ci.weight_list) {
				file.Print("    typedef StaticDB::Weight<{0}_Record> _{1}_Weight;", table.m_name, field.m_name);
				if (field.m_group == null)
					file.Print("    inline const _{0}_Weight& _{0}() const {{ return m_{0}; }}", field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CPP];
					if (field.m_group.TypeInfo.IsEnum())
						file.Print("    inline const _{0}_Weight& _{0}(uint32_t group_ID) const {{ return _{0}(static_cast<{1}>(group_ID)); }}", field.m_name, group_type);
					file.Print("    inline const _{0}_Weight& _{0}({1} group_ID) const {{ return m_{0}[group_ID]; }}", field.m_name, group_type);
				}
				file.Print("");
			}
			foreach (var field in ci.order_list) {
				file.Print("    typedef std::map<int64_t, {0}_Record> _{1}_Order;", table.m_name, field.m_name);
				if (field.m_group == null)
					file.Print("    inline const _{0}_Order& _{0}() const {{ return m_{0}; }}", field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CPP];
					if (field.m_group.TypeInfo.IsEnum())
						file.Print("    inline const _{0}_Order& _{0}(uint32_t group_ID) const {{ return _{0}(static_cast<{1}>(group_ID)); }}", field.m_name, group_type);
					file.Print("    inline const _{0}_Order& _{0}({1} group_ID) const {{ return m_{0}[group_ID]; }}", field.m_name, group_type);
				}
				file.Print("");
			}
			file.Print("");
			file.Print("  private:");
			file.Print("    typedef typename {0}_Record::Ref Ref;", table.m_name);
			file.Print("    std::unordered_map<uint32_t, std::unique_ptr<Ref>> m__refTable;");
			foreach (var field in ci.group_list) {
				string ID_type = ci.ID_Enum == null ? "uint32_t" : ci.ID_Enum.EnumName;
				string group_type = field.TypeInfo.types[TypeMapper.Type.CPP];
				file.Print("    StaticDB::HashMap<{0}, _{1}_Group> m_{1};", group_type, field.m_name);
			}
			foreach (var field in ci.weight_list) {
				if (field.m_group == null)
					file.Print("    _{0}_Weight m_{0};", field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CPP];
					file.Print("    StaticDB::HashMap<{0}, _{1}_Weight> m_{1};", group_type, field.m_name);
				}
			}
			foreach (var field in ci.order_list) {
				if (field.m_group == null)
					file.Print("    _{0}_Order m_{0};", field.m_name);
				else {
					string group_type = field.m_group.TypeInfo.types[TypeMapper.Type.CPP];
					file.Print("    StaticDB::HashMap<{0}, _{1}_Order> m_{1};", group_type, field.m_name);
				}
			}
			file.Print("");
			file.Print("    virtual const wchar_t* GetTableFileName() const override {{ return L\"{0}.bin\"; }}", table.m_name);
			file.Print("");
			file.Print("    virtual void OnLoaded1(const StaticDB::Tables& tables) override;");
			file.Print("    virtual void OnLoaded2(const StaticDB::Tables& tables) override;");
			file.Print("  };");
			file.Print("}");
			file.Flush();

			file = new Printer(Path.Combine(Config.Out_CPP_Path, table.m_name + "_Table.inl"));
			file.Print("// {0}", Config.AutoGenComment);
			file.Print("namespace {0}", Config.Namespace);
			file.Print("{");
			file.Print("  void {0}_Table::OnLoaded1(const StaticDB::Tables& tables)", table.m_name);
			file.Print("  {");
			file.Print("    for (auto& it : m_ID) {");
			file.Print("      auto& record = it.second;");
			file.Print("      auto& ref = m__refTable[it.first];");
			file.Print("      ref.reset(new Ref);");
			file.Print("      record.m_ref = ref.get();");
			file.Print("");
			foreach (var field in ci.ref_list) {
				file.Print("      auto& ref_{0} = StaticDB::GetRecord<{1}_Table>(tables, TableID_{1}::Value, record->_{0}());",
					field.m_name, field.m_refTable);
				file.Print("      ref->m_{0} = &ref_{0};", field.m_name);
				file.Print("      if (ref->m_{0} == nullptr)", field.m_name);
				file.Print("        throw StaticDB::Message(\"null record, table:{0}, ID:\", static_cast<uint32_t>(record->_{1}()));",
					field.m_refTable, field.m_name);
				file.Print("");
			}
			foreach (var field in ci.group_list) {
				file.Print("      m_{0}[record->_{0}()][record->_ID_INT()] = record;", field.m_name);
				file.Print("");
			}
			foreach (var field in ci.weight_list) {
				if (field.m_group == null)
					file.Print("      m_{0}.Add(record->_{0}(), &record);", field.m_name);
				else
					file.Print("      m_{0}[record->_{1}()].Add(record->_{0}(), &record);", field.m_name, field.m_group.m_name);
				file.Print("");
			}
			foreach (var field in ci.order_list) {
				if (field.m_group == null)
					file.Print("      m_{0}[record->_{0}()] = record;", field.m_name);
				else
					file.Print("      m_{0}[record->_{1}()][record->_{0}()] = record;", field.m_name, field.m_group.m_name);
				file.Print("");
			}
			file.Print("    }");
			file.Print("  }");
			file.Print("");
			file.Print("");
			file.Print("  void {0}_Table::OnLoaded2(const StaticDB::Tables& tables)", table.m_name);
			file.Print("  {");
			file.Print("    for (auto& it : m_ID) {");
			file.Print("      auto& record = it.second;");
			file.Print("      auto& ref = m__refTable[it.first];");
			foreach (var field in ci.refGroup_list) {
				file.Print("      {{ auto table = dynamic_cast<{0}_Table*>(tables[StaticDB::GetTableIndex(TableID_{0}::Value)].get());", field.m_refTable);
				file.Print("        ref->m_{0} = &table->_{1}(record->_{0}()); }}", field.m_name, field.m_refField);
			}
			foreach (var field in ci.refWeightGroup_list) {
				file.Print("      {{ auto table = dynamic_cast<{0}_Table*>(tables[StaticDB::GetTableIndex(TableID_{0}::Value)].get());", field.m_refTable);
				file.Print("        ref->m_{0} = &table->_{1}(record->_{0}()); }}", field.m_name, field.m_refField);
			}
			foreach (var field in ci.refOrderGroup_list) {
				file.Print("      {{ auto table = dynamic_cast<{0}_Table*>(tables[StaticDB::GetTableIndex(TableID_{0}::Value)].get());", field.m_refTable);
				file.Print("        ref->m_{0} = &table->_{1}(record->_{0}()); }}", field.m_name, field.m_refField);
			}
			file.Print("    }");
			file.Print("  }");
			file.Print("}");
			file.Flush();
		}
	}
}

