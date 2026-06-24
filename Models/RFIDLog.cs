namespace BookHiveLibrary.Models
{
    public class RFIDLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public DateTime TapInTime { get; set; } = DateTime.Now;
        public DateTime? TapOutTime { get; set; }
        public bool IsInside { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
