using System;
using System.Threading.Tasks;
using Sandbox;

namespace WebSocket.Schema;

public class RequestMessage : Message
{
	private RequestMessage( string type, string content, string steamId, string token )
	{
		Type = type;
		SteamId = steamId;
		Content = content;
		CorrelationId = Guid.NewGuid().ToString();
		Token = token;
	}

	public static async Task<RequestMessage> CreateAsync( string type, SteamId steamId, string content = null )
	{
		var token = await Sandbox.Services.Auth.GetToken( WebSocketConnection.Instance.ServiceName );
		return new RequestMessage( type, content, steamId.ToString(), token );
	}
}
