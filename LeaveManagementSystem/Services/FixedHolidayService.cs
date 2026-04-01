using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LeaveManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Services
{
    public class FixedHolidayService
    {
        private readonly ApplicationDbContext _context;

        public FixedHolidayService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task EnsureYearlyFixedHolidaysAsync(int year)
        {
            var fixedHolidays = new[]
            {
                new { Name = "New Year's Day", Month = 1,  Day = 1  },
                new { Name = "Labour Day",     Month = 5,  Day = 1  },
                new { Name = "National Day",   Month = 8,  Day = 31 },
                new { Name = "Malaysia Day",   Month = 9,  Day = 16 },
                new { Name = "Christmas Day",  Month = 12, Day = 25 }
            };

            foreach (var h in fixedHolidays)
            {
                var date = new DateTime(year, h.Month, h.Day);

                bool exists = await _context.PublicHolidays.AnyAsync(ph =>
                    ph.Date == date &&
                    ph.Name == h.Name &&
                    ph.IsFixedAnnual);

                if (!exists)
                {
                    _context.PublicHolidays.Add(new PublicHoliday
                    {
                        Name = h.Name,
                        Date = date,
                        Year = year,
                        Type = "Public",
                        CountryCode = "MY",
                        Location = "National",
                        IsFixedAnnual = true
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
