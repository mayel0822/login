namespace BookHiveLibrary.Models
{
    public class Section
    {
        public int Id { get; set; }
        public string Level { get; set; } = "";
        public string Course { get; set; } = "";
        public string Year { get; set; } = "";
        public string SectionName { get; set; } = "";
        public string AdviserName { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
