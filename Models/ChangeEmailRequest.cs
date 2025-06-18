using System.ComponentModel.DataAnnotations;

namespace KuwagoAPI.Models
{
    public class ChangeEmailRequest
    {
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; } = null!;

        [Required]
        public string FirebaseToken { get; set; } = null!;
    }
}
