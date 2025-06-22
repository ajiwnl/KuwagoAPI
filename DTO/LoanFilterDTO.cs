using static KuwagoAPI.Helper.LoanEnums;

namespace KuwagoAPI.DTO
{
    public class LoanFilterDTO
    {
        public LoanStatus? LoanStatus { get; set; }
        public LoanType? LoanType { get; set; }
    }
}
