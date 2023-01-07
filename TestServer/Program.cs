using WebSocketsNET;

using var server = new Server("localhost", 11311)
	.AddRootHandler<MessagePrinterHandler>()
	.Start()
	;

Console.ReadLine();




class MessagePrinterHandler : Handler
{
	public override Task HandleAsync(WebSocketConnection _, string message)
	{
		LogInfo(message);
		return Task.CompletedTask;
	}
}