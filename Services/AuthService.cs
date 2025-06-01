using Firebase.Auth;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using KuwagoAPI.Models;
using System.ComponentModel.DataAnnotations;
using KuwagoAPI.Helper;

namespace KuwagoAPI.Services
{
    public class AuthService
    {
        private readonly FirebaseAuthProvider _firebaseAuth;
        private readonly FirestoreDb _firestoreDb;

        public AuthService(FirebaseAuthProvider firebaseAuth, FirestoreDb firestoreDb)
        {
            _firebaseAuth = firebaseAuth;
            _firestoreDb = firestoreDb;
        }

        public async Task<StatusResponse> RegisterUserAsync(RegisterRequest request)
        {
            // Basic validation is already handled via [Required] and [EmailAddress], but you can still add custom checks if needed.

            // Check if email already exists
            var checkEmail = await _firestoreDb.Collection("Users")
                .WhereEqualTo("Email", request.Email)
                .GetSnapshotAsync();

            // Check if username already exists
            var checkUsername = await _firestoreDb.Collection("Users")
                .WhereEqualTo("Username", request.Username)
                .GetSnapshotAsync();

            if (checkEmail.Documents.Count > 0)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Email already registered.",
                    StatusCode = 409 // Conflict
                };
            }

            if (checkUsername.Documents.Count > 0)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Username already exist.",
                    StatusCode = 409 // Conflict
                };
            }

            // Validate phone number (Philippines format: must start with '09' and be 11 digits)
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                !System.Text.RegularExpressions.Regex.IsMatch(request.PhoneNumber, @"^09\d{9}$"))
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Invalid phone number. It must start with '09' and contain 11 digits.",
                    StatusCode = 400 // Bad Request
                };
            }


            try
            {
                var auth = await _firebaseAuth.CreateUserWithEmailAndPasswordAsync(request.Email, request.Password);
                await _firebaseAuth.SendEmailVerificationAsync(auth.FirebaseToken);

                var uid = auth.User.LocalId;
                var docRef = _firestoreDb.Collection("Users").Document(uid);

                var user = new mUser
                {
                    UID = uid,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    ProfilePicture = "https://i.pinimg.com/474x/e6/e4/df/e6e4df26ba752161b9fc6a17321fa286.jpg",
                    Password = request.Password,
                    Username = request.Username,
                    createdAt = Timestamp.FromDateTime(DateTime.UtcNow),
                    Role = "User"
                };

                await docRef.SetAsync(user);

                return new StatusResponse
                { 
                    Success = true,
                    Message = "Registration successful! Please verify your email.",
                    StatusCode = 201 // Created
                };
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = ex.Message,
                    StatusCode = 500
                };
            }
        }

        public async Task<StatusResponse> LoginUserAsync(string email, string password)
        {
            // Basic email & password validation before calling Firebase
            var emailValidator = new EmailAddressAttribute();
            if (!emailValidator.IsValid(email))
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Invalid email format.",
                    StatusCode = 400
                };
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Password is required.",
                    StatusCode = 400
                };
            }

            try
            {
                var auth = await _firebaseAuth.SignInWithEmailAndPasswordAsync(email, password);
                var user = await _firebaseAuth.GetUserAsync(auth.FirebaseToken);

                if (!user.IsEmailVerified)
                {
                    return new StatusResponse
                    {
                        Success = false,
                        Message = "Email not verified.",
                        StatusCode = 403 // Forbidden
                    };
                }

                return new StatusResponse
                {
                    Success = true,
                    Message = user.LocalId, // You can change this if needed
                    StatusCode = 200
                };
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                string message = ex.Message.Contains("INVALID_LOGIN_CREDENTIALS")
                    ? "Incorrect email or password."
                    : ex.Message;

                return new StatusResponse
                {
                    Success = false,
                    Message = message,
                    StatusCode = 401 // Unauthorized
                };
            }
        }

        public async Task<StatusResponse> ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Invalid email address.",
                    StatusCode = 400
                };
            }

            try
            {
                await _firebaseAuth.SendPasswordResetEmailAsync(email);
                return new StatusResponse
                {
                    Success = true,
                    Message = "Password reset email sent. Please check your inbox.",
                    StatusCode = 200
                };
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                string msg = ex.Message.Contains("EMAIL_NOT_FOUND")
                    ? "No account found with that email."
                    : ex.Message;

                return new StatusResponse
                {
                    Success = false,
                    Message = msg,
                    StatusCode = 404
                };
            }
        }

        public async Task<mUser?> GetUserByUIDAsync(string uid)
        {
            var docRef = _firestoreDb.Collection("Users").Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
                return null;

            return snapshot.ConvertTo<mUser>();
        }


    }
}
