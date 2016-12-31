using System;
using System.IO;

namespace StaticDB_Maker
{
	partial class Generator
	{
		public static void GenCS(Table table)
		{
			Printer file = new Printer(Path.Combine(Config.Out_CS_Path, table.m_name + "_loader.cs"));
			file.Print("// {0}", Config.AutoGenComment);
			// TODO
			file.Flush();
		}
	}
}
