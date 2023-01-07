using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsNET
{
	public abstract class Handler
	{
		internal Server? server;


		public abstract Task HandleAsync(string message);


		public void LogInfo(string message) => server!.LogInfo(message);
		public void LogError(string error) => server!.LogError(error);
	}
}
