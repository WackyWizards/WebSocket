using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocket.Schema;

public class Message
{
	[JsonPropertyName( "type" )]
	public string Type { get; init; }

	[JsonPropertyName( "content" )]
	public string Content { get; init; }
	
	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; }

	public static implicit operator Message( string json )
	{
		if ( string.IsNullOrEmpty( json ) )
		{
			return new Message();
		}

		try
		{
			return JsonSerializer.Deserialize<Message>( json ) ?? new Message();
		}
		catch ( JsonException )
		{
			return new Message();
		}
	}

	public static implicit operator string( Message message )
	{
		if ( message is null )
		{
			return "";
		}

		try
		{
			return JsonSerializer.Serialize( message );
		}
		catch ( JsonException )
		{
			return "";
		}
	}
}
