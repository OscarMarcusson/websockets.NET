using System.Text.Json;
using WebSocketsNET;
using WebSocketsNET.Protocols;
using WebSocketsNET.Protocols.SEP;

using var server = new Server("localhost", 11311)
	.AddRootHandler<MessagePrinterHandler>()
	.AddHandler<SimpleEndPointExample>("sep")
	.Start()
	;

Console.ReadLine();




class MessagePrinterHandler : Handler
{
	public override async Task HandleAsync(WebSocketConnection connection, string message)
	{
		LogInfo(message);
		await connection.Send($"Thanks for the message of {message.Length} characters!");
		await Broadcast(connection, $"'{connection.GetEndPoint}' said: {message}");
	}
}


public class SimpleEndPointExample : SimpleEndPointHandler
{
	[Route("log")]
	public void Log(Test payload)
	{
		LogInfo($"You said: {payload.Value}");
	}

	[Route("log/id")]
	public void Log(string id, Test payload)
	{
		LogInfo($"{id} said: {payload.Value}");
	}
}

public class Test
{
	public string Value = "";
}