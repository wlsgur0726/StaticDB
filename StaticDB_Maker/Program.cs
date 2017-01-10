using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using System.Threading;

namespace StaticDB_Maker
{
	class Program
	{
		static void Test()
		{
			try {
				Config.DB_Path = @"D:\windows\kjh\workspace\git\StaticDB\example\DB\Test01";
				DirectoryInfo di = new DirectoryInfo(Config.DB_Path);
				var files = di.GetFiles();
				foreach (var file in files) {
					if (file.Extension.ToLower() != ".csv")
						continue;
					string name = Path.GetFileNameWithoutExtension(file.Name);
					string err = Common.CheckNamingRule(name);
					if (err.Length > 0) {
						Console.WriteLine("warning!!! ignore table : " + err);
						continue;
					}
					Config.TableID.Add(name, Config.TableID.Count);
					Config.Target_Table.Add(name);
				}

				Config.Namespace = "Test01";
				Config.Out_FBS_Path = @"D:\windows\kjh\workspace\git\StaticDB\example\gen_fbs\Test01";
				Config.Out_BIN_Path = @"D:\windows\kjh\workspace\git\StaticDB\example\gen_bin\Test01";
				Config.Out_CPP_Path = @"D:\windows\kjh\workspace\git\StaticDB\example\cpp\Test01";
				Config.flatc_Path = Path.Combine(@"D:\windows\kjh\workspace\flatbuffertest", "flatc.exe");
				foreach (var table in Config.Target_Table) {
					var builder = Builder.s_instance.FindBuilder(table);
					if (builder.Build_Step1() == false)
						throw new Exception("build fail - " + table);
				}
				foreach (var table in Config.Target_Table) {
					var builder = Builder.s_instance.FindBuilder(table);
					if (builder.Build_Step2() == false)
						throw new Exception("build fail - " + table);
				}
				Generator.GenDBCode_CPP();
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
			finally {
				try {
					//(new DirectoryInfo(Config.Temp_Path)).Delete(true);
				}
				catch (Exception) {
				}
			}
		}

		static void Main(string[] args)
		{
			Test();
		}
	}
}
