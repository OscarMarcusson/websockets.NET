using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Text;

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

		public Handler AddRootHandler()
		{
			LogInfo($"Added a root handler");

			return new Handler(this);
		}

		public Handler AddHandler(string url)
		{
			url = url.Trim(' ', '\t', '\\', '/');
			if (url.Length == 0)
				throw new ArgumentException("Empty url");

			LogInfo($"Added handler '{url}'");
			// TODO::
			return new Handler(this);
		}


		public Server Start()
		{
			tcpListener.Start();
			LogInfo($"Started on '{tcpListener.LocalEndpoint}'");
			Task.Run(AcceptConnectionsAsync);
			return this;
		}


		async Task AcceptConnectionsAsync()
		{
			while (true)
			{
				var client = await tcpListener.AcceptTcpClientAsync();
				var requestHandler = new ConnectionRequestHandler(this);
				_ = Task.Run(async () => await requestHandler.HandleConnectionRequestAsync(client, AddConnection));
			}
		}

		void AddConnection(WebSocketConnection connection, string url)
		{
			if(url.Length == 0)
			{
				// TODO:: Check that we have a root handler
			}
			else
			{
				// TODO:: Check some dictionary
			}

			connection.Dispose();
		}

		internal void LogInfo(string message)
		{
			lock (logLocker)
			{
				if (logInfo != null)
					logInfo(message);
				else
					Utils.PrintDefaultLog("INF", message, ConsoleColor.Gray);
			}
		}

		internal void LogError(string error)
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
