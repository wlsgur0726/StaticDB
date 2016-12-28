#pragma once
#include "flatbuffers/flatbuffers.h"
#include "asd/exception.h"
#include "asd/filedef.h"
#include <string>
#include <sstream>
#include <memory>
#include <typeinfo>
#include <unordered_map>
#include <functional>


namespace StaticDB
{
	class TableInterface
	{
	public:
		virtual ~TableInterface() {}
		virtual const asd::FChar* GetTableFileName() const = 0;
		virtual bool Init(std::vector<uint8_t>&& data) = 0;
	};



	template <typename FBS_TABLE, typename FBS_RECORD>
	class Table : public TableInterface
	{
	public:
		typedef FBS_TABLE	FBS_Table;
		typedef FBS_RECORD	FBS_Record;

		inline const FBS_Record& GetRecord(int64_t ID) const
		{
			auto it = m_indexer_ID_INT.find(ID);
			asd_Assert(it != m_indexer_ID_INT.end(),
					   "{} - invalid ID {}", typeid(FBS_Table).name(), ID);
			return *(it->second);
		}

		inline const FBS_Record& GetRecord(const std::string& ID) const
		{
			auto it = m_indexer_ID_STRING.find(ID);
			asd_Assert(it != m_indexer_ID_STRING.end(),
					   "{} - invalid ID {}", typeid(FBS_Table).name(), ID);
			return *(it->second);
		}

	protected:
		std::vector<uint8_t> m_data;
		const FBS_Table* m_table = nullptr;
		std::unordered_map<int64_t, FBS_Record*> m_indexer_ID_INT;
		std::unordered_map<asd::MString, FBS_Record*> m_indexer_ID_STRING;

		virtual bool Init(std::vector<uint8_t>&& data) override
		{
			m_data = std::move(data);
			m_table = flatbuffers::GetRoot<FBS_Table>(m_data.data());
			m_indexer_ID_INT.clear();
			m_indexer_ID_STRING.clear();

			if (m_table == nullptr) {
				asd_Assert(false, "{} - no data", typeid(FBS_Table).name());
				return false;
			}

			auto records = m_table->Data();
			if (records == nullptr) {
				asd_Assert(false, "{} - no data", typeid(FBS_Table).name());
				return false;
			}

			for (auto record : *records) {
				auto ID_INT = record->ID_INT();
				if (m_indexer_ID_INT.emplace(ID_INT, record).second == false) {
					asd_Assert(false, "{} - duplicate ID_INT {}", typeid(FBS_Table).name(), ID_INT);
					return false;
				}
				auto ID_STRING = record->ID_STRING();
				if (ID_STRING!=nullptr && ID_STRING.size()>0) {
					asd::MString str(ID_STRING->data());
					if (m_indexer_ID_STRING.emplace(str, record).second == false) {
						asd_Assert(false, "{} - duplicate ID_STRING {}", typeid(FBS_Table).name(), str);
						return false;
					}
				}
			}
			return true;
		}
	};



	class DB
	{
	public:
		typedef std::vector<std::unique_ptr<TableInterface>> Tables;

		bool InitTables(asd::FString dir, Tables&& tables);

		virtual void OnInitComplete(bool success) {};

	protected:
		Tables m_tables;

	};
}