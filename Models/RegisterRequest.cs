using System.ComponentModel.DataAnnotations;

namespace KuwagoAPI.Models
{
    public class RegisterRequest
    {
        [Required]
        public string? FirstName { get; set; }

        [Required]
        public string? LastName { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? Username { get; set; }

        [Required]
        public string? PhoneNumber { get; set; }

        [Required]
        [MinLength(6)]
        public string? Password { get; set; }

        [Required]
        public int? Role { get; set; } 

    }
}
