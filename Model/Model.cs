using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace betten.Model
{
    public class BettenContext : DbContext
    {
        public DbSet<Event> Events { get; set; }
        public DbSet<Helper> Helpers { get; set; }
        public DbSet<Bed> Beds { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<SK> SK { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=betten.sqlite");
        }
    }

    public abstract class Entity
    {
        [JsonProperty("id")]
        public int Id { get; set; }
    }

    public class Event : Entity
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("date")]
        public DateTime Date { get; set; }
        [JsonProperty("stationHead")]
        public string StationHead { get; set; }
        [JsonProperty("physician")]
        public string Physician { get; set; }
        [JsonProperty("documenter")]
        public string Documenter { get; set; }

        [JsonIgnore]
        public List<Helper> Helpers { get; set; }
        [JsonIgnore]
        public List<Bed> Beds { get; set; }
        [JsonIgnore]
        public List<Patient> Patients { get; set; }
    }
    public class Helper : Entity
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        public int EventId { get; set; }
        [JsonIgnore]
        public Event Event { get; set; }
    }

    public class SK : Entity
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("colorClass")]
        public string ColorClass { get; set; }

        [JsonProperty("paleColorClass")]
        public string PaleColorClass { get; set; }

        [JsonProperty("bedPrefix")]
        public string BedPrefix { get; set; }

        [JsonIgnore]
        public List<Bed> Beds { get; set; }

        [JsonProperty("count")]
        [NotMapped]
        public int Count => Beds.Count;
    }
    public class Bed : Entity
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        public int EventId { get; set; }
        [JsonIgnore]
        public Event Event { get; set; }

        public int SKId { get; set; }
        [JsonIgnore]
        public SK SK { get; set; }

        [JsonIgnore]
        public List<Patient> Patients { get; set; }
    }
    public class Patient : Entity
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("firstName")]
        public string FirstName { get; set; }
        [JsonProperty("gender")]
        public string Gender { get; set; }
        [JsonProperty("birth")]
        public string Birth { get; set; }
        [JsonProperty("discipline")]
        public string Discipline { get; set; }
        [JsonProperty("admission")]
        public string Admission { get; set; }
        [JsonProperty("discharge")]
        public string Discharge { get; set; }
        [JsonProperty("dischargedBy")]
        public string DischargedBy { get; set; }
        [JsonProperty("patientNumber")]
        public int? PatientNumber { get; set; }
        [JsonProperty("transported")]
        public bool Transported { get; set; }
        [JsonProperty("comment")]
        public string Comment { get; set; }

        public int EventId { get; set; }
        [JsonIgnore]
        public Event Event { get; set; }

        public int BedId { get; set; }
        [JsonIgnore]
        public Bed Bed { get; set; }

        public void Update(Patient other)
        {
            Name = other.Name;
            FirstName = other.FirstName;
            Gender = other.Gender;
            Birth = other.Birth;
            Discipline = other.Discipline;
            Admission = other.Admission;
            Discharge = other.Discharge;
            DischargedBy = other.DischargedBy;
            BedId = other.BedId;
            Transported = other.Transported;
            Comment = other.Comment;
        }
    }
}