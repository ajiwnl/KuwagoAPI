namespace KuwagoAPI.DTO
{
    public class AgreedLoanFilterDTO
    {
        public string? AgreedLoanID { get; set; }
        public string? BorrowerUID { get; set; }
        public string? LenderUID { get; set; }
        public string? BorrowerFirstName { get; set; }
        public string? BorrowerLastName { get; set; }
        public string? LenderFirstName { get; set; }
        public string? LenderLastName { get; set; }
        public double? MinInterestRate { get; set; }
        public double? MaxInterestRate { get; set; }
        public DateTime? AgreementDateAfter { get; set; }
        public DateTime? AgreementDateBefore { get; set; }
    }

}
