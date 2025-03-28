using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Diagnostics;
using WebSocket.Schema;

namespace WebSocket;

public sealed class WebSocketConnection : Component, IDisposable
{
	public static WebSocketConnection Instance { get; private set; }

	[Property]
	public string Uri { get; set; }

	[Property, ToggleGroup( "UseToken", Label = "Use Token" )]
	public bool UseToken { get; set; } = false;

	[Property, ToggleGroup( "UseToken", Label = "Use Token" )]
	public string ServiceName { get; set; }

	public Sandbox.WebSocket Socket { get; set; }

	private readonly ConcurrentDictionary<string, TaskCompletionSource<ResponseMessage>> _pendingRequests = new();

	public event Action<Message> OnMessageReceived;

	private static Logger Log => new( "WebSocket" );

	protected override void OnStart()
	{
		Instance = this;
		base.OnStart();
	}

	protected override void OnDestroy()
	{
		Dispose();
		base.OnDestroy();
	}

	public async Task Connect()
	{
		try
		{
			Socket = new Sandbox.WebSocket();
			Socket.OnMessageReceived += MessageReceived;
			
			if ( UseToken )
			{
				var token = await Sandbox.Services.Auth.GetToken( ServiceName );
				if ( string.IsNullOrEmpty( token ) )
				{
					throw new InvalidOperationException( "Failed to fetch a valid session token for service: " + ServiceName );
				}

				var headers = new Dictionary<string, string>()
				{
					{
						"Authorization", token
					}
				};

				await Socket.Connect( Uri, headers );
			}
			else
			{
				await Socket.Connect( Uri );
			}

			Log.Info( $"Connected to WebSocket server." );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to connect to WebSocket server: {ex.Message}" );
			throw new Exception( "Failed to connect to WebSocket server", ex );
		}
	}

	public async Task SendMessage( Message message )
	{
		try
		{
			await Socket.Send( message );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to send WebSocket message: {ex.Message}" );
			throw new Exception( "Failed to send WebSocket message", ex );
		}
	}

	private void MessageReceived( string message )
	{
		try
		{
			Message msg = message;
			if ( !string.IsNullOrEmpty( msg.CorrelationId ) && _pendingRequests.TryRemove( msg.CorrelationId, out var tcs ) )
			{
				var response = JsonSerializer.Deserialize<ResponseMessage>( message );
				tcs.TrySetResult( response );
				Log.Info( $"Completed request with ID {msg.CorrelationId}" );
			}
			else
			{
				// This is an unsolicited message
				OnMessageReceived?.Invoke( msg );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error processing received message: {ex.Message}" );
			throw new Exception( "Error processing received message", ex );
		}
	}

	public async Task<ResponseMessage> SendRequest( RequestMessage request )
	{
		var tcs = new TaskCompletionSource<ResponseMessage>();
		_pendingRequests[request.CorrelationId] = tcs;

		try
		{
			await Socket.Send( request );
			Log.Info( $"Sent request {request.Type} with ID {request.CorrelationId}" );
			return await tcs.Task;
		}
		catch ( Exception ex )
		{
			_pendingRequests.TryRemove( request.CorrelationId, out _ );
			throw new Exception( $"Error sending request: {ex.Message}" );
		}
	}

	public void Dispose()
	{
		try
		{
			if ( Socket is not null )
			{
				Socket.OnMessageReceived -= MessageReceived;
				Socket.Dispose();
			}

			foreach ( var request in _pendingRequests )
			{
				request.Value.TrySetCanceled();
			}
			_pendingRequests.Clear();
		}
		catch ( Exception ex )
		{
			Log.Error( $"Failed to dispose of WebSocketConnection: {ex.Message}" );
		}
	}
}
