using BookHiveLibrary.Data;
using BookHiveLibrary.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Controllers
{
    public class BorrowController : Controller
    {
        private const int MaxBooksPerUser = 3;
        private const int BorrowDays = 2;
        private const int PickupHours = 24;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BorrowController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task LoadBorrowFormData()
        {
            ViewBag.Students = await _userManager.Users
                .Where(u => (u.UserType == "Student" || u.UserType == "Professor") && u.IsActive)
                .OrderBy(u => u.LastName)
                .ToListAsync();
            ViewBag.AvailableBooks = await _context.Books
                .Where(b => !b.IsArchived && b.AvailableQuantity > 0)
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        // Transaction Management - Book
        public async Task<IActionResult> Index()
        {
            // Pending reservations panel
            ViewBag.PendingReservations = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            // Books due soon panel (due within 2 days or overdue)
            var dueCutoff = DateTime.Now.AddDays(2);
            ViewBag.BooksDue = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "PickedUp" && r.DueDate <= dueCutoff)
                .OrderBy(r => r.DueDate)
                .ToListAsync();

            ViewBag.BooksDueCount = ((List<BookReservation>)ViewBag.BooksDue).Count;

            // Borrowed books table
            var borrowedBooks = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "PickedUp" || r.Status == "Overdue" || r.Status == "Approved")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.PendingCount = await _context.BookReservations.CountAsync(r => r.Status == "Pending");

            await LoadBorrowFormData();
            return View(borrowedBooks);
        }

        // Librarian creates a borrow record directly (walk-up)
        [HttpPost]
        public async Task<IActionResult> CreateBorrow(string userId, int bookId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var book = await _context.Books.FindAsync(bookId);

            if (user == null || book == null)
            {
                TempData["Error"] = "Invalid user or book.";
                return RedirectToAction("Index");
            }

            int activeCount = await _context.BookReservations.CountAsync(r =>
                r.UserId == userId &&
                (r.Status == "PickedUp" || r.Status == "Overdue"));

            if (activeCount >= MaxBooksPerUser)
            {
                TempData["Error"] = $"This user already has {MaxBooksPerUser} books borrowed.";
                return RedirectToAction("Index");
            }

            if (book.AvailableQuantity <= 0)
            {
                TempData["Error"] = "No copies available for this book.";
                return RedirectToAction("Index");
            }

            book.AvailableQuantity -= 1;

            _context.BookReservations.Add(new BookReservation
            {
                UserId = userId,
                BookId = bookId,
                Status = "PickedUp",
                ActualPickupTime = DateTime.Now,
                DueDate = DateTime.Now.AddDays(BorrowDays),
                PickupDeadline = DateTime.Now.AddHours(PickupHours)
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Book \"{book.Title}\" borrowed by {user.FirstName} {user.LastName}. Due in {BorrowDays} days.";
            return RedirectToAction("Index");
        }

        // Detail view
        public async Task<IActionResult> Detail(int id)
        {
            var reservation = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();
            return View(reservation);
        }

        // Approve reservation — librarian confirms student has picked up the book
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var reservation = await _context.BookReservations
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            // Enforce 3-book limit
            int activeCount = await _context.BookReservations.CountAsync(r =>
                r.UserId == reservation.UserId &&
                (r.Status == "PickedUp" || r.Status == "Overdue") &&
                r.Id != id);

            if (activeCount >= MaxBooksPerUser)
            {
                TempData["Error"] = $"User already has {MaxBooksPerUser} books borrowed.";
                return RedirectToAction("Index");
            }

            reservation.Status = "Approved";
            reservation.PickupDeadline = DateTime.Now.AddHours(PickupHours);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Reservation approved. Student has 24 hours to pick up.";
            return RedirectToAction("Index");
        }

        // Deny reservation
        [HttpPost]
        public async Task<IActionResult> Deny(int id, string? remarks)
        {
            var reservation = await _context.BookReservations.FindAsync(id);
            if (reservation == null) return NotFound();

            reservation.Status = "Denied";
            reservation.LibrarianRemarks = remarks ?? "";

            await _context.SaveChangesAsync();
            TempData["Success"] = "Reservation denied.";
            return RedirectToAction("Index");
        }

        // Confirm pickup — student physically collected the book
        [HttpPost]
        public async Task<IActionResult> ConfirmPickup(int id)
        {
            var reservation = await _context.BookReservations
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            reservation.Status = "PickedUp";
            reservation.ActualPickupTime = DateTime.Now;
            reservation.DueDate = DateTime.Now.AddDays(BorrowDays);
            reservation.ReminderSent = false;

            // Decrement available quantity
            if (reservation.Book != null)
                reservation.Book.AvailableQuantity = Math.Max(0, reservation.Book.AvailableQuantity - 1);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Book picked up. Due date: {reservation.DueDate:MMM dd, yyyy}.";
            return RedirectToAction("Index", new { tab = "Active" });
        }

        // Confirm return
        [HttpPost]
        public async Task<IActionResult> ConfirmReturn(int id)
        {
            var reservation = await _context.BookReservations
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            reservation.Status = "Returned";
            reservation.ActualReturnDate = DateTime.Now;

            if (reservation.Book != null)
                reservation.Book.AvailableQuantity = Math.Min(reservation.Book.TotalQuantity, reservation.Book.AvailableQuantity + 1);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Book returned successfully.";
            return RedirectToAction("Index", new { tab = "Active" });
        }
    }
}
