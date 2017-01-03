#include "D:\windows\kjh\Documents\temp\sdb\cpp\Test_DB.h"

bool Test::Test_DB::OnInitComplete()
{
	return true;
}

int main()
{
	Test::Test_DB db;
	bool b = db.InitTables(L"D:\\windows\\kjh\\Documents\\temp\\sdb\\bin\\");
	auto rec = db.ItemGacha().Pick_GachaRatio(4);
	auto id = rec.ItemIDRef()->ID_INT();
	return 0;
}