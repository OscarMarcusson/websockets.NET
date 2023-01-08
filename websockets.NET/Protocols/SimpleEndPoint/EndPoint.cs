using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketsNET.Protocols.SEP
{
	internal class EndPoint
	{
		public bool id;
		public bool payload;
		public bool payloadIsValueType;
		public Func<string?, string?, string?, Task>? handler;
	}
}
