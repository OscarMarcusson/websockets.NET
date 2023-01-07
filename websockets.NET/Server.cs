using System;
using System.Net.Sockets;
using System.Net;

namespace WebSocketsNET
{
	public class Server : IDisposable
	{
		readonly TcpListener tcpListener;
		readonly object logLocker = new object();
		Action<string>? logInfo;
		Action<string>? logError;

		public Server(string ip, int port) : this(
				ip.Trim().Equals("localhost", StringComparison.OrdinalIgnoreCase)
					? IPAddress.Loopback
					: IPAddress.Parse(ip),
				port)
		{
		}
		public Server(IPAddress ip, int port)
		{
			tcpListener = new TcpListener(ip, port);
		}

		public Server AddLogger(Action<string> info, Action<string> error)
		{
			logInfo = info;
			logError = error;
			return this;
		}

		public Server AddHandler(string url)
		{
			LogInfo($"Added handler '{url}'");
			// TODO::
			return this;
		}


		public Server Start()
		{
			tcpListener.Start();
			LogInfo($"Started on '{tcpListener.LocalEndpoint}'");
			return this;
		}





		void LogInfo(string message)
		{
			lock (logLocker)
			{
				if (logInfo != null)
					logInfo(message);
				else
					Utils.PrintDefaultLog("INF", message, ConsoleColor.Gray);
			}
		}

		void LogError(string error)
		{
			lock (logLocker)
			{
				if (logError != null)
					logError(error);
				else
					Utils.PrintDefaultLog("ERR", error, ConsoleColor.Red);
			}
		}


		public void Dispose() => tcpListener.Stop();
	}
}
