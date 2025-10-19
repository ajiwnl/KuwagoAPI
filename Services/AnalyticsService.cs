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
            var loansSnapshot = await _firestoreDb.Collection("LoanRequests").GetSnapshotAsync();
            int activeLoans = loansSnapshot.Count;
            analytics["ActiveLoans"] = activeLoans;

            // --- 3️⃣ Total Revenue (Sum of Interests from Approved Loans) ---
            decimal totalRevenue = 0;

            foreach (var doc in loansSnapshot.Documents)
            {
                try
                {
                    if (!doc.ContainsField("LoanStatus")) continue;
                    var status = doc.GetValue<string>("LoanStatus");

                    // Only approved loans
                    if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!doc.ContainsField("LoanAmount") || !doc.ContainsField("InterestRate"))
                        continue;

                    double principal = doc.GetValue<double>("LoanAmount");
                    double interestRate = doc.GetValue<double>("InterestRate");

                    // Compute interest amount
                    double interestAmount = principal * (interestRate / 100.0);
                    totalRevenue += (decimal)interestAmount;
                }
                catch
                {
                    continue;
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


        public async Task<IEnumerable<object>> GetRevenueTrendAsync(string period)
        {
            var loansSnapshot = await _firestoreDb.Collection("LoanRequests").GetSnapshotAsync();
            var timezone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); // UTC+8

            // Collect (date, revenue) pairs
            var revenueData = new List<(DateTime Date, decimal Revenue)>();

            foreach (var doc in loansSnapshot.Documents)
            {
                try
                {
                    if (!doc.ContainsField("LoanStatus") ||
                        !string.Equals(doc.GetValue<string>("LoanStatus"), "Approved", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!doc.ContainsField("LoanAmount") || !doc.ContainsField("InterestRate"))
                        continue;

                    double principal = doc.GetValue<double>("LoanAmount");
                    double interestRate = doc.GetValue<double>("InterestRate");
                    decimal interestAmount = (decimal)(principal * (interestRate / 100.0));

                    // Determine approval or created date for grouping
                    DateTime createdAt;
                    if (doc.ContainsField("CreatedAt"))
                        createdAt = doc.GetValue<Timestamp>("CreatedAt").ToDateTime();
                    else
                        createdAt = DateTime.UtcNow;

                    createdAt = TimeZoneInfo.ConvertTimeFromUtc(createdAt, timezone);

                    revenueData.Add((createdAt, interestAmount));
                }
                catch
                {
                    continue;
                }
            }

            // Group by selected period
            IEnumerable<IGrouping<string, (DateTime Date, decimal Revenue)>> grouped;
            switch (period.ToLower())
            {
                case "daily":
                    grouped = revenueData
                        .GroupBy(r => r.Date.ToString("yyyy-MM-dd"))
                        .OrderBy(g => g.Key);
                    break;

                case "weekly":
                    grouped = revenueData
                        .GroupBy(r =>
                        {
                            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                            var week = cal.GetWeekOfYear(r.Date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                            return $"{r.Date.Year}-W{week:D2}";
                        })
                        .OrderBy(g => g.Key);
                    break;

                case "yearly":
                    grouped = revenueData
                        .GroupBy(r => r.Date.ToString("yyyy"))
                        .OrderBy(g => g.Key);
                    break;

                default: // monthly
                    grouped = revenueData
                        .GroupBy(r => r.Date.ToString("yyyy-MM"))
                        .OrderBy(g => g.Key);
                    break;
            }

            // Prepare chart data
            var result = grouped.Select(g => new
            {
                Period = g.Key,
                Revenue = g.Sum(x => x.Revenue)
            });

            return result;
        }


    }
}
