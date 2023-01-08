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
			if(consoleColor == ConsoleColor.Gray)
			{
				Console.WriteLine(message.Replace("\n", "\n  "));
			}
			// Non-standard colors are printed for only the first lin
			else
			{
				var newLineIndex = message.IndexOf('\n');
				if(newLineIndex < 0)
				{
					Console.WriteLine(message);
				}
				else
				{
					Console.WriteLine(message.Substring(0, newLineIndex));
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine("  " + message.Substring(newLineIndex+1).Replace("\n", "\n  "));
				}
			}
		}
	}
}
