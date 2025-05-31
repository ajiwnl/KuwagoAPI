using System.ComponentModel.DataAnnotations;

namespace KuwagoAPI.Models
{
    public class ForgotPassRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

    }
}
