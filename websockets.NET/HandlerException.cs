using System;
using System.Collections.Generic;
using System.Text;

namespace WebSocketsNET
{
	public class HandlerException : Exception
	{
		public HandlerException(string error) : base(error) { }
	}
}
