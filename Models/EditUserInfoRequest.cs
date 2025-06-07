namespace KuwagoAPI.Models
{
    public class EditUserInfoRequest
    {
        public string UID { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public int? Status { get; set; }
    }

}
