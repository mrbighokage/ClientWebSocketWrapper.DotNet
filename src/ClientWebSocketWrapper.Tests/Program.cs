using System;
using System.Threading.Tasks;
using ClientWebSocketWrapper;

namespace Test
{
    class Program
    {
        public delegate Task Test();

        static void Main(string[] args)
        {
            Console.WriteLine("Test Gdax");
            Task.Delay(200).GetAwaiter().GetResult();

            RunAsync(TestGdax).GetAwaiter().GetResult();

            Console.WriteLine("Test Poloniex");
            Task.Delay(200).GetAwaiter().GetResult();

            RunAsync(TestPoloniex).GetAwaiter().GetResult();
        }

        private static async Task TestGdax()
        {
            var uri = new Uri("wss://ws-feed.gdax.com");

            using (var socket = new WebSocketWrapper(uri))
            {
                MessageArrived(socket);

                await socket.ConnectAsync();

                await socket.SendAsync(new
                {
                    type = "subscribe",
                    product_ids = new string[] { "ETH-USD", "ETH-EUR" }
                });

                await Task.Delay(10000);
            }
        }

        private static async Task TestPoloniex() {
            var uri = new Uri("wss://api2.poloniex.com");

            using (var socket = new WebSocketWrapper(uri))
            {
                MessageArrived(socket);

                await socket.ConnectAsync();

                // subscribe to chanell BTC_NXT
                await socket.SendAsync(new
                {
                    command = "subscribe",
                    channel = 69
                });

                await Task.Delay(10000);

                await socket.SendAsync(new
                {
                    command = "unsubscribe",
                    channel = 69
                });

                await Task.Delay(500);
            }
        }

        private static void MessageArrived(WebSocketWrapper socket) {
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
                Console.WriteLine("Connection Error");
            };
        }

        private static async Task RunAsync(Test test) {
            try
            {
                do
                {
                    await test();

                    Console.WriteLine("\nType \"exit\" to run next test or close application.");
                }
                while (!Console.ReadLine().ToLower().Equals("exit"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }
    }
}
