using System.ComponentModel.DataAnnotations;

namespace BookHiveLibrary.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required]
        public string Email { get; set; } = "";

        [Required]
        public string Code { get; set; } = "";
    }
}