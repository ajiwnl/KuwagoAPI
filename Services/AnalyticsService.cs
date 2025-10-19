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

        public async Task<IEnumerable<object>> GetUserGrowthAsync(string period)
        {
            var snapshot = await _firestoreDb.Collection("Users").GetSnapshotAsync();
            var timezone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); // UTC+8
            var users = new List<DateTime>();

            // Collect createdAt timestamps
            foreach (var doc in snapshot.Documents)
            {
                if (!doc.ContainsField("createdAt")) continue;

                try
                {
                    DateTime createdAt = doc.GetValue<Timestamp>("createdAt").ToDateTime();
                    createdAt = TimeZoneInfo.ConvertTimeFromUtc(createdAt, timezone);
                    users.Add(createdAt);
                }
                catch { continue; }
            }

            // Group by period
            IEnumerable<IGrouping<string, DateTime>> grouped;
            switch (period.ToLower())
            {
                case "daily":
                    grouped = users
                        .GroupBy(u => u.ToString("yyyy-MM-dd"))
                        .OrderBy(g => g.Key);
                    break;

                case "weekly":
                    grouped = users
                        .GroupBy(u =>
                        {
                            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                            var week = cal.GetWeekOfYear(u, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                            return $"{u.Year}-W{week:D2}";
                        })
                        .OrderBy(g => g.Key);
                    break;

                case "yearly":
                    grouped = users
                        .GroupBy(u => u.ToString("yyyy"))
                        .OrderBy(g => g.Key);
                    break;

                default: // monthly
                    grouped = users
                        .GroupBy(u => u.ToString("yyyy-MM"))
                        .OrderBy(g => g.Key);
                    break;
            }

            // Build chart-friendly data
            var result = grouped.Select(g => new
            {
                Period = g.Key,
                Count = g.Count()
            });

            return result;
        }

    }
}
