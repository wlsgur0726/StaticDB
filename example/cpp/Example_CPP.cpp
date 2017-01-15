#include "stdafx.h"

int main()
{
	Test01::Test01_DB db;
	bool b;
	b = db.InitTables(L"D:\\windows\\kjh\\workspace\\git\\StaticDB\\example\\gen_bin\\Test01"); assert(b);

	return 0;
}

#define IterateField(TableName, var)\
	for (uint32_t var=(uint32_t)TableName##_Field::MIN; var<=(uint32_t)TableName##_Field::MAX; ++var)

bool Test01::Test01_DB::OnInitComplete()
{
	//return true;
	std::cout << "\n" << "T1 all records\n";
	for (auto& it : T1()) {
		std::cout << "  " << it.first << "\n";
		IterateField(Test01::T1, col) {
			std::cout << "    " << Test01::EnumNameT1_Field(static_cast<Test01::T1_Field>(col));
			std::cout << "  :  " << it.second.GetStr(col) << '\n';
		}
	}

	std::cout << "\n" << "T1 colGROUP1\n";
	{
		uint32_t groupID;
		groupID = 111;
		std::cout << "  " << groupID << "\n";
		{
			auto& group = T1()._colGROUP1(groupID);
			std::cout << "    ";
			for (auto& it : group)
				std::cout << static_cast<uint32_t>(it.first) << ' ';
			std::cout << '\n';
		}
		groupID = 222;
		std::cout << "  " << groupID << "\n";
		{
			auto& group = T1()._colGROUP1(groupID);
			std::cout << "    ";
			for (auto& it : group)
				std::cout << static_cast<uint32_t>(it.first) << ' ';
			std::cout << '\n';
		}
		groupID = 333;
		std::cout << "  " << groupID << "\n";
		{
			auto& group = T1()._colGROUP1(groupID);
			std::cout << "    ";
			for (auto& it : group)
				std::cout << static_cast<uint32_t>(it.first) << ' ';
			std::cout << '\n';
		}
	}
	return true;
}