using System;
using System.IO;

namespace StaticDB_Maker
{
	partial class Generator
	{
		public static void GenCPP(Table table)
		{
			Printer file = new Printer(Path.Combine(Config.Out_CPP_Path, table.m_name + "_loader.h"));
			file.Print("// {0}", Config.AutoGenComment);
			// TODO
			file.Flush();
		}
	}
}
