#include "StaticDB.h"
#include "asd/file.h"
#include "asd/iconvwrap.h"
#include <filesystem>


using namespace std::experimental;

namespace StaticDB
{
	bool DB::InitTables(asd::FString dir, Tables&& tables)
	{
		auto TryInit = [&]()
		{
			filesystem::path dirPath(dir.c_str());
			for (auto& table : tables) {
				if (table == nullptr) {
					asd_Assert(false, "null data");
					return false;
				}

				asd::Fstring filePath = dirPath / table->GetTableFileName();
				asd::File file(filePath.c_str(), _F("rb"));
				if (file.GetLastError() != 0) {
					asd_Assert(false,
							   "fail file.Open(), errno:{}, {}",
							   file.GetLastError(),
							   asd::ConvToM(filePath.c_str()));
					return false;
				}

				if (file.Seek(0, SEEK_END) != 0) {
					asd_Assert(false,
							   "fail file.Seek(), errno:{}, {}",
							   file.GetLastError(),
							   asd::ConvToM(filePath.c_str()));
					return false;
				}

				auto size = file.Tell();
				if (size < 0) {
					asd_Assert(false,
							   "fail file.Tell(), errno:{}, {}",
							   file.GetLastError(),
							   asd::ConvToM(filePath.c_str()));
					return false;
				}

				if (file.Seek(0, SEEK_SET) != 0) {
					asd_Assert(false,
							   "fail file.Seek(), errno:{}, {}",
							   file.GetLastError(),
							   asd::ConvToM(filePath.c_str()));
					return false;
				}

				std::vector<uint8_t> data;
				data.resize(size);
				for (size_t offset=0;;) {
					size_t r = file.Read(&data[offset], 1, data.size()-offset);
					if (r == 0) {
						if (offset < (size_t)size) {
							asd_Assert(false,
									   "fail file.Read(), errno:{}, {}",
									   file.GetLastError(),
									   asd::ConvToM(filePath.c_str()));
							return false;
						}
						break;
					}
					offset += r;
				}

				if (table->Init(std::move(data)) == false) {
					asd_Assert(false, "fail table->Init(), {}", typeid(*table).name());
					return false;
				}
			}
			m_tables = std::move(tables);
			return true;
		};

		bool ret = TryInit();
		OnInitComplete(ret);
		return ret;
	}
}
