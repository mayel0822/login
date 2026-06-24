using BookHiveLibrary.Data;
using BookHiveLibrary.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Controllers
{
    public class LibrarianController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LibrarianController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.ActiveUsers = await _context.RFIDLogs.CountAsync(l => l.IsInside);
            ViewBag.BookBorrowed = await _context.BookReservations.CountAsync(r => r.Status == "PickedUp");
            ViewBag.Reservations = await _context.BookReservations.CountAsync(r => r.Status == "Pending");
            ViewBag.ComputerInUse = await _context.ComputerSessions.CountAsync(s => s.EndTime == null);
            ViewBag.ComputerAvailable = await _context.ComputerUnits.CountAsync(c => !c.IsArchived && c.IsAvailable);

            ViewBag.CurrentBorrowers = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "PickedUp" || r.Status == "Overdue")
                .OrderBy(r => r.DueDate)
                .Take(10)
                .ToListAsync();

            ViewBag.ReservationList = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            ViewBag.BookDues = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "PickedUp" && r.DueDate <= DateTime.Now.AddDays(1))
                .OrderBy(r => r.DueDate)
                .Take(10)
                .ToListAsync();

            return View();
        }

        // ── User Management ──────────────────────────────────────────────────

        public async Task<IActionResult> UserManagement(string? search, string? userType)
        {
            var query = _userManager.Users
                .Where(u => u.UserType == "Student" || u.UserType == "Professor");

            if (!string.IsNullOrWhiteSpace(userType))
                query = query.Where(u => u.UserType == userType);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search) ||
                    u.StudentNumber.Contains(search) ||
                    u.EmployeeNumber.Contains(search) ||
                    u.Email!.Contains(search));

            var users = await query.OrderBy(u => u.UserType).ThenBy(u => u.LastName).ToListAsync();

            ViewBag.Search = search;
            ViewBag.UserType = userType;

            return View(users);
        }

        // ── Registration ─────────────────────────────────────────────────────

        public IActionResult Registration() => View();

        [HttpPost]
        public async Task<IActionResult> Registration(string firstName, string lastName, string middleName,
            string email, string userType, string studentNumber, string employeeNumber,
            string section, string rfidNumber, string phoneNumber, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Email and password are required.";
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                MiddleName = middleName,
                UserType = userType,
                StudentNumber = studentNumber,
                EmployeeNumber = employeeNumber,
                Section = section,
                RFIDNumber = rfidNumber,
                PhoneNumber = phoneNumber,
                IsFirstLogin = false,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                return View();
            }

            await _userManager.AddToRoleAsync(user, userType);
            TempData["Success"] = $"{userType} registered successfully.";
            return RedirectToAction("UserManagement");
        }

        // ── Sectioning ───────────────────────────────────────────────────────

        public async Task<IActionResult> Sectioning()
        {
            ViewBag.Sections = await _context.Sections.OrderBy(s => s.Level).ThenBy(s => s.SectionName).ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateSection(string level, string course, string year, string sectionName)
        {
            _context.Sections.Add(new Section
            {
                Level = level,
                Course = course,
                Year = year,
                SectionName = sectionName
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Section created.";
            return RedirectToAction("Sectioning");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSection(int id)
        {
            var section = await _context.Sections.FindAsync(id);
            if (section != null)
            {
                _context.Sections.Remove(section);
                await _context.SaveChangesAsync();
            }
            TempData["Success"] = "Section deleted.";
            return RedirectToAction("Sectioning");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSection(string userId, string section)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.Section = section;
                await _userManager.UpdateAsync(user);
            }
            TempData["Success"] = "Section updated.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public async Task<IActionResult> ResetSection(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.Section = "";
                await _userManager.UpdateAsync(user);
            }
            TempData["Success"] = "Section reset.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public async Task<IActionResult> ResetAllSections(string userType)
        {
            var users = _userManager.Users.Where(u => u.UserType == userType).ToList();
            foreach (var u in users)
                u.Section = "";
            foreach (var u in users)
                await _userManager.UpdateAsync(u);
            TempData["Success"] = $"All {userType} sections have been reset.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAccountStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction("UserManagement");
        }

        // ── Student Activity (RFID live tracker) ─────────────────────────────

        public async Task<IActionResult> StudentActivity(string? date, string? activity)
        {
            var query = _context.RFIDLogs
                .Include(l => l.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
                query = query.Where(l => l.TapInTime.Date == parsedDate.Date);

            if (activity == "Inside")
                query = query.Where(l => l.IsInside);
            else if (activity == "Left")
                query = query.Where(l => !l.IsInside);

            var logs = await query.OrderByDescending(l => l.TapInTime).Take(100).ToListAsync();

            ViewBag.Date = date;
            ViewBag.Activity = activity;

            return View(logs);
        }

        // RFID tap-in (called by IoT device via HTTP)
        [HttpPost]
        public async Task<IActionResult> RfidTapIn(string rfidNumber)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RFIDNumber == rfidNumber);
            if (user == null) return NotFound("RFID not registered.");

            var open = await _context.RFIDLogs
                .Where(l => l.UserId == user.Id && l.IsInside)
                .ToListAsync();
            foreach (var log in open)
            {
                log.IsInside = false;
                log.TapOutTime = DateTime.Now;
            }

            _context.RFIDLogs.Add(new RFIDLog { UserId = user.Id, TapInTime = DateTime.Now });
            await _context.SaveChangesAsync();

            return Ok(new { name = $"{user.FirstName} {user.LastName}", studentNumber = user.StudentNumber });
        }

        // RFID tap-out (called by IoT device via HTTP)
        [HttpPost]
        public async Task<IActionResult> RfidTapOut(string rfidNumber)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RFIDNumber == rfidNumber);
            if (user == null) return NotFound("RFID not registered.");

            var log = await _context.RFIDLogs
                .Where(l => l.UserId == user.Id && l.IsInside)
                .OrderByDescending(l => l.TapInTime)
                .FirstOrDefaultAsync();

            if (log != null)
            {
                log.IsInside = false;
                log.TapOutTime = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}
