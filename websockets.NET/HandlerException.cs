using System;
using System.Collections.Generic;
using System.Text;

namespace WebSocketsNET
{
	public class HandlerException : Exception
	{
		public readonly string code;
		public string? url;

		public HandlerException(string code, string error) : base(error)
		{
			this.code = code;
		}

		public HandlerException(string code, string url, string error) : base(error)
		{
			this.code = code;
			this.url = url;
		}

		public override string Message =>
			url == null
				? $"{code}\n{base.Message}"
				: $"{code}: {url}\n{base.Message}"
				;
	}
}
