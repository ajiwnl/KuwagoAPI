using static KuwagoAPI.Helper.LoanEnums;
using System.ComponentModel.DataAnnotations;

namespace KuwagoAPI.DTO
{
    public class LoanDTO
    {
        [Required]
        public MaritalStatus MaritalStatus { get; set; }

        [Required]
        public HighestEducation HighestEducation { get; set; }

        [Required]
        public string EmploymentInformation { get; set; }

        [Required]
        public string DetailedAddress { get; set; }

        [Required]
        public ResidentType ResidentType { get; set; }

        [Required]
        public LoanType LoanType { get; set; }

        [Required]
        public LoanAmount LoanAmount { get; set; }

        [Required]
        public string LoanPurpose { get; set; }
    }
}
