using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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
        public string Title { get; set; }
        public DateTime Datum { get; set; }
        public string StationHead { get; set; }
        public string Physician { get; set; }
        public string Documenter { get; set; }

        public List<Helper> Helpers { get; set; }
        public List<Bed> Beds { get; set; }
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
        public Event Event { get; set; }

        public int SKId { get; set; }
        public SK SK { get; set; }

        public List<Patient> Patients { get; set; }
    }
    public class Patient : Entity
    {
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string Gender { get; set; }
        public string Birth { get; set; }
        public string Discipline { get; set; }
        public DateTime Admission { get; set; }
        public DateTime Discharge { get; set; }
        public string DischargedBy { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; }

        public int BedId { get; set; }
        public Bed Bed { get; set; }
    }
}