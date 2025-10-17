using Google.Cloud.Firestore;

namespace KuwagoAPI.Services
{
    public class AnalyticsService
    {
        private readonly FirestoreDb _firestoreDb;

        public AnalyticsService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        public async Task<Dictionary<string, object>> GetAnalyticsAsync()
        {
            var analytics = new Dictionary<string, object>();

            // --- 1️⃣ Total Users ---
            var usersSnapshot = await _firestoreDb.Collection("Users").GetSnapshotAsync();
            int totalUsers = usersSnapshot.Count;
            analytics["TotalUsers"] = totalUsers;

            // --- 2️⃣ Active Loans ---
            var loansSnapshot = await _firestoreDb.Collection("LoanRequests")
                                                  .GetSnapshotAsync();
            int activeLoans = loansSnapshot.Count;
            analytics["ActiveLoans"] = activeLoans;

            // --- 3️⃣ Total Revenue (Sum of AmountPaid) ---
            var paymentsSnapshot = await _firestoreDb.Collection("Payments").GetSnapshotAsync();
            decimal totalRevenue = 0;

            foreach (var doc in paymentsSnapshot.Documents)
            {
                if (doc.ContainsField("AmountPaid"))
                {
                    var value = doc.GetValue<double>("AmountPaid");
                    totalRevenue += (decimal)value;
                }
            }

            analytics["TotalRevenue"] = totalRevenue;

            return analytics;
        }
    }
}
