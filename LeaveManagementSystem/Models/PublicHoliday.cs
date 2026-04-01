using System;

namespace LeaveManagementSystem.Models
{
    public class PublicHoliday
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string CountryCode { get; set; } = "MY";

        public string Location { get; set; } = string.Empty;

        public int Year { get; set; }
        public bool IsFixedAnnual { get; set; } = false;
    }
}
