using BookHiveLibrary.Data;
using BookHiveLibrary.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Controllers
{
    public class ComputerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ComputerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task LoadComputerStats()
        {
            ViewBag.TotalComputers = await _context.ComputerUnits.CountAsync();
            ViewBag.AvailableComputers = await _context.ComputerUnits.CountAsync(c => !c.IsArchived && c.IsAvailable);
            ViewBag.InUseComputers = await _context.ComputerUnits.CountAsync(c => !c.IsArchived && !c.IsAvailable);
            ViewBag.ArchivedComputers = await _context.ComputerUnits.CountAsync(c => c.IsArchived);
        }

        // Computer List
        public async Task<IActionResult> Index()
        {
            await LoadComputerStats();

            var computers = await _context.ComputerUnits
                .Where(c => !c.IsArchived)
                .Include(c => c.Sessions.Where(s => s.IsActive))
                    .ThenInclude(s => s.User)
                .OrderBy(c => c.ComputerNumber)
                .ToListAsync();

            return View(computers);
        }

        // Register / Add Computer
        public async Task<IActionResult> Register()
        {
            await LoadComputerStats();
            return View(new ComputerUnit());
        }

        [HttpPost]
        public async Task<IActionResult> Register(string computerNumber, bool isAvailable = true)
        {
            if (string.IsNullOrWhiteSpace(computerNumber))
            {
                await LoadComputerStats();
                ModelState.AddModelError("", "Computer number is required.");
                return View(new ComputerUnit());
            }

            bool exists = await _context.ComputerUnits.AnyAsync(c => c.ComputerNumber == computerNumber && !c.IsArchived);
            if (exists)
            {
                await LoadComputerStats();
                ModelState.AddModelError("", "A computer with that number already exists.");
                return View(new ComputerUnit());
            }

            _context.ComputerUnits.Add(new ComputerUnit { ComputerNumber = computerNumber, IsAvailable = isAvailable });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{computerNumber} registered.";
            return RedirectToAction("Index");
        }

        // Start a session (librarian assigns student to a computer)
        [HttpPost]
        public async Task<IActionResult> StartSession(int computerId, string userId)
        {
            var computer = await _context.ComputerUnits.FindAsync(computerId);
            if (computer == null || !computer.IsAvailable)
            {
                TempData["Error"] = "Computer not available.";
                return RedirectToAction("Index");
            }

            _context.ComputerSessions.Add(new ComputerSession
            {
                ComputerUnitId = computerId,
                UserId = userId,
                StartTime = DateTime.Now
            });

            computer.IsAvailable = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Session started.";
            return RedirectToAction("Index");
        }

        // End session
        [HttpPost]
        public async Task<IActionResult> EndSession(int sessionId)
        {
            var session = await _context.ComputerSessions
                .Include(s => s.ComputerUnit)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return NotFound();

            session.EndTime = DateTime.Now;
            session.IsActive = false;

            if (session.ComputerUnit != null)
                session.ComputerUnit.IsAvailable = true;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Session ended.";
            return RedirectToAction("Index");
        }

        // Archive Computer
        [HttpPost]
        public async Task<IActionResult> Archive(int id, string reason)
        {
            var computer = await _context.ComputerUnits.FindAsync(id);
            if (computer == null) return NotFound();

            computer.IsArchived = true;
            computer.IsAvailable = false;
            computer.ArchiveReason = reason;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Computer archived.";
            return RedirectToAction("Index");
        }

        // Archived computers list
        public async Task<IActionResult> ArchiveList()
        {
            await LoadComputerStats();
            var archived = await _context.ComputerUnits
                .Where(c => c.IsArchived)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(archived);
        }

        // Permanently delete archived computer
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var computer = await _context.ComputerUnits.FindAsync(id);
            if (computer != null)
            {
                _context.ComputerUnits.Remove(computer);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Computer permanently deleted.";
            }
            return RedirectToAction("ArchiveList");
        }

        // Restore from archive
        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var computer = await _context.ComputerUnits.FindAsync(id);
            if (computer == null) return NotFound();

            computer.IsArchived = false;
            computer.IsAvailable = true;
            computer.ArchiveReason = "";

            await _context.SaveChangesAsync();
            TempData["Success"] = "Computer restored.";
            return RedirectToAction("ArchiveList");
        }
    }
}
