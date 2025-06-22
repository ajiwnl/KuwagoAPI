// DTO/LoanFilterDTO.cs
namespace KuwagoAPI.DTO
{
    public class LoanFilterDTOv2
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? LoanStatus { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
    }
}
