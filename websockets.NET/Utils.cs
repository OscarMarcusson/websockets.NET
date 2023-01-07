using System;
using System.Collections.Generic;
using System.Text;

namespace WebSocketsNET
{
	internal static class Utils
	{
		public static void PrintDefaultLog(string type, string message, ConsoleColor consoleColor)
		{
			Console.ForegroundColor = ConsoleColor.DarkCyan;
			Console.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.Write(" [");
			Console.ForegroundColor = consoleColor;
			Console.Write(type);
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.Write("] ");

			Console.ForegroundColor = consoleColor;
			Console.WriteLine(message.Replace("\n", "\n  "));
		}
	}
}
