using WebSocketsNET;

var server = new Server("localhost", 11311)
				.AddHandler("test")
				.Start()
				;

Console.ReadLine();