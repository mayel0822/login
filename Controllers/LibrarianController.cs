using BookHiveLibrary.Data;
using BookHiveLibrary.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

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

        public async Task<IActionResult> Registration()
        {
            ViewBag.Students = await _userManager.Users
                .Where(u => u.UserType == "Student" && u.IsActive)
                .OrderBy(u => u.LastName)
                .ToListAsync();
            ViewBag.Professors = await _userManager.Users
                .Where(u => u.UserType == "Professor" && u.IsActive)
                .OrderBy(u => u.LastName)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Registration(string firstName, string lastName, string middleName,
            string email, string userType, string studentNumber, string employeeNumber,
            string section, string rfidNumber, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Email is required.";
                return await Registration();
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
                IsActive = true,
                EmailConfirmed = true
            };

            // Password not set by librarian — student authenticates via Microsoft OAuth
            string autoPassword = $"BookHive@{Guid.NewGuid().ToString("N")[..8]}!";
            var result = await _userManager.CreateAsync(user, autoPassword);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                return View();
            }

            await _userManager.AddToRoleAsync(user, userType);
            TempData["Success"] = $"{userType} \"{firstName} {lastName}\" registered successfully.";
            return RedirectToAction("UserManagement");
        }

        // ── Excel Import ─────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select an Excel file.";
                return View("Registration");
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var results = new List<string[]>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets[0];
            int rowCount = sheet.Dimension?.Rows ?? 0;

            for (int row = 2; row <= rowCount; row++)
            {
                string userType     = sheet.Cells[row, 1].Text.Trim();
                string firstName    = sheet.Cells[row, 2].Text.Trim();
                string middleName   = sheet.Cells[row, 3].Text.Trim();
                string lastName     = sheet.Cells[row, 4].Text.Trim();
                string email        = sheet.Cells[row, 5].Text.Trim();
                string phone        = sheet.Cells[row, 6].Text.Trim();
                string studentNum   = sheet.Cells[row, 7].Text.Trim();
                string employeeNum  = sheet.Cells[row, 8].Text.Trim();
                string section      = sheet.Cells[row, 9].Text.Trim();

                string fullName = $"{firstName} {lastName}";
                string idNum = !string.IsNullOrEmpty(studentNum) ? studentNum : employeeNum;

                if (string.IsNullOrEmpty(email))
                {
                    results.Add(new[] { userType, fullName, email, idNum, section, "Skipped (no email)" });
                    continue;
                }

                var existing = await _userManager.FindByEmailAsync(email);
                if (existing != null)
                {
                    results.Add(new[] { userType, fullName, email, idNum, section, "Already exists" });
                    continue;
                }

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    MiddleName = middleName,
                    UserType = userType,
                    StudentNumber = studentNum,
                    EmployeeNumber = employeeNum,
                    Section = section,
                    PhoneNumber = phone,
                    IsFirstLogin = false,
                    IsActive = true,
                    EmailConfirmed = true
                };

                string autoPassword = $"BookHive@{Guid.NewGuid().ToString("N")[..8]}!";
                var result = await _userManager.CreateAsync(user, autoPassword);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, userType);
                    results.Add(new[] { userType, fullName, email, idNum, section, "Registered" });
                }
                else
                {
                    string error = result.Errors.FirstOrDefault()?.Description ?? "Failed";
                    results.Add(new[] { userType, fullName, email, idNum, section, $"Failed: {error}" });
                }
            }

            ViewBag.ImportedUsers = results;
            TempData["Success"] = $"Import complete: {results.Count(r => r[5] == "Registered")} registered, {results.Count(r => r[5] != "Registered")} skipped/failed.";
            return View("Registration");
        }

        // ── Sectioning ───────────────────────────────────────────────────────

        public async Task<IActionResult> Sectioning()
        {
            ViewBag.Sections = await _context.Sections.OrderBy(s => s.Level).ThenBy(s => s.SectionName).ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateSection(string level, string course, string year, string sectionName, string adviserName)
        {
            _context.Sections.Add(new Section
            {
                Level = level,
                Course = course,
                Year = year,
                SectionName = sectionName,
                AdviserName = adviserName ?? ""
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Section created.";
            return RedirectToAction("Sectioning");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAllSections(string levelGroup)
        {
            IQueryable<Section> query = _context.Sections;

            if (levelGroup == "JHS")
                query = query.Where(s => s.Level == "Grade 7" || s.Level == "Grade 8" || s.Level == "Grade 9" || s.Level == "Grade 10");
            else if (levelGroup == "SHS")
                query = query.Where(s => s.Level == "SHS");
            else if (levelGroup == "College")
                query = query.Where(s => s.Level == "College");

            _context.Sections.RemoveRange(query);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{levelGroup} sections have been reset.";
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

        public async Task<IActionResult> StudentActivity(string? date, string? logType)
        {
            logType ??= "Library";

            DateTime? parsedDate = null;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var d))
                parsedDate = d;

            if (logType == "Computer")
            {
                var csQuery = _context.ComputerSessions
                    .Include(s => s.User)
                    .Include(s => s.ComputerUnit)
                    .AsQueryable();

                if (parsedDate.HasValue)
                    csQuery = csQuery.Where(s => s.StartTime.Date == parsedDate.Value.Date);

                ViewBag.ComputerSessions = await csQuery.OrderByDescending(s => s.StartTime).Take(100).ToListAsync();
                ViewBag.Date = date;
                ViewBag.LogType = logType;
                return View(new List<RFIDLog>());
            }

            if (logType == "Book")
            {
                var bQuery = _context.BookReservations
                    .Include(r => r.User)
                    .Include(r => r.Book)
                    .AsQueryable();

                if (parsedDate.HasValue)
                    bQuery = bQuery.Where(r => r.CreatedAt.Date == parsedDate.Value.Date);

                ViewBag.BookLogs = await bQuery.OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync();
                ViewBag.Date = date;
                ViewBag.LogType = logType;
                return View(new List<RFIDLog>());
            }

            var rfidQuery = _context.RFIDLogs
                .Include(l => l.User)
                .AsQueryable();

            if (parsedDate.HasValue)
                rfidQuery = rfidQuery.Where(l => l.TapInTime.Date == parsedDate.Value.Date);

            var logs = await rfidQuery.OrderByDescending(l => l.TapInTime).Take(100).ToListAsync();

            ViewBag.Date = date;
            ViewBag.LogType = logType;

            return View(logs);
        }

        // ── Notifications ─────────────────────────────────────────────────────

        public async Task<IActionResult> GetNotifications()
        {
            var pending = await _context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .Select(r => new {
                    r.Id,
                    user = r.User != null ? r.User.FirstName + " " + r.User.LastName : "Unknown",
                    userType = r.User != null ? r.User.UserType : "",
                    book = r.Book != null ? r.Book.Title : "Unknown",
                    createdAt = r.CreatedAt.ToString("MMM d, h:mm tt")
                })
                .ToListAsync();

            return Json(new { count = pending.Count, items = pending });
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
