using Google.Cloud.Firestore;
using KuwagoAPI.Models;

namespace KuwagoAPI.Services
{
    public class CreditScoreService
    {
        private readonly FirestoreDb _firestoreDb;

        public CreditScoreService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        public async Task<mCreditScores?> GetCreditScoreAsync(string uid)
        {
            var docRef = _firestoreDb.Collection("CreditScores").Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists ? snapshot.ConvertTo<mCreditScores>() : null;
        }

        public async Task InitializeCreditScoreAsync(string uid)
        {
            var creditScore = new mCreditScores
            {
                UID = uid,
                Score = 600, // Starting score
                TotalLoans = 0,
                SuccessfulRepayments = 0,
                MissedRepayments = 0,
                LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            await _firestoreDb.Collection("CreditScores").Document(uid).SetAsync(creditScore);
        }

        public async Task UpdateCreditScoreAsync(string uid, bool onTimeRepayment)
        {
            var docRef = _firestoreDb.Collection("CreditScores").Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();
            if (!snapshot.Exists) return;

            var creditScore = snapshot.ConvertTo<mCreditScores>();

            creditScore.TotalLoans++;

            if (onTimeRepayment)
            {
                creditScore.SuccessfulRepayments++;
                creditScore.Score += 20;
            }
            else
            {
                creditScore.MissedRepayments++;
                creditScore.Score -= 30;
            }

            creditScore.Score = Math.Clamp(creditScore.Score, 300, 850);
            creditScore.LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow);

            await docRef.SetAsync(creditScore);
        }
    }

}
    