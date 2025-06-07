using System.ComponentModel.DataAnnotations;

namespace KuwagoAPI.Models
{
    public class ChangePasswordRequest
    {
        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = null!;
    }
}
