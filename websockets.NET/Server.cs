using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using WebSocketsNET.Protocols;
using WebSocketsNET.Protocols.SEP;

namespace WebSocketsNET
{
	public class Server : IDisposable
	{
		readonly TcpListener tcpListener;
		readonly object logLocker = new object();
		Action<string>? logInfo;
		Action<string>? logError;

		Handler? rootHandler;
		public Handler? GetRootHandler => rootHandler;

		readonly Dictionary<string, Handler> handlers = new Dictionary<string, Handler>();
		public bool TryGetHandler(string url, out Handler? handler) => handlers.TryGetValue(url, out handler);



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

		public Server AddRootHandler<THandler>() where THandler : Handler, new()
		{
			if (rootHandler != null)
				throw new ArgumentException("A root handler already exists");

			LogInfo($"Using '{typeof(THandler).Name}' as the root handler");
			rootHandler = new THandler() { server = this };
			return this;
		}

		public Server AddHandler<THandler>(string url) where THandler : Handler, new()
		{
			if (typeof(THandler) == typeof(SimpleEndPointHandler))
				throw new ArgumentException($"Invalid handler type, can't use the base '{typeof(SimpleEndPointHandler).Name}' type. Please use a class that inherits from it");

			if (typeof(THandler) == typeof(Handler))
				throw new ArgumentException($"Invalid handler type, can't use the base '{typeof(Handler).Name}' type. Please use a class that inherits from it");

			url = url.Trim(' ', '\t', '\\', '/');
			if (url.Length == 0)
				throw new ArgumentException("Empty url");

			if (url.Any(x => !char.IsLetterOrDigit(x) && x != '_' && x != '-'))
				throw new ArgumentException($"Invalid characters found in url '{url}'");

			if (handlers.ContainsKey(url))
				throw new ArgumentException($"'{url}' is already defined");

			LogInfo($"Added handler '{url}'");
			var handler = new THandler() { server = this };
			handlers[url] = handler;
			return this;
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
				_ = Task.Run(async () => await requestHandler.HandleConnectionRequestAsync(client));
			}
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
