using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebSocketsNET.Protocols.SimpleEndPoint
{
	internal class Utils
	{
		public static bool ValidateUrl(string route) => route.All(x => char.IsLetterOrDigit(x) || x == '-' || x == '_' || x == '/');
	}
}
