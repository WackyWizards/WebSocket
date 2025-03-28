using System;
using System.Threading.Tasks;
using System.Text.Json;
using Sandbox;
using WebSocket.Schema;

namespace WebSocket.Tests;

[TestClass]
public class WebSocketConnectionTests
{
	private const ulong SteamId = 76561198355153721;
	private const string TestUri = "ws://localhost:8080";
	private const string TestServiceName = "Hangout";

	private static WebSocketConnection CreateTestConnection( bool useToken = false )
	{
		var scene = new Scene();
		var go = scene.CreateObject();
		var connection = go.AddComponent<WebSocketConnection>();
		connection.Uri = TestUri;

		if ( !useToken )
		{
			return connection;
		}

		connection.UseToken = true;
		connection.ServiceName = TestServiceName;

		return connection;
	}

	[TestMethod]
	public async Task ConnectWithoutToken_ValidConfiguration_ShouldSucceed()
	{
		var connection = CreateTestConnection();
		await connection.Connect();

		Assert.IsNotNull( connection.Socket, "WebSocket should be initialized" );
		Assert.AreEqual( TestUri, connection.Uri, "URI should match configured value" );
		Assert.IsFalse( connection.UseToken, "Token usage should be disabled" );
	}

	[TestMethod]
	public async Task SendMessage_ValidMessage_ShouldSucceed()
	{
		var connection = CreateTestConnection();
		await connection.Connect();

		var token = await Sandbox.Services.Auth.GetToken( connection.ServiceName );
		var message = new Message
		{
			Type = "onJoin", SteamId = SteamId.ToString(), Token = token
		};

		try
		{
			await connection.SendMessage( message );
		}
		catch ( Exception ex )
		{
			Assert.Fail( $"SendMessage should not throw an exception: {ex.Message}" );
		}
	}

	[TestMethod]
	public async Task MessageReceived_UnsolicitedMessage_EventShouldTrigger()
	{
		var connection = CreateTestConnection();
		await connection.Connect();

		var messageReceiveTcs = new TaskCompletionSource<Message>();

		connection.OnMessageReceived += ( msg ) =>
		{
			messageReceiveTcs.TrySetResult( msg );
		};

		var testMessage = new Message
		{
			Type = "UnsolicitedTest"
		};

		var method = typeof( WebSocketConnection ).GetMethod(
			"MessageReceived",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);

		method?.Invoke( connection, [JsonSerializer.Serialize( testMessage )] );

		var receivedMessage = await messageReceiveTcs.Task.WaitAsync( TimeSpan.FromSeconds( 2 ) );

		Assert.IsNotNull( receivedMessage, "Received message should not be null" );
		Assert.AreEqual( "UnsolicitedTest", receivedMessage.Type, "Message type should match" );
	}

	[TestMethod]
	public async Task Dispose_ActiveConnection_ShouldCleanupResources()
	{
		var connection = CreateTestConnection();
		await connection.Connect();

		try
		{
			connection.Dispose();
		}
		catch ( Exception ex )
		{
			Assert.Fail( $"Dispose should not throw an exception: {ex.Message}" );
		}
	}

	[TestMethod]
	[ExpectedException( typeof( Exception ) )]
	public async Task Connect_InvalidUri_ShouldThrowConnectionException()
	{
		var connection = CreateTestConnection();
		connection.Uri = "ws://nonexistent-server:8080";

		await connection.Connect();
	}

	[TestMethod]
	[ExpectedException( typeof( Exception ) )]
	public async Task SendMessage_BeforeConnection_ShouldThrowException()
	{
		var connection = CreateTestConnection();
		var message = new Message
		{
			Type = "PreConnectionTest"
		};

		await connection.SendMessage( message );
	}
}
