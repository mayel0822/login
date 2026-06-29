namespace BookHiveLibrary.Models
{
    public class ComputerSession
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public int ComputerUnitId { get; set; }
        public ComputerUnit? ComputerUnit { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; } = true;
        public int AllowedMinutes { get; set; } = 60;
        public int ExtendedMinutes { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
