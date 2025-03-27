using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocket.Schema;

public class Message
{
	[JsonPropertyName( "type" )]
	public string Type { get; set; }
	
	[JsonPropertyName( "steamId" )]
	public string SteamId { get; set; }
	
	[JsonPropertyName( "token" )]
	public string Token { get; set; }

	[JsonPropertyName( "content" )]
	public string Content { get; set; }
	
	[JsonPropertyName("correlationId")]
	internal string CorrelationId { get; set; }

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
