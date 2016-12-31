using System;
using System.IO;

namespace StaticDB_Maker
{
	partial class Generator
	{
		public static void GenJS(Table table)
		{
			Printer file = new Printer(Path.Combine(Config.Out_CS_Path, table.m_name + "_loader.js"));
			file.Print("// {0}", Config.AutoGenComment);
			// TODO
			file.Flush();
		}
	}
}
