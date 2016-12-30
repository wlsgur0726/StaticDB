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
					Config.All_Table.Add(name);
					Config.Target_Table.Add(name);
				}

				Config.Namespace = "Test";
				Config.Temp_Path = Path.Combine(Config.DB_Path, "temp");
				Config.Out_FBS_Path = Path.Combine(Config.DB_Path, "gen");
				Config.flatc_Path = Path.Combine(@"D:\windows\kjh\workspace\flatbuffertest", "flatc.exe");
				foreach (var table in Config.Target_Table) {
					var builder = Builder.s_instance.FindBuilder(table);
					if (builder.Build() == false)
						Console.Error.WriteLine("build fail - " + table);
				}
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
