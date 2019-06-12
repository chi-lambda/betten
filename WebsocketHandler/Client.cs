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
        public Client(HttpContext httpContext, BettenContext dbContext, WebSocket webSocket, Handler handler, bool isLocal)
        {
            this.webSocket = webSocket;
            this.httpContext = httpContext;
            this.dbContext = dbContext;
            this.handler = handler;
            this.isLocal = isLocal;
        }

        public async Task Run()
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                var message = Encoding.UTF8.GetString(buffer).Trim('\0');
                Console.WriteLine(">>{0}<<", message);
                var commandMessage = JsonConvert.DeserializeObject<CommandMessage>(message);
                if (commandMessage != null)
                    switch (commandMessage.Command)
                    {
                        case "GetSKs":
                            await SendSKs();
                            break;
                        case "GetInitialData":
                            await SendHelpers();
                            await SendPatients();
                            break;
                        case "UpsertHelpers":
                            await UpsertHelpers(commandMessage.Parameters);
                            break;
                        case "UpsertPatients":
                            await UpsertPatients(commandMessage.Parameters);
                            break;
                        case "CreateBeds":
                            await CreateBeds(commandMessage.Parameters);
                            break;
                        default:
                            Console.WriteLine("Unknown message '{0}'", commandMessage.Command);
                            break;
                    }

                buffer = new byte[1024 * 4];
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
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                Disconnect();
            }
        }

        public async Task SendHelpers()
        {
            if (!isLocal) { return; }
            var helpers = new Dictionary<string, Helper[]>() { { "helpers", dbContext.Helpers.ToArray() } };
            var helpersString = JsonConvert.SerializeObject(helpers);
            var sendBytes = Encoding.UTF8.GetBytes(helpersString);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                Disconnect();
            }
        }

        public async Task SendBeds()
        {
            await SendSKs();
            var beds = new Dictionary<string, Bed[]>() { { "beds", dbContext.Beds.ToArray() } };
            var bedsString = JsonConvert.SerializeObject(beds);
            var sendBytes = Encoding.UTF8.GetBytes(bedsString);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                Disconnect();
            }
        }

        public async Task SendPatients()
        {
            await SendBeds();
            ///            if (!isLocal) { return; }
            var patients = new Dictionary<string, Patient[]>() { { "patients", dbContext.Patients.ToArray() } };
            var patientsString = JsonConvert.SerializeObject(patients);
            var sendBytes = Encoding.UTF8.GetBytes(patientsString);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                Disconnect();
            }
        }

        private async Task UpsertHelpers(object[] parameters)
        {
            if (!isLocal) { return; }
            var helpers = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Helper>())
                .Where(h => h.Id == 0)
                .ToArray();
            await dbContext.Helpers.AddRangeAsync(helpers);
            await dbContext.SaveChangesAsync();
            await handler.BroadcastHelpers();
        }

        private async Task UpsertPatients(object[] parameters)
        {
            if (!isLocal) { return; }
            var newPatients = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Patient>())
                .Where(p => p.Id == 0)
                .ToArray();
            var patientNumber = await dbContext.Patients.Select(p => p.PatientNumber).MaxAsync() ?? 0;
            foreach (var patient in newPatients)
            {
                patientNumber++;
                patient.PatientNumber = patientNumber;
            }
            await dbContext.Patients.AddRangeAsync(newPatients);
            var existingPatients = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Patient>())
                .Where(p => p.Id != 0)
                .ToDictionary(k => k.Id, v => v);
            var existingIds = existingPatients.Keys;
            var dbPatients = dbContext.Patients.Where(p => existingIds.Contains(p.Id));
            foreach (var patient in dbPatients)
            {
                patient.Update(existingPatients[patient.Id]);
            }
            await dbContext.SaveChangesAsync();
            await handler.BroadcastPatients();
        }

        private async Task CreateBeds(object[] parameters)
        {
            if (!isLocal) { return; }
            var bedPrefixes = dbContext.SK.ToDictionary(k => k.Id, v => v.BedPrefix);
            var beds = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<CreateBedsParameter>())
                .SelectMany(cbp => Enumerable.Range(1, cbp.Count).Select(i => new Bed() { SKId = cbp.Id, EventId = 1, Name = bedPrefixes[cbp.Id] + " " + i }))
                .ToArray();
            await dbContext.Beds.AddRangeAsync(beds);
            await dbContext.SaveChangesAsync();
            await handler.BroadcastBeds();
        }

        private async void Disconnect()
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", new CancellationToken());
            }
            catch { }
            handler.RemoveClient(this);
        }

        private WebSocket webSocket;
        private HttpContext httpContext;
        private BettenContext dbContext;
        private Handler handler;
        private bool isLocal;
    }
}