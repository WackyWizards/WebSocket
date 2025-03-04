using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
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

	public Sandbox.WebSocket Socket { get; set; }

	private readonly ConcurrentDictionary<string, TaskCompletionSource<ResponseMessage>> _pendingRequests = new();
	private readonly ConcurrentDictionary<string, CancellationTokenSource> _timeoutTokens = new();

	public event Action<Message> OnMessageReceived;

	private static Logger Log => new( "WebSocket" );

	protected override void OnStart()
	{
		Instance = this;
		
		Socket = new Sandbox.WebSocket();
		Socket.OnMessageReceived += MessageReceived;
		_ = Connect();

		base.OnStart();
	}

	protected override void OnDestroy()
	{
		Dispose();
		base.OnDestroy();
	}

	private async Task Connect()
	{
		try
		{
			await Socket.Connect( Uri );
			Log.Info( $"Connected to WebSocket server at {Uri}" );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to connect to WebSocket server: {ex.Message}" );
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
		}
	}

	private void MessageReceived( string message )
	{
		try
		{
			Message msg = message;
			if ( !string.IsNullOrEmpty( msg.CorrelationId ) && _pendingRequests.TryRemove( msg.CorrelationId, out var tcs ) )
			{
				if ( _timeoutTokens.TryRemove( msg.CorrelationId, out var cts ) )
				{
					cts.Cancel();
					cts.Dispose();
				}

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
		}
	}

	public async Task<ResponseMessage> SendRequest( RequestMessage request, int? timeout = null )
	{
		var tcs = new TaskCompletionSource<ResponseMessage>();
		_pendingRequests[request.CorrelationId] = tcs;

		if ( timeout.HasValue )
		{
			var timeoutTask = CreateTimeoutTask( request.CorrelationId, request.Type, timeout.Value );
			_ = timeoutTask;
		}

		try
		{
			await Socket.Send( request );
			Log.Info( $"Sent request {request.Type} with ID {request.CorrelationId}" );

			return await tcs.Task;
		}
		catch ( Exception ex )
		{
			_pendingRequests.TryRemove( request.CorrelationId, out _ );

			// Cancel and remove any timeouts
			if ( !_timeoutTokens.TryRemove( request.CorrelationId, out var cts ) )
			{
				throw new Exception( $"Error sending request: {ex.Message}" );
			}

			// ReSharper disable once MethodHasAsyncOverload
			cts.Cancel();
			cts.Dispose();

			throw new Exception( $"Error sending request: {ex.Message}" );
		}
	}

	private async Task CreateTimeoutTask( string correlationId, string requestType, int timeout )
	{
		var cts = new CancellationTokenSource();
		_timeoutTokens[correlationId] = cts;

		try
		{
			await Task.Delay( timeout, cts.Token );

			if ( _pendingRequests.TryRemove( correlationId, out var timeoutTcs ) )
			{
				timeoutTcs.TrySetException( new TimeoutException( $"Request {requestType} timed out after {timeout} seconds" ) );
			}
		}
		catch ( TaskCanceledException )
		{
			// This is expected if the response came before the timeout
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error in timeout handling: {ex.Message}" );
		}
		finally
		{
			// Clean up the timeout token
			if ( _timeoutTokens.TryRemove( correlationId, out var token ) )
			{
				token.Dispose();
			}
		}
	}

	public void Dispose()
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

		foreach ( var token in _timeoutTokens )
		{
			token.Value.Cancel();
			token.Value.Dispose();
		}
		_timeoutTokens.Clear();
	}
}
