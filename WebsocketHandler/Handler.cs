using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using betten.Model;
using System.Linq;

namespace betten.WebsocketHandler
{
    public class Handler
    {
        private ICollection<Client> Clients = new List<Client>();
        private BettenContext dbContext = new BettenContext();

        public int EventId { get; private set; }

        public async Task AddClient(HttpContext httpContext, WebSocket webSocket, bool isLocal)
        {
            var client = new Client(httpContext, dbContext, webSocket, this, isLocal);
            lock (Clients)
            {
                Clients.Add(client);
            }
            await client.Run();
        }

        public void RemoveClient(Client client)
        {
            lock (Clients)
            {
                Clients.Remove(client);
            }
        }

        public async Task BroadcastHelpers()
        {
            foreach (var client in Clients.ToArray())
            {
                await client.SendHelpers();
            }
        }
        public async Task BroadcastPatients()
        {
            foreach (var client in Clients.ToArray())
            {
                await client.SendPatients();
            }
        }
        public async Task BroadcastBeds()
        {
            foreach (var client in Clients.ToArray())
            {
                await client.SendBeds();
            }
        }
        public async Task BroadcastEvents()
        {
            foreach (var client in Clients.ToArray())
            {
                await client.SendEvents();
                await client.SendPatients();
            }
        }

        public async Task SetEventId(int eventId)
        {
            this.EventId = eventId;
            foreach (var client in Clients.ToArray())
            {
                await client.SendEventId();
            }
        }
    }
}