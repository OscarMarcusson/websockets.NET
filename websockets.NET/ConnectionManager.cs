using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebSocketsNET
{
	internal class ConnectionManager
	{
		readonly object locker = new object();
		readonly List<WebSocketConnection> connections = new List<WebSocketConnection>();


		public void Connect(WebSocketConnection connection)
		{
			lock (locker)
				connections.Add(connection);
		}


		public void Disconnect(WebSocketConnection connection)
		{
			lock (locker)
				connections.Remove(connection);
		}


		public WebSocketConnection[] GetConnections()
		{
			lock (locker)
				return connections.ToArray();
		}
		public WebSocketConnection[] GetConnections(Func<WebSocketConnection, bool> filter)
		{
			lock (locker)
				return connections.Where(filter).ToArray();
		}
	}
}
