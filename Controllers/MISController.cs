using BookHiveLibrary.Models;
using BookHiveLibrary.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Controllers
{
    [Authorize(Roles = "MIS")]
    public class MISController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public MISController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // ── Dashboard ────────────────────────────────────────────────────────

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalLibrarians  = await _userManager.Users.CountAsync(u => u.UserType == "Librarian");
            ViewBag.TotalStudents    = await _userManager.Users.CountAsync(u => u.UserType == "Student");
            ViewBag.TotalProfessors  = await _userManager.Users.CountAsync(u => u.UserType == "Professor");
            ViewBag.ActiveLibrarians = await _userManager.Users.CountAsync(u => u.UserType == "Librarian" && u.IsActive);
            ViewBag.ActiveStudents   = await _userManager.Users.CountAsync(u => u.UserType == "Student"   && u.IsActive);
            ViewBag.ActiveProfessors = await _userManager.Users.CountAsync(u => u.UserType == "Professor" && u.IsActive);

            ViewBag.RecentUsers = await _userManager.Users
                .Where(u => u.UserType == "Librarian" || u.UserType == "Student" || u.UserType == "Professor")
                .OrderByDescending(u => u.CreatedAt)
                .Take(8)
                .ToListAsync();

            return View();
        }

        // ── Registration (3-tab page) ─────────────────────────────────────────

        [HttpGet]
        public IActionResult Registration() => View();

        [HttpPost]
        public async Task<IActionResult> RegisterUser(
            string role, string firstName, string lastName, string middleName,
            string email, string outlookEmail, string password, string confirmPassword,
            string studentNumber, string employeeNumber, string section,
            string rfidNumber, string phoneNumber, string adviserEmail)
        {
            if (password != confirmPassword)
            {
                TempData["RegError"] = "Passwords do not match.";
                return RedirectToAction("Registration");
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                TempData["RegError"] = "An account with that email already exists.";
                return RedirectToAction("Registration");
            }

            var username = email.Split('@')[0];
            var user = new ApplicationUser
            {
                UserName       = username,
                Email          = email,
                OutlookEmail   = outlookEmail ?? "",
                FirstName      = firstName,
                MiddleName     = middleName ?? "",
                LastName       = lastName,
                UserType       = role,
                StudentNumber  = studentNumber ?? "",
                EmployeeNumber = employeeNumber ?? "",
                Section        = section ?? "",
                RFIDNumber     = rfidNumber ?? "",
                PhoneNumber    = phoneNumber,
                AdviserEmail   = adviserEmail ?? "",
                EmailConfirmed = true,
                IsActive       = true,
                IsFirstLogin   = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                TempData["RegError"] = string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction("Registration");
            }

            await _userManager.AddToRoleAsync(user, role);
            TempData["Success"] = $"{role} account for {firstName} {lastName} created successfully.";
            return RedirectToAction("Registration");
        }

        // ── User Information (unified list with role filter) ─────────────────

        public async Task<IActionResult> UserInformation(string? roleFilter, string? search)
        {
            var query = _userManager.Users
                .Where(u => u.IsActive &&
                    (u.UserType == "Librarian" || u.UserType == "Student" || u.UserType == "Professor"));

            if (!string.IsNullOrWhiteSpace(roleFilter) && roleFilter != "All")
                query = query.Where(u => u.UserType == roleFilter);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search)  ||
                    u.Email!.Contains(search)    ||
                    u.StudentNumber.Contains(search) ||
                    u.EmployeeNumber.Contains(search));

            var users = await query.OrderBy(u => u.LastName).ToListAsync();

            ViewBag.RoleFilter = string.IsNullOrWhiteSpace(roleFilter) ? "All" : roleFilter;
            ViewBag.Search = search;
            return View(users);
        }

        // ── Archived Users ────────────────────────────────────────────────────

        public async Task<IActionResult> ArchivedUsers()
        {
            var users = await _userManager.Users
                .Where(u => !u.IsActive &&
                    (u.UserType == "Librarian" || u.UserType == "Student" || u.UserType == "Professor"))
                .OrderBy(u => u.LastName)
                .ToListAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ArchiveUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsActive = false;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"{user.FirstName} {user.LastName} has been archived.";
            }
            return RedirectToAction("UserInformation");
        }

        [HttpPost]
        public async Task<IActionResult> RestoreUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsActive = true;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"{user.FirstName} {user.LastName} has been restored.";
            }
            return RedirectToAction("ArchivedUsers");
        }

        // ── List by role (kept for backward compat) ───────────────────────────

        public Task<IActionResult> Librarians(string? search) => UserList("Librarian", search);
        public Task<IActionResult> Students(string? search)   => UserList("Student",   search);
        public Task<IActionResult> Professors(string? search) => UserList("Professor",  search);

        private async Task<IActionResult> UserList(string role, string? search)
        {
            var query = _userManager.Users.Where(u => u.UserType == role);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search)  ||
                    u.Email!.Contains(search)    ||
                    u.StudentNumber.Contains(search) ||
                    u.EmployeeNumber.Contains(search));

            var users = await query.OrderBy(u => u.LastName).ToListAsync();

            ViewBag.Role   = role;
            ViewBag.Search = search;
            return View("UserList", users);
        }

        // ── Create ───────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Create(string role = "Student")
        {
            return View(new MISCreateUserViewModel { Role = role });
        }

        [HttpPost]
        public async Task<IActionResult> Create(MISCreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = string.IsNullOrWhiteSpace(model.Username)
                ? model.Email.Split('@')[0]
                : model.Username;

            var user = new ApplicationUser
            {
                UserName        = username,
                Email           = model.Email,
                OutlookEmail    = model.OutlookEmail,
                FirstName       = model.FirstName,
                MiddleName      = model.MiddleName,
                LastName        = model.LastName,
                UserType        = model.Role,
                StudentNumber   = model.StudentNumber,
                EmployeeNumber  = model.EmployeeNumber,
                Section         = model.Section,
                RFIDNumber      = model.RFIDNumber,
                PhoneNumber     = model.PhoneNumber,
                AdviserEmail    = model.AdviserEmail,
                Level           = model.Level,
                Course          = model.Course,
                EmailConfirmed  = true,
                IsActive        = true,
                IsFirstLogin    = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError("", e.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.Role);

            TempData["Success"] = $"{model.Role} account for {model.FirstName} {model.LastName} created successfully.";
            return RedirectToAction(model.Role + "s");
        }

        // ── Edit ─────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var vm = new MISEditUserViewModel
            {
                Id             = user.Id,
                Role           = user.UserType,
                FirstName      = user.FirstName,
                MiddleName     = user.MiddleName,
                LastName       = user.LastName,
                Email          = user.Email ?? "",
                OutlookEmail   = user.OutlookEmail,
                StudentNumber  = user.StudentNumber,
                EmployeeNumber = user.EmployeeNumber,
                Section        = user.Section,
                RFIDNumber     = user.RFIDNumber,
                PhoneNumber    = user.PhoneNumber ?? "",
                AdviserEmail   = user.AdviserEmail,
                Level          = user.Level,
                Course         = user.Course
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MISEditUserViewModel model)
        {
            // Password fields are optional on edit
            if (!string.IsNullOrWhiteSpace(model.NewPassword) && model.NewPassword != model.ConfirmPassword)
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");

            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FirstName      = model.FirstName;
            user.MiddleName     = model.MiddleName;
            user.LastName       = model.LastName;
            user.Email          = model.Email;
            user.OutlookEmail   = model.OutlookEmail;
            user.StudentNumber  = model.StudentNumber;
            user.EmployeeNumber = model.EmployeeNumber;
            user.Section        = model.Section;
            user.RFIDNumber     = model.RFIDNumber;
            user.PhoneNumber    = model.PhoneNumber;
            user.AdviserEmail   = model.AdviserEmail;
            user.Level          = model.Level;
            user.Course         = model.Course;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var e in updateResult.Errors)
                    ModelState.AddModelError("", e.Description);
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!pwResult.Succeeded)
                {
                    foreach (var e in pwResult.Errors)
                        ModelState.AddModelError("", e.Description);
                    return View(model);
                }
            }

            TempData["Success"] = $"{user.FirstName} {user.LastName}'s account updated.";
            return RedirectToAction(user.UserType + "s");
        }

        // ── Toggle Active/Inactive ────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(string id, string returnAction = "UserInformation")
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"{user.FirstName} {user.LastName} has been {(user.IsActive ? "activated" : "deactivated")}.";
            }
            return RedirectToAction(returnAction);
        }

        // ── Delete (hard) ─────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Delete(string id, string returnAction)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var name = $"{user.FirstName} {user.LastName}";
                await _userManager.DeleteAsync(user);
                TempData["Success"] = $"Account for {name} has been deleted.";
            }
            return RedirectToAction(returnAction);
        }
    }
}
