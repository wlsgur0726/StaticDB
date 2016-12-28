using System;
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
			Config.DB_Path = @"D:\windows\kjh\Documents\temp\sdb";
			System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(Config.DB_Path);
			var files = di.GetFiles();
			foreach (var file in files) {
				if (file.Extension.ToLower() != ".csv")
					continue;
				string name = file.Name.Remove(file.Name.Length - 4);
				string err = Common.CheckNamingRule(name);
				if (err.Length > 0) {
					Console.WriteLine("warning!!! ignore table : " + err);
					continue;
				}
				Config.All_Table.Add(name);
				Config.Target_Table.Add(name);
			}

			Config.Namespace = "Test";
			Config.Out_FBS_Path = System.IO.Path.Combine(Config.DB_Path, "gen");

			foreach (var table in Config.Target_Table) {
				var builder = Builder.s_instance.FindBuilder(table);
				if (builder.Build() == false)
					Console.Error.WriteLine("build fail - " + table);
			}
		}

		static void Main(string[] args)
		{
			Test();
		}
	}
}
