using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using betten.Model;
using betten.WebsocketHandler.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace betten.WebsocketHandler
{
    public class Client
    {
        public Client(HttpContext httpContext, BettenContext dbContext, WebSocket webSocket, Handler handler)
        {
            this.webSocket = webSocket;
            this.httpContext = httpContext;
            this.dbContext = dbContext;
            this.handler = handler;
        }

        public async Task Run()
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                var message = Encoding.UTF8.GetString(buffer);
                var commandMessage = JsonConvert.DeserializeObject<CommandMessage>(message);
                if (commandMessage != null)
                    switch (commandMessage.Command)
                    {
                        case "GetSKs":
                            await SendSKs();
                            break;
                        case "GetInitialData":
                            await SendSKs();
                            await SendHelpers();
                            break;
                        case "UpsertHelpers":
                            await UpsertHelpers(commandMessage.Parameters);
                            break;
                        default:
                            Console.WriteLine("Unknown message '{0}'", commandMessage.Command);
                            break;
                    }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            handler.RemoveClient(this);
        }

        private async Task SendSKs()
        {
            var sks = new Dictionary<string, SK[]>() { { "sks", dbContext.SK.Include(sk => sk.Beds).ToArray() } };
            var skString = JsonConvert.SerializeObject(sks);
            var sendBytes = Encoding.UTF8.GetBytes(skString);
            await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendHelpers()
        {
            var helpers = new Dictionary<string, Helper[]>() { { "helpers", dbContext.Helpers.ToArray() } };
            var helpersString = JsonConvert.SerializeObject(helpers);
            var sendBytes = Encoding.UTF8.GetBytes(helpersString);
            await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task UpsertHelpers(object[] parameters)
        {
            var helpers = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Helper>())
                .Where(h => h.Id == 0)
                .ToArray();
            await dbContext.Helpers.AddRangeAsync(helpers);
            await dbContext.SaveChangesAsync();
            await handler.BroadcastHelpers();
        }

        private async Task CreateBeds(object[] parameters)
        {
            var helpers = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Helper>())
                .Where(h => h.Id == 0)
                .ToArray();
            await dbContext.Helpers.AddRangeAsync(helpers);
            await dbContext.SaveChangesAsync();
            await handler.BroadcastHelpers();
        }

        private WebSocket webSocket;
        private HttpContext httpContext;
        private BettenContext dbContext;
        private Handler handler;
    }
}