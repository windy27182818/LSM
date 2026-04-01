using System.Net.Http;
using System.Text.Json;
using LeaveManagementSystem.Models;

namespace LeaveManagementSystem.Services
{
    public class HolidaySyncService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;

        public HolidaySyncService(HttpClient httpClient, IConfiguration config, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _config = config;
            _context = context;
        }

        public async Task SyncPublicHolidaysAsync(int year)
        {
            var apiKey = _config["AbstractApi:HolidayKey"];
            var url = $"https://holidays.abstractapi.com/v1/?api_key={apiKey}&country=MY&year={year}";

            var response = await _httpClient.GetStringAsync(url);

            var holidays = JsonSerializer.Deserialize<List<PublicHoliday>>(response);

            if (holidays == null) return;

            foreach (var h in holidays)
            {
                bool exists = _context.PublicHolidays
                    .Any(x => x.Date == h.Date && x.Name == h.Name);

                if (!exists)
                {
                    _context.PublicHolidays.Add(h);
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
