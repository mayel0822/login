namespace BookHiveLibrary.Models
{
    public class BookReservation
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }
        public int BookId { get; set; }
        public Book? Book { get; set; }
        public DateTime ReservationDate { get; set; } = DateTime.Now;
        public DateTime PickupDeadline { get; set; }
        public DateTime? ActualPickupTime { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ActualReturnDate { get; set; }

        // Pending | Approved | Denied | PickedUp | Returned | Overdue
        public string Status { get; set; } = "Pending";
        public bool ReminderSent { get; set; } = false;
        public string LibrarianRemarks { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
