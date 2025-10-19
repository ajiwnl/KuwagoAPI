namespace KuwagoAPI.DTO
{
    public class PaymentRequestDTO
    {
        public string PayableID { get; set; }
        public string BorrowerUID { get; set; }
        public double AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string Notes { get; set; }
        public string PaymentType { get; set; } // "Cash" or "ECash"

    }

}
