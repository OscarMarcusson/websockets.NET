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
	public void Log(/*string id*/Test payload)
	{
		LogInfo($"You said: {payload.Value}");
	}


	static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { IncludeFields = true };
	protected override object DeserializeJson(Type type, string json)
	{
		try
		{
			var parsedJson = JsonSerializer.Deserialize(json, type, jsonSerializerOptions);
			if (parsedJson == null)
				throw new HandlerException("500 Internal Server Exception", "Could not parse JSON data");
			return parsedJson;
		}
		catch (Exception e)
		{
			throw new HandlerException("500 Internal Server Exception", $"Could not parse JSON data: {e.Message}");
		}
	}
}

public class Test
{
	public string Value = "";
}