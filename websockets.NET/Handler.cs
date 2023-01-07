using System;
using System.Collections.Generic;
using System.Text;

namespace WebSocketsNET
{
	public class Handler
	{
		readonly Server server;

		internal Handler(Server server)
		{
			this.server = server;
		}




		public Handler AddSomething()
		{
			return this;
		}



		public Server Apply() => server;
	}
}
