namespace LeaveManagementSystem.ViewModels
{
    public class CalendarEventDto
    {
        public int? Id { get; set; }
        public string? Title { get; set; }
        public string? Start { get; set; }
        public string? End { get; set; }
        public string? Color { get; set; }
        public bool IsSpecialRequest { get; set; }
        public bool IsPublicHoliday { get; set; }
        public string? Url { get; set; }
        public string? HolidayType { get; set; }
        public string? HolidayLocation { get; set; }
    }
}
