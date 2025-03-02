using System;

namespace WebSocket.Schema;

public class RequestMessage : Message
{
	public RequestMessage( string type, string content = null )
	{
		Type = type;
		Content = content;
		CorrelationId = Guid.NewGuid().ToString();
	}
}
