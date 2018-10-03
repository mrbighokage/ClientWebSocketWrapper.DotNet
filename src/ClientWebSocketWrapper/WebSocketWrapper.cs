using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ClientWebSocketWrapper
{
    // Based on this sample:
    // https://code.msdn.microsoft.com/vstudio/The-simple-WebSocket-4524921c
    // Based on WampSharp WebSocketWrapperConnection:
    // https://github.com/Code-Sharp/WampSharp/blob/wampv2/src/net45/Extensions/WampSharp.WebSockets/WebSockets/WebSocketWrapperConnection.cs
    // Based on Code-Sharp/PoloniexWebSocketsApi Elad Zelingher
    // https://github.com/Code-Sharp/PoloniexWebSocketsApi/blob/master/src/PoloniexWebSocketsApi/PoloniexChannel.cs
    public class WebSocketWrapper : IDisposable
    {
        private readonly ClientWebSocket _webSocket;

        private static ILogger _logger;

        private readonly JsonSerializer _serializer;

        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        private readonly Uri _addressUri;

        public event MessageArrivedDelegate MessageArrived;

        public event Action ConnectionClosed;
        public event Action<Exception> ConnectionError;

        private bool IsConnected => _webSocket.State == WebSocketState.Open;
        private WebSocketMessageType WebSocketMessageType => WebSocketMessageType.Text;

        public delegate void MessageArrivedDelegate(string message);

        public WebSocketWrapper(Uri addressUri, ILogger logger = null)
        {
            _logger = logger;
            _webSocket = new ClientWebSocket();
            _serializer = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            _addressUri = addressUri;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public Task SendAsync(object command)
        {
            ArraySegment<byte> messageToSend = GetMessageInBytes(command);
            return _webSocket.SendAsync(messageToSend, WebSocketMessageType, true, _cancellationToken);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        private ArraySegment<byte> GetMessageInBytes(object command)
        {
            StringWriter writer = new StringWriter();
            JsonTextWriter tokenWriter = new JsonTextWriter(writer) { Formatting = Formatting.None };
            _serializer.Serialize(tokenWriter, command);
            string formatted = writer.ToString();

            byte[] bytes = Encoding.UTF8.GetBytes(formatted);

            return new ArraySegment<byte>(bytes);
        }

        public async Task ConnectAsync()
        {
            try
            {
                await _webSocket.ConnectAsync(_addressUri, _cancellationToken)
                          .ConfigureAwait(false);

                Task task = Task.Run(this.RunAsync, _cancellationToken);
            }
            catch (Exception ex)
            {
                RaiseConnectionError(ex);
                RaiseConnectionClosed();
            }
        }

        private async Task RunAsync()
        {
            try
            {
                /*We define a certain constant which will represent
                  size of received data. It is established by us and 
                  we can set any value. We know that in this case the size of the sent
                  data is very small.
                */
                const int maxMessageSize = 2048;

                // Buffer for received bits.
                ArraySegment<byte> receivedDataBuffer = new ArraySegment<byte>(new byte[maxMessageSize]);

                MemoryStream memoryStream = new MemoryStream();

                // Checks WebSocket state.
                while (IsConnected && !_cancellationToken.IsCancellationRequested)
                {
                    // Reads data.
                    WebSocketReceiveResult webSocketReceiveResult =
                        await ReadMessage(receivedDataBuffer, memoryStream).ConfigureAwait(false);

                    if (webSocketReceiveResult.MessageType != WebSocketMessageType.Close)
                    {
                        memoryStream.Position = 0;
                        OnNewMessage(memoryStream);
                    }

                    memoryStream.Position = 0;
                    memoryStream.SetLength(0);
                }
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException) ||
                    !_cancellationToken.IsCancellationRequested)
                {
                    RaiseConnectionError(ex);
                }
            }

            if (_webSocket.State != WebSocketState.CloseReceived && _webSocket.State != WebSocketState.Closed)
            {
                await CloseWebSocket().ConfigureAwait(false);
            }

            RaiseConnectionClosed();
        }

        private async Task CloseWebSocket()
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed sending a close message to client", ex);
            }
        }

        private async Task<WebSocketReceiveResult> ReadMessage(ArraySegment<byte> receivedDataBuffer, MemoryStream memoryStream)
        {
            WebSocketReceiveResult webSocketReceiveResult;

            do
            {
                webSocketReceiveResult = await _webSocket.ReceiveAsync(receivedDataBuffer, _cancellationToken).ConfigureAwait(false);

                await memoryStream.WriteAsync(receivedDataBuffer.Array,
                                              receivedDataBuffer.Offset,
                                              webSocketReceiveResult.Count,
                                              _cancellationToken)
                                   .ConfigureAwait(false);
            }
            while (!webSocketReceiveResult.EndOfMessage);

            return webSocketReceiveResult;
        }

        private void OnNewMessage(MemoryStream payloadData)
        {
            string message = new StreamReader(payloadData, Encoding.ASCII).ReadToEnd();
            RaiseMessageArrived(message);
        }

        protected virtual void RaiseMessageArrived(string message)
        {
            MessageArrived?.Invoke(message);
        }

        protected virtual void RaiseConnectionClosed()
        {
            _logger?.LogDebug("Connection has been closed");
            ConnectionClosed?.Invoke();
        }

        protected virtual void RaiseConnectionError(Exception ex)
        {
            _logger?.LogError("A connection error occured", ex);
            ConnectionError?.Invoke(ex);
        }
    }
}
