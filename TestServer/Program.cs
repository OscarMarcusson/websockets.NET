using WebSocketsNET;

var server = new Server("localhost", 11311)
				.AddRootHandler()
					.AddSomething()
					.Apply()
				.Start()
				;

Console.ReadLine();