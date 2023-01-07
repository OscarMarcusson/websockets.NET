using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsNET
{
	internal class ConnectionRequestHandler
	{
		readonly Server server;

		public ConnectionRequestHandler(Server server) => this.server = server;


		public async Task HandleConnectionRequestAsync(TcpClient client, Action<WebSocketConnection,string> onSuccessCallback)
		{
			var stream = client.GetStream();
			var reader = new StreamReader(stream);
			try
			{
				// Start line
				var startLine = await reader.ReadLineAsync();
				if (startLine == null)
					throw new Exception("No data available");

				if (!startLine.StartsWith("GET "))
					throw new Exception($"Invalid request, expected 'GET': {startLine}");

				var url = startLine.Substring(4).Trim(' ', '\t', '/', '\\');

				var httpVersionIndex = url.LastIndexOf("HTTP/");
				if (httpVersionIndex < 0)
					throw new Exception($"Invalid request, expected an HTTP version at the end: {startLine}");

				var httpVersion = httpVersionIndex == 0 ? url : url.Substring(httpVersionIndex);
				if (httpVersion != "HTTP/1.1")
					throw new Exception($"Invalid request HTTP version: {httpVersion}");

				url = httpVersionIndex == 0 ? "" : url.Substring(0, httpVersionIndex).TrimEnd();

				// Headers
				var connectionHeaderIsValid = false;
				var upgradeHeaderIsValid = false;
				var webSocketVersionHeaderIsValid = false;
				string? webSocketKey = null;
				while (true)
				{
					var header = await reader.ReadLineAsync();
					if (string.IsNullOrWhiteSpace(header))
						break;

					var splitIndex = header.IndexOf(':');
					if (splitIndex < 0)
						throw new Exception($"Invalid header: {header}");

					var key = header.Substring(0, splitIndex).Trim();
					var value = header.Substring(splitIndex + 1).Trim();

					switch (key.ToLower())
					{
						case "connection":
							if (connectionHeaderIsValid) throw new Exception($"Duplicate 'Connection'");
							if (!value.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) throw new Exception($"Invalid 'Connection' value. Expected 'Upgrade', got '{value}'");
							connectionHeaderIsValid = true;
							break;

						case "upgrade":
							if (upgradeHeaderIsValid) throw new Exception($"Duplicate 'Upgrade'");
							if (!value.Equals("websocket", StringComparison.OrdinalIgnoreCase)) throw new Exception($"Invalid 'Upgrade' value. Expected 'websocket', got '{value}'");
							upgradeHeaderIsValid = true;
							break;

						case "sec-websocket-version":
							if (webSocketVersionHeaderIsValid) throw new Exception($"Duplicate 'Sec-WebSocket-Version'");
							if (value != "13") throw new Exception($"Invalid 'Sec-WebSocket-Version' value. Expected '13', got '{value}'");
							webSocketVersionHeaderIsValid = true;
							break;

						case "sec-websocket-key":
							if (webSocketKey != null) throw new Exception($"Duplicate 'Sec-WebSocket-Key'");
							webSocketKey = value;
							break;

							// Sec-WebSocket-Extensions?
					}
				}

				if (!connectionHeaderIsValid) throw new Exception("Expected a 'Connection' header");
				if (!upgradeHeaderIsValid) throw new Exception("Expected a 'Upgrade' header");
				if (!webSocketVersionHeaderIsValid) throw new Exception("Expected a 'Sec-WebSocket-Version' header");
				if (webSocketKey == null) throw new Exception("Expected a 'Sec-WebSocket-Key' header");

				server.LogInfo($"'{client.Client.RemoteEndPoint}' requested {(url.Length == 0 ? "the root handler" : $"'{url}'")}");
				
				// Response
				webSocketKey += "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; // Magic string from RFC 6455
				using (var sha1 = System.Security.Cryptography.SHA1.Create())
				{
					var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(webSocketKey));
					webSocketKey = Convert.ToBase64String(bytes);
				}

				var response = Encoding.UTF8.GetBytes(
					string.Join("\r\n",
						"HTTP/1.1 101 Switching Protocols",
						"Connection: Upgrade",
						"Upgrade: websocket",
						$"Sec-WebSocket-Accept: {webSocketKey}",
						"\r\n"));

				await stream.WriteAsync(response, 0, response.Length);
				onSuccessCallback(new WebSocketConnection(client, stream, reader, new StreamWriter(stream)), url);
			}
			catch (Exception e)
			{
				server.LogError($"Connection '{client.Client.RemoteEndPoint}': {e.Message}");
				reader.Dispose();
				stream.Dispose();
				client.Dispose();
			}
		}
	}
}
