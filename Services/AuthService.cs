using Firebase.Auth;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using KuwagoAPI.Models;
using System.ComponentModel.DataAnnotations;
using KuwagoAPI.Helper;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Newtonsoft.Json.Linq;
using KuwagoAPI.DTO;

namespace KuwagoAPI.Services
{
    public class AuthService
    {
        private readonly FirebaseAuthProvider _firebaseAuth;
        private readonly FirestoreDb _firestoreDb;
        private readonly FirebaseAdmin.Auth.FirebaseAuth _firebaseAdminAuth;

        public AuthService(FirebaseAuthProvider firebaseAuth, FirestoreDb firestoreDb, FirebaseAdmin.Auth.FirebaseAuth firebaseAdminAuth)
        {
            _firebaseAuth = firebaseAuth;
            _firestoreDb = firestoreDb;
            _firebaseAdminAuth = firebaseAdminAuth;
            _firebaseAdminAuth = firebaseAdminAuth;
        }

        public string GenerateJwtToken(string uid, int role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes("a-string-secret-at-least-256-bits-long");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.NameIdentifier, uid),
            new Claim(ClaimTypes.Role, role.ToString())
        }),
                Expires = DateTime.UtcNow.AddMinutes(60),
                Issuer = "KuwagoAPI",
                Audience = "KuwagoClient",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
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

            int role = request.Role ?? (int)UserRole.Borrower;
            if (!Enum.IsDefined(typeof(UserRole), role))
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Invalid user role.",
                    StatusCode = 400
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
                    Username = request.Username,
                    createdAt = Timestamp.FromDateTime(DateTime.UtcNow),
                    Role = role,
                    Status = (int)UserStatus.Active
                };

                await docRef.SetAsync(user);

                return new StatusResponse
                { 
                    Success = true,
                    Message = "Registration successful! Please verify your email.",
                    StatusCode = 201
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
                    Message = user.LocalId,
                    StatusCode = 200,
                    Data = new { UID = user.LocalId }
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

        public async Task<List<mUser>> GetAllUsersAsync(string? UID = null, string? LastName = null, string? Email = null, int? Role = null)
        {
            CollectionReference usersRef = _firestoreDb.Collection("Users");

            // If UID is provided, fetch by document ID (cannot combine with other filters)
            if (!string.IsNullOrEmpty(UID))
            {
                DocumentSnapshot snapshot = await usersRef.Document(UID).GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    return new List<mUser> { snapshot.ConvertTo<mUser>() };
                }
                return new List<mUser>();
            }

            Query query = usersRef;

            if (!string.IsNullOrEmpty(LastName))
                query = query.WhereEqualTo("LastName", LastName);

            if (!string.IsNullOrEmpty(Email))
                query = query.WhereEqualTo("Email", Email);

            if (Role.HasValue)
                query = query.WhereEqualTo("Role", Role.Value);

            var snapshotList = await query.GetSnapshotAsync();
            return snapshotList.Documents.Select(doc => doc.ConvertTo<mUser>()).ToList();
        }

        public async Task<StatusResponse> EditUserInfoRequest(string uid, EditUserInfoRequest request)
        {
            var userDoc = _firestoreDb.Collection("Users").Document(uid);
            var snapshot = await userDoc.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "User not found.",
                    StatusCode = 404
                };
            }

            var updates = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(request.FirstName)) updates["FirstName"] = request.FirstName;
            if (!string.IsNullOrWhiteSpace(request.LastName)) updates["LastName"] = request.LastName;
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber)) updates["PhoneNumber"] = request.PhoneNumber;
            if (Enum.IsDefined(typeof(UserStatus), request.Status))
            {
                var currentStatus = snapshot.GetValue<int>("Status");
                if (request.Status != currentStatus)
                {
                    updates["Status"] = request.Status;
                }
            }



            if (updates.Count > 0)
                await userDoc.UpdateAsync(updates);

            // Fetch updated user data
            var updatedSnapshot = await userDoc.GetSnapshotAsync();
            var updatedUser = updatedSnapshot.ConvertTo<mUser>();

            var userDto = new UserDto
            {
                UID = updatedUser.UID,
                FullName = $"{updatedUser.FirstName} {updatedUser.LastName}",
                Email = updatedUser.Email,
                PhoneNumber = updatedUser.PhoneNumber,
                Username = updatedUser.Username,
                ProfilePicture = updatedUser.ProfilePicture,
                Role = Enum.IsDefined(typeof(UserRole), updatedUser.Role) ? ((UserRole)updatedUser.Role).ToString() : "Unknown",
                CreatedAt = updatedUser.createdAt.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Status = Enum.IsDefined(typeof(UserStatus), updatedUser.Status) ? ((UserStatus)updatedUser.Status).ToString() : "Unknown"

            };

            return new StatusResponse
            {
                Success = true,
                Message = "Profile updated successfully.",
                StatusCode = 200,
                 Data = userDto
            };
        }

        public async Task<StatusResponse> ChangeUserEmailAsync(string uid, string newEmail, string currentUserToken)
        {
            try
            {
                // 1. Update email via Firebase Admin SDK
                var userRecordArgs = new UserRecordArgs()
                {
                    Uid = uid,
                    Email = newEmail,
                    EmailVerified = false // Because email changed, mark as unverified
                };

                await _firebaseAdminAuth.UpdateUserAsync(userRecordArgs);

                // 2. Send verification email using Firebase client SDK
                //valid Firebase ID token (currentUserToken) from the client side
                await _firebaseAuth.SendEmailVerificationAsync(currentUserToken);

                // 3. Also update email in Firestore user document
                var userDoc = _firestoreDb.Collection("Users").Document(uid);
                await userDoc.UpdateAsync("Email", newEmail);

                return new StatusResponse
                {
                    Success = true,
                    Message = "Email changed successfully. Verification email sent to new address.",
                    StatusCode = 200
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


        public async Task<StatusResponse> ChangePasswordAsync(string uid, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = "Password must be at least 6 characters long.",
                    StatusCode = 400
                };
            }

            try
            {
                // Update password in Firebase Auth
                var userRecordArgs = new UserRecordArgs
                {
                    Uid = uid,
                    Password = newPassword
                };

                var userRecord = await _firebaseAdminAuth.UpdateUserAsync(userRecordArgs);

                return new StatusResponse
                {
                    Success = true,
                    Message = "Password updated successfully.",
                    StatusCode = 200
                };
            }
            catch (FirebaseAdmin.Auth.FirebaseAuthException ex)
            {
                return new StatusResponse
                {
                    Success = false,
                    Message = ex.Message,
                    StatusCode = 500
                };
            }
        }

        public async Task UpdateUserProfilePictureAsync(string uid, string imageUrl)
        {
            var userDocRef = _firestoreDb.Collection("Users").Document(uid);
            await userDocRef.UpdateAsync("ProfilePicture", imageUrl);
        }




    }
}
