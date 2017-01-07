#pragma once
#include "flatbuffers/flatbuffers.h"
#include <string>
#include <sstream>
#include <fstream>
#include <iostream>
#include <filesystem>
#include <memory>
#include <functional>
#include <random>
#include <vector>
#include <map>
#include <unordered_map>
#include <typeinfo>



namespace StaticDB
{
	template <typename ARG>
	inline std::string Message_Internal(std::stringstream& ss, const ARG& arg)
	{
		ss << arg;
		return ss.str();
	}
	template <typename ARG, typename... ARGS>
	inline std::string Message_Internal(std::stringstream& ss, const ARG& arg, const ARGS&... args)
	{
		ss << arg;
		return Message_Internal(ss, args...);
	}
	template <typename... ARGS>
	inline std::string Message(const ARGS&... args)
	{
		std::stringstream ss;
		return Message_Internal(ss, args...);
	}



	template <typename T, typename... ARGS>
	const T& Null(const ARGS&... args)
	{
		static const T s_null(args...);
		return s_null;
	}



	template <typename K, typename V>
	class HashMap : public std::unordered_map<K, V>
	{
	public:
		using std::unordered_map<K, V>::operator[];
		const V& operator[](const K& key) const
		{
			auto it = find(key);
			if (it == end())
				return Null<V>();
			return it->second;
		}
	};



	template <typename T>
	std::mt19937_64& RandomGenrator()
	{
		thread_local std::random_device t_rd;
		thread_local std::uniform_int_distribution<T> t_dist;
		thread_local std::mt19937_64 t_generator(t_dist(t_rd));
		return t_generator;
	}



	template <typename RECORD>
	class Weight
	{
	public:
		void Add(uint32_t weight, const RECORD* record)
		{
			if (weight == 0)
				return;
			auto org = m_total;
			m_total += weight;
			if (m_total < org)
				throw Message("overflow error");
			m_candidate.emplace(m_total, record);
		}

		const RECORD& Pick() const
		{
			std::uniform_int_distribution<uint32_t> fGetRandomValue(1, m_total);
			uint32_t r = fGetRandomValue(RandomGenrator<uint32_t>());
			auto it = m_candidate.lower_bound(r);
			if (it == m_candidate.end())
				return Null<RECORD>();
			return *it->second;
		}

		const uint32_t Total() const
		{
			return m_total;
		}

	private:
		std::map<uint32_t, const RECORD*> m_candidate;
		uint32_t m_total = 0;
	};



	template <typename FBS_TYPE>
	class Record
	{
	public:
		typedef FBS_TYPE FBS;

		Record(const FBS* record = nullptr) : m_record(record) {}
		inline const FBS* get() const { return m_record; }
		inline operator const FBS*() const { return m_record; }
		inline const FBS* operator->() const { return m_record; }
		inline const FBS& operator*() const { return *m_record; }
		inline bool operator==(std::nullptr_t) const { return m_record == nullptr; }
		inline bool operator!=(std::nullptr_t) const { return m_record != nullptr; }
		inline bool operator==(const FBS* right) const { return m_record == right; }
		inline bool operator!=(const FBS* right) const { return m_record != right; }
		inline bool operator<(const FBS* right) const { return m_record < right; }
		inline bool operator>(const FBS* right) const { return m_record > right; }
		inline bool operator<=(const FBS* right) const { return m_record <= right; }
		inline bool operator>=(const FBS* right) const { return m_record >= right; }

	protected:
		const FBS* m_record;
	};



	class TableInterface;
	typedef std::unique_ptr<TableInterface> Table_ptr;
	typedef std::vector<Table_ptr> Tables;

	template <typename TABLE_ID>
	inline uint32_t GetTableIndex(TABLE_ID table_ID)
	{
		return static_cast<uint32_t>(table_ID);
	}

	template <typename TABLE, typename TABLE_ID, typename RECORD_ID>
	inline const typename TABLE::Record& GetRecord(const Tables& tables,
														  TABLE_ID table_ID,
														  RECORD_ID record_ID)
	{
		auto& table = tables[GetTableIndex(table_ID)];
		TABLE* cast = dynamic_cast<TABLE*>(table.get());
		return cast->GetRecord(record_ID);
	}



	class TableInterface
	{
	public:
		virtual ~TableInterface() {}
		virtual const wchar_t* GetTableFileName() const = 0;
		virtual void Init(std::vector<uint8_t>&& buffer) = 0;
		virtual void OnLoaded(const Tables& tables) = 0;
	};



	template <typename FBS_DATA, typename RECORD>
	class Table : public TableInterface
	{
	public:
		typedef FBS_DATA	FBS_Data;
		typedef RECORD		Record;

		inline const Record& GetRecord(uint32_t ID) const { return m_ID[ID]; }
		inline const Record& operator[](uint32_t ID) const { return m_ID[ID]; }

		typedef typename HashMap<uint32_t, Record>::const_iterator const_iterator;
		inline const_iterator begin() const { return m_ID.begin(); }
		inline const_iterator end() const { return m_ID.end(); }


	protected:
		virtual void Init(std::vector<uint8_t>&& buffer) override
		{
			m_buffer = std::move(buffer);
			m_data = flatbuffers::GetRoot<FBS_Data>(m_buffer.data());
			if (m_data == nullptr)
				throw Message(typeid(FBS_Data).name(), " - invalid buffer");

			flatbuffers::Verifier verifier(m_buffer.data(), m_buffer.size());
			if (m_data->Verify(verifier) == false)
				throw Message(typeid(FBS_Data).name(), " - fail verify");

			auto data = m_data->Data();
			if (data == nullptr)
				throw Message(typeid(FBS_Data).name(), " - data is null");

			m_ID.clear();
			for (auto p : *data) {
				Record record(p);
				uint32_t ID = static_cast<uint32_t>(record->_ID());
				if (m_ID.emplace(ID, record).second == false)
					throw Message(typeid(FBS_Data).name(), " - duplicate ID, ", ID);
			}
		}

		std::vector<uint8_t> m_buffer;
		const FBS_Data* m_data = nullptr;
		HashMap<uint32_t, Record> m_ID;
	};



	class DB
	{
	public:
		virtual ~DB() {}

	protected:
		bool InitTables(const std::wstring& dir, Tables&& tables)
		{
			bool ok = true;
			std::experimental::filesystem::path base_path(dir.c_str());
			for (auto& table : tables) {
				try {
					auto file_path = base_path / table->GetTableFileName();
					std::ifstream ifs(file_path.c_str(), std::ios::binary | std::ios::ate);
					if (ifs.is_open() == false)
						throw Message("failed to open file, ", file_path);
					size_t size = ifs.tellg();
					ifs.seekg(0);
					std::vector<uint8_t> buffer;
					buffer.resize(size);
					ifs.read((char*)buffer.data(), buffer.size());
					ifs.close();
					table->Init(std::move(buffer));
				}
				catch (const std::string& e) {
					ok = false;
					OnError(e);
				}
			}
			for (auto& table : tables) {
				try {
					table->OnLoaded(tables);
				}
				catch (const std::string& e) {
					ok = false;
					OnError(e);
				}
			}
			if (ok == false)
				return false;
			m_tables = std::move(tables);
			return OnInitComplete();
		}

		virtual void OnError(const std::string& errmsg)
		{
			std::cerr << (errmsg + '\n');
		}

		virtual bool OnInitComplete()
		{
			// if (verify == fail)
			//    return false;
			// return true;
			return true;
		};

		Tables m_tables;
	};
}