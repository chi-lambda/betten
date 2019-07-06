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
                var commandMessage = JsonConvert.DeserializeObject<CommandMessage>(message);
                if (commandMessage != null)
                    switch (commandMessage.Command)
                    {
                        case "GetSKs":
                            await SendSKs();
                            break;
                        case "GetInitialData":
                            await SendEvents();
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
                        case "ToggleTransported":
                            await ToggleTransported(commandMessage.Parameters);
                            break;
                        case "DischargePatient":
                            await DischargePatient(commandMessage.Parameters);
                            break;
                        case "UpsertEvents":
                            await UpsertEvents(commandMessage.Parameters);
                            break;
                        case "SetEvent":
                            await SetEvent(commandMessage.Parameters);
                            await handler.BroadcastPatients();
                            await handler.BroadcastHelpers();
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
            using (var dbContext = new BettenContext())
            {
                var sksWithAllBeds = dbContext.SK
                    .Include(sk => sk.Beds)
                    .ToArray();
                foreach (var skWithBed in sksWithAllBeds)
                {
                    skWithBed.Beds = skWithBed.Beds.Where(b => b.EventId == handler.EventId).ToList();
                }
                var sks = new Dictionary<string, SK[]>() { {
                "sks", sksWithAllBeds
            } };
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
        }

        public async Task SendHelpers()
        {
            if (!isLocal) { return; }
            using (var dbContext = new BettenContext())
            {
                var helpers = new Dictionary<string, Helper[]>() { {
                "helpers", dbContext.Helpers.Where(h => h.EventId == handler.EventId).ToArray()
            } };
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
        }

        public async Task SendBeds()
        {
            await SendSKs();
            using (var dbContext = new BettenContext())
            {
                var beds = new Dictionary<string, Bed[]>() { {
                 "beds", dbContext.Beds.Include(b => b.Patients).Where(b => b.EventId == handler.EventId).ToArray()
            } };
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
        }

        public async Task SendPatients()
        {
            await SendBeds();
            using (var dbContext = new BettenContext())
            {
                if (!isLocal) { return; }
                var patients = new Dictionary<string, Patient[]>() { {
                 "patients", dbContext.Patients.Where(p => p.EventId == handler.EventId).ToArray()
            } };
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
        }

        public async Task SendEvents()
        {
            await SendEventId();
            using (var dbContext = new BettenContext())
            {
                var events = isLocal
                    ? new Dictionary<string, Event[]>() { { "events", dbContext.Events.ToArray() } }
                    : new Dictionary<string, Event[]>() { { "events", dbContext.Events.Where(e => e.Id == handler.EventId).ToArray() } };
                var eventsString = JsonConvert.SerializeObject(events);
                var sendBytes = Encoding.UTF8.GetBytes(eventsString);
                try
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    Disconnect();
                }
            }
            await SendHelpers();
        }

        private async Task UpsertHelpers(object[] parameters)
        {
            if (!isLocal) { return; }
            var helpers = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Helper>())
                .Where(h => h.Id == 0)
                .ToArray();
            using (var dbContext = new BettenContext())
            {
                await dbContext.Helpers.AddRangeAsync(helpers);
                await dbContext.SaveChangesAsync();
            }
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
            using (var dbContext = new BettenContext())
            {
                var patientNumber = await dbContext.Patients.AnyAsync(p => newPatients[0].EventId == p.EventId)
                    ? await dbContext.Patients
                        .Where(p => newPatients[0].EventId == p.EventId)
                        .Select(p => p.PatientNumber)
                        .MaxAsync()
                    : 0;
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
            }
            await handler.BroadcastPatients();
        }

        private async Task UpsertEvents(object[] parameters)
        {
            if (!isLocal) { return; }
            var newEvents = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Event>())
                .Where(p => p.Id == 0)
                .ToArray();
            var existingEvents = parameters
                .Cast<JObject>()
                .Select(o => o.ToObject<Event>())
                .Where(p => p.Id != 0)
                .ToDictionary(k => k.Id, v => v);
            var existingIds = existingEvents.Keys;
            using (var dbContext = new BettenContext())
            {
                await dbContext.Events.AddRangeAsync(newEvents);
                var dbEvents = dbContext.Events.Where(p => existingIds.Contains(p.Id));
                foreach (var evt in dbEvents)
                {
                    evt.Update(existingEvents[evt.Id]);
                }
                await dbContext.SaveChangesAsync();
            }
            if (newEvents.Any())
            {
                await handler.SetEventId(newEvents.First().Id);
            }
            await handler.BroadcastEvents();
        }

        private async Task CreateBeds(object[] parameters)
        {
            if (!isLocal) { return; }
            using (var dbContext = new BettenContext())
            {
                var bedPrefixes = dbContext.SK.ToDictionary(k => k.Id, v => v.BedPrefix);
                var beds = parameters
                    .Cast<JObject>()
                    .Select(o => o.ToObject<CreateBedsParameter>())
                    .SelectMany(cbp => Enumerable.Range(1, cbp.Count).Select(i => new Bed() { SKId = cbp.Id, EventId = handler.EventId, Name = bedPrefixes[cbp.Id] + " " + i }))
                    .ToArray();
                await dbContext.Beds.AddRangeAsync(beds);
                await dbContext.SaveChangesAsync();
            }
            await handler.BroadcastBeds();
        }

        private async Task ToggleTransported(object[] parameters)
        {
            if (!isLocal) { return; }
            if (parameters.Length < 1) { return; }
            if (!(parameters[0] is long)) { return; }
            var patientId = (long)parameters[0];
            using (var dbContext = new BettenContext())
            {
                var patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == patientId);
                if (patient == null) { return; }
                patient.Transported = !patient.Transported;
                await dbContext.SaveChangesAsync();
            }
            await handler.BroadcastPatients();
        }

        private async Task DischargePatient(object[] parameters)
        {
            if (!isLocal) { return; }
            var param = parameters
               .Cast<JObject>()
               .Select(o => o.ToObject<DischargePatientParameters>())
               .First();
            using (var dbContext = new BettenContext())
            {
                var patient = await dbContext.Patients.FirstOrDefaultAsync(p => p.Id == param.Id);
                if (patient == null) { return; }
                patient.DischargedBy = param.DischargedBy;
                patient.Discharge = DateTime.Now.ToString("HH:mm");
                await dbContext.SaveChangesAsync();
            }
            await handler.BroadcastPatients();
        }

        private async Task SetEvent(object[] parameters)
        {
            var eventId = (int)parameters.Cast<long>().First();
            await handler.SetEventId(eventId);
        }

        public async Task SendEventId()
        {
            var eventIdString = JsonConvert.SerializeObject(new { eventId = handler.EventId });
            var sendBytes = Encoding.UTF8.GetBytes(eventIdString);
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(sendBytes, 0, sendBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                Disconnect();
            }
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
        private Handler handler;
        private bool isLocal;
    }
}