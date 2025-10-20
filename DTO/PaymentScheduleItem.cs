namespace KuwagoAPI.DTO
{
    public class PaymentScheduleItem
    {
        public string DueDate { get; set; }
        public string? PaymentDate { get; set; }
        public double AmountPaid { get; set; }
        public double RequiredToPayEveryMonth { get; set; }
        public double ActualPayment { get; set; }
        public string Status { get; set; }
    }
}
