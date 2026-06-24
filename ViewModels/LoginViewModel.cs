using System.ComponentModel.DataAnnotations;

namespace BookHiveLibrary.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email or Username")]
        public string EmailOrUsername { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }

        [EmailAddress]
        [Display(Name = "Microsoft Outlook Email")]
        public string? OutlookEmail { get; set; }
    }
}