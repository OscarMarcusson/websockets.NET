using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsNET
{
	public abstract class Handler
	{
		internal Server? server;
		readonly ConnectionManager connectionManager = new ConnectionManager();

		internal void Connect(WebSocketConnection connection)
		{
			connectionManager.Connect(connection);
			OnConnect(connection);
		}

		internal void Disconnect(WebSocketConnection connection)
		{
			connectionManager.Disconnect(connection);
			OnDisconnect(connection);
		}

		public virtual void OnConnect(WebSocketConnection connection) { }
		public virtual void OnDisconnect(WebSocketConnection connection) { }

		public abstract Task HandleAsync(WebSocketConnection connection, string message);

		public async Task Broadcast(string message)
		{
			var receivers = connectionManager.GetConnections();
			await Task.WhenAll(receivers.Select(async x => await x.Send(message)));
		}

		public async Task Broadcast(WebSocketConnection sender, string message)
		{
			var receivers = connectionManager.GetConnections(x => x != sender);
			await Task.WhenAll(receivers.Select(async x => await x.Send(message)));
		}

		public void LogInfo(string message) => server!.LogInfo(message);
		public void LogError(string error) => server!.LogError(error);
	}
}
