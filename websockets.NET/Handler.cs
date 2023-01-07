using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsNET
{
	public abstract class Handler
	{
		internal Server? server;

		internal virtual void Connect(WebSocketConnection connection) { }
		internal virtual void Disconnect(WebSocketConnection connection) { }

		public abstract Task HandleAsync(WebSocketConnection connection, string message);


		public void LogInfo(string message) => server!.LogInfo(message);
		public void LogError(string error) => server!.LogError(error);
	}
}
