using KuwagoAPI.Helper;

namespace KuwagoAPI.DTO
{
    public class LoanAgreementDTO
    {
        public string LoanRequestID { get; set; }
        public string UpdatedLoanStatus { get; set; }
        public int UpdatedLoanAmount { get; set; }
        public double InterestRate { get; set; }
        public LoanEnums.TermsOfMonths? TermsOfMonths { get; set; }
        public LoanEnums.PaymentType? PaymentType { get; set; }

    }
}
