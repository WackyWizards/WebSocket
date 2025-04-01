using System.Text.Json.Serialization;

namespace WebSocket.Schema;

/// <summary>
/// A response back from the WebSocket server.
/// </summary>
public class ResponseMessage : Message
{
	[JsonPropertyName( "success" )]
	public bool Success { get; init; }

	[JsonPropertyName( "error" )]
	public string Error { get; init; }
}
