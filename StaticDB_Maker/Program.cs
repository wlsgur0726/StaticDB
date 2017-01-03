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
				Config.DB_Path = @"D:\windows\kjh\Documents\temp\sdb";
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
					Config.TableID.Add(name, Config.TableID.Count+1);
					Config.Target_Table.Add(name);
				}

				Config.Namespace = "Test";
				Config.Out_FBS_Path = Path.Combine(Config.DB_Path, "gen");
				Config.Out_BIN_Path = Path.Combine(Config.DB_Path, "bin");
				Config.Out_CPP_Path = Path.Combine(Config.DB_Path, "cpp");
				Config.flatc_Path = Path.Combine(@"D:\windows\kjh\workspace\flatbuffertest", "flatc.exe");
				foreach (var table in Config.Target_Table) {
					var builder = Builder.s_instance.FindBuilder(table);
					if (builder.Build() == false)
						Console.Error.WriteLine("build fail - " + table);
				}
				Generator.GenDBCode_CPP();
			}
			finally {
				//(new DirectoryInfo(Config.Temp_Path)).Delete(true);
			}
		}

		static void Main(string[] args)
		{
			Test();
			//Path.
			//Console.WriteLine(Path.GetDirectoryName (@"D:\windows\kjh\Documents\temp\sdb\Item.csv"));
		}
	}
}
