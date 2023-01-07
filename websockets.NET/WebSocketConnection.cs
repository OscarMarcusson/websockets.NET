using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace WebSocketsNET
{
	public class WebSocketConnection : IDisposable
	{
		readonly TcpClient client;
		readonly NetworkStream stream;
		readonly StreamReader reader;
		readonly StreamWriter writer;

		public WebSocketConnection(TcpClient client, NetworkStream stream, StreamReader reader, StreamWriter writer)
		{
			this.client = client;
			this.stream = stream;
			this.reader = reader;
			this.writer = writer;
		}

		public void Dispose()
		{
			client.Close();
			writer.Dispose();
			reader.Dispose();
			stream.Dispose();
			client.Dispose();
		}
	}
}
