using System.Text.Json.Serialization;

namespace WebSocket.Schema;

public class ResponseMessage : Message
{
	[JsonPropertyName( "success" )]
	public bool Success { get; init; }

	[JsonPropertyName( "error" )]
	public string Error { get; init; }
}
