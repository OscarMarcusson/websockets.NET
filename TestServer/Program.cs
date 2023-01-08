using WebSocketsNET;

using var server = new Server("localhost", 11311)
	.AddRootHandler<MessagePrinterHandler>()
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