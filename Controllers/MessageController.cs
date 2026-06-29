using BookHiveLibrary.Data;
using BookHiveLibrary.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Controllers
{
    [Authorize]
    public class MessageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessageController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Inbox page
        public async Task<IActionResult> Index(string? withUserId)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Challenge();

            // All users the current user has had a conversation with
            var contactIds = await _context.Messages
                .Where(m => m.SenderId == me.Id || m.ReceiverId == me.Id)
                .Select(m => m.SenderId == me.Id ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            var contacts = await _userManager.Users
                .Where(u => contactIds.Contains(u.Id))
                .ToListAsync();

            // All users available to message (everyone except self)
            var allUsers = await _userManager.Users
                .Where(u => u.Id != me.Id && u.IsActive)
                .OrderBy(u => u.UserType).ThenBy(u => u.LastName)
                .ToListAsync();

            ViewBag.Me       = me;
            ViewBag.Contacts = contacts;
            ViewBag.AllUsers = allUsers;
            ViewBag.WithUserId = withUserId;

            return View();
        }

        // AJAX: get conversation messages between me and another user
        [HttpGet]
        public async Task<IActionResult> GetConversation(string otherUserId)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var messages = await _context.Messages
                .Where(m => (m.SenderId == me.Id && m.ReceiverId == otherUserId) ||
                            (m.SenderId == otherUserId && m.ReceiverId == me.Id))
                .OrderBy(m => m.SentAt)
                .Select(m => new {
                    m.Id,
                    m.Content,
                    m.SenderId,
                    m.IsRead,
                    sentAt = m.SentAt.ToString("MMM d, h:mm tt")
                })
                .ToListAsync();

            // Mark received messages as read
            var unread = await _context.Messages
                .Where(m => m.SenderId == otherUserId && m.ReceiverId == me.Id && !m.IsRead)
                .ToListAsync();
            unread.ForEach(m => m.IsRead = true);
            if (unread.Any()) await _context.SaveChangesAsync();

            var other = await _userManager.FindByIdAsync(otherUserId);
            return Json(new {
                messages,
                myId = me.Id,
                otherName = other != null ? other.FirstName + " " + other.LastName : "Unknown",
                otherType = other?.UserType ?? ""
            });
        }

        // AJAX: send a message
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(string receiverId, string content)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(content)) return BadRequest();

            var msg = new Message
            {
                SenderId   = me.Id,
                ReceiverId = receiverId,
                Content    = content.Trim(),
                SentAt     = DateTime.Now
            };
            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            return Json(new {
                msg.Id,
                msg.Content,
                msg.SenderId,
                sentAt = msg.SentAt.ToString("MMM d, h:mm tt")
            });
        }

        // AJAX: unread count for badge
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Json(new { count = 0 });

            var count = await _context.Messages
                .CountAsync(m => m.ReceiverId == me.Id && !m.IsRead);
            return Json(new { count });
        }

        // AJAX: conversation list for the sidebar
        [HttpGet]
        public async Task<IActionResult> GetConversationList()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var allMsgs = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == me.Id || m.ReceiverId == me.Id)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            var convos = allMsgs
                .GroupBy(m => m.SenderId == me.Id ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var last   = g.First();
                    var other  = last.SenderId == me.Id ? last.Receiver : last.Sender;
                    var unread = g.Count(m => m.ReceiverId == me.Id && !m.IsRead);
                    return new {
                        userId    = other?.Id ?? "",
                        name      = other != null ? other.FirstName + " " + other.LastName : "Unknown",
                        userType  = other?.UserType ?? "",
                        lastMsg   = last.Content.Length > 40 ? last.Content[..40] + "…" : last.Content,
                        sentAt    = last.SentAt.ToString("MMM d, h:mm tt"),
                        unread
                    };
                })
                .ToList();

            return Json(convos);
        }
    }
}
