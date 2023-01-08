using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketsNET
{
	public class WebSocketConnection : IDisposable
	{
		readonly Server server;
		readonly Handler handler;

		readonly TcpClient client;
		readonly NetworkStream stream;
		readonly StreamReader reader;
		readonly StreamWriter writer;
		readonly Task readLoopTask;
		readonly CancellationTokenSource cancellation;
		readonly CancellationToken cancellationToken;

		public EndPoint GetEndPoint => client.Client.RemoteEndPoint;

		internal WebSocketConnection(Server server, Handler handler, TcpClient client, NetworkStream stream, StreamReader reader, StreamWriter writer)
		{
			this.server = server;
			this.handler = handler;

			this.client = client;
			this.stream = stream;
			this.reader = reader;
			this.writer = writer;

			cancellation = new CancellationTokenSource();
			cancellationToken = cancellation.Token;

			handler.Connect(this);
			readLoopTask = Task.Run(ReadLoopAsync);
		}

		async Task ReadLoopAsync()
		{
			var bytes = new byte[16];
			var buffer = new byte[16]; // TODO:: Implement later
			try
			{
				while (!cancellation.IsCancellationRequested)
				{
					var read = await stream.ReadAsync(bytes, cancellationToken);
					if (read <= 0)
					{
						server.LogError($"Reached end of stream, killing connection for '{client.Client.RemoteEndPoint}'");
						break;
					}
					var ulongRead = (ulong)read;

					// var fin = (bytes[0] & 0b10000000) != 0; // Not used for now
					var mask = (bytes[1] & 0b10000000) != 0;
					// The mask should always be set. If not, we kill the connection
					if (!mask)
					{
						server.LogError($"The mask bit was not set, killing connection for '{client.Client.RemoteEndPoint}'");
						break;
					}

					int opcode = bytes[0] & 0b00001111; // 0: continuation frame, 1: text message, 2: binary, 9: ping, 10: pong
					if (opcode == 8) // End of stream
						break;

					ulong offset = 2;
					ulong messageLength = (ulong)(bytes[1] & 0b01111111);

					if (messageLength == 126)
					{
						// Reverse bytes, websocket uses big-endian and bitconverter does not
						messageLength = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
						offset = 4;
					}
					else if (messageLength == 127)
					{
						// Reverse bytes, websocket uses big-endian and bitconverter does not
						messageLength = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
						offset = 10;
					}

					if (messageLength == 0)
					{
						server.LogError($"The message length was zero, killing connection for '{client.Client.RemoteEndPoint}'");
						break;
					}

					// To allow for insane lengths we do this uggly thing where we just take a
					// small number of bytes first and then read the exact number of remaning
					// bytes based on the message length resolved from the first few.
					// TODO:: Find some way to ease the GC preassure from this...
					var expectedTotalLength = messageLength + offset + 4;
					var messageBytes = new byte[expectedTotalLength];
					for (int i = 0; i < read; i++)
						messageBytes[i] = bytes[i];

					if (expectedTotalLength > ulongRead)
					{
						var remainingBytesToRead = expectedTotalLength - ulongRead;
						await stream.ReadAsync(messageBytes, read, (int)remainingBytesToRead, cancellationToken);
					}
					else if(expectedTotalLength < ulongRead)
					{
						throw new NotImplementedException("Read more than the message length, need to implement something here");
					}

					byte[] decoded = new byte[messageLength];
					byte[] masks = new byte[4] { messageBytes[offset], messageBytes[offset + 1], messageBytes[offset + 2], messageBytes[offset + 3] };
					offset += 4;

					for (ulong i = 0; i < messageLength; ++i)
						decoded[i] = (byte)(messageBytes[offset + i] ^ masks[i % 4]);

					string text = Encoding.UTF8.GetString(decoded);
					await handler.HandleAsync(this, text);
				}
			}
			catch(Exception e)
			{
				server.LogError($"Unhandled exception, killing connection for '{client.Client.RemoteEndPoint}'\n{e}");
			}

			cancellation.Cancel();
			Dispose();
		}


		public async Task Send(string message)
		{
			var frameLength = 2;
			var messageBytes = Encoding.UTF8.GetBytes(message);

			if (messageBytes.Length >= 126)
			{
				if (messageBytes.Length < ushort.MaxValue) frameLength = 4;
				else frameLength = 10;
			}

			var totalMessageLength = frameLength + 4 + messageBytes.Length;
			var bytes = new byte[totalMessageLength];

			// Fin && OP code
			bytes[0] = 0b10000000 | (2 & 0x0F);

			// Frame length
			switch (frameLength)
			{
				case 10: bytes[1] = 127; break; // TODO:: Encode bits 2 & 3
				case 4: bytes[1] = 126; break; // TODO:: Encode all the way up
				default: bytes[1] = (byte)messageBytes.Length; break;
			}

			// Message
			var offset = frameLength; // + 4;
			for (var i = 0l; i < messageBytes.LongLength; ++i)
				bytes[i + offset] = (byte)(messageBytes[i]);

			await stream.WriteAsync(bytes, cancellationToken);
		}



		public void Dispose()
		{
			if (!cancellation.IsCancellationRequested)
			{
				cancellation.Cancel();
				readLoopTask.Wait(100); // TODO:: Setting for this?
			}

			client.Close();
			writer.Dispose();
			reader.Dispose();
			stream.Dispose();
			client.Dispose();

			handler.Disconnect(this);
		}
	}
}
