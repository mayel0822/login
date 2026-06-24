using BookHiveLibrary.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<OtpVerification> OtpVerifications { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<BookReservation> BookReservations { get; set; }
        public DbSet<ComputerUnit> ComputerUnits { get; set; }
        public DbSet<ComputerSession> ComputerSessions { get; set; }
        public DbSet<RFIDLog> RFIDLogs { get; set; }
        public DbSet<Section> Sections { get; set; }
    }
}
