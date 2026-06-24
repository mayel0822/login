namespace BookHiveLibrary.Models
{
    public class ComputerUnit
    {
        public int Id { get; set; }
        public string ComputerNumber { get; set; } = "";
        public bool IsAvailable { get; set; } = true;
        public bool IsArchived { get; set; } = false;
        public string ArchiveReason { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<ComputerSession> Sessions { get; set; } = new List<ComputerSession>();
    }
}
