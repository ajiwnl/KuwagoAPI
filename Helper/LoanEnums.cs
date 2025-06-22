namespace KuwagoAPI.Helper
{
    public class LoanEnums
    {
        public enum MaritalStatus
        {
            Single = 1,
            Married = 2,
            Divorced = 3,
            Widowed = 4
        }

        public enum HighestEducation
        {
            None = 0,
            HighSchool = 1,
            College = 2,
            Vocational = 3,
            Masters = 4,
            Doctorate = 5
        }

        public enum ResidentType
        {
            Owned = 1,
            Rented = 2,
            LivingWithRelatives = 3,
            GovernmentProvided = 4
        }

        public enum LoanType
        {
            Personal = 1,
            MicroBusiness = 2,
            Emergency = 3,
            Education = 4,
            Medical = 5,
            HomeImprovement = 6
        }

        public enum LoanAmount
        {
            Php1000 = 1000,
            Php2000 = 2000,
            Php5000 = 5000,
            Php10000 = 10000,
            Php20000 = 20000
        }

        public enum LoanStatus
        {
            Pending = 0,
            Active = 1,
            InProgress = 2,
            Denied = 3,
            Completed = 4
        }
    }
}
