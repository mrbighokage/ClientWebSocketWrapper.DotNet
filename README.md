# ClientWebSocketWrapper.DotNet <img src="http://danpatrascu.com/wp-content/uploads/2017/12/download.png" width="56" />

## Event-based Wrapper for the ClientWebSocket

Compatible with .NETStandard 2.0

## Installation
The package is available in NuGet, or feel free to download:
https://www.nuget.org/packages/ClientWebSocketWrapper

**Nuget PM**
```
Install-Package ClientWebSocketWrapper -Version 1.0.0
```

## Usage example [Test project](https://github.com/mrbighokage/ClientWebSocketWrapper.DotNet/blob/develop/src/ClientWebSocketWrapper.Tests/Program.cs)

### Test exchange GDAX

```csharp
var uri = new Uri("wss://ws-feed.gdax.com");

using (var socket = new WebSocketWrapper(uri))
{
    // check events
    socket.MessageArrived += (message) =>
    {
        Console.WriteLine(message);
    };

    socket.ConnectionClosed += () =>
    {
        Console.WriteLine("Connection Closed");
    };

    socket.ConnectionError += (exception) =>
    {
        Console.WriteLine(exception.Message);
    };

    await socket.ConnectAsync();

    await socket.SendAsync(new
    {
        type = "subscribe",
        product_ids = new string[] { "ETH-USD", "ETH-EUR" }
    });

    await Task.Delay(10000);
}
```
