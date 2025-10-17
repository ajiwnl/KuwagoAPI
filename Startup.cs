using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using KuwagoAPI.Services;
using Firebase.Auth;
using CloudinaryDotNet.Actions;
using CloudinaryDotNet;

namespace KuwagoAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Set the environment variable for Firestore credentials
            string path = Path.Combine(Directory.GetCurrentDirectory(), "json", "kuwagodb-firebase-adminsdk-fbsvc-9947ca1fee.json");
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);

            // Initialize Firebase if not already initialized
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(path)
                });
            }
            
            services.AddSingleton(FirebaseAdmin.Auth.FirebaseAuth.GetAuth(FirebaseApp.DefaultInstance));

            // Register FirestoreDb and Auth instance
            services.AddSingleton(new FirebaseAuthProvider(new FirebaseConfig("AIzaSyDk9o89s1prZ4aQd1dzB0XiUQWCUGDD7n8")));
            services.AddSingleton(provider =>
            {
                return FirestoreDb.Create("kuwagodb");
            });

            services.AddScoped<FirestoreService>();
            services.AddScoped<AuthService>();
            services.AddScoped<IdentityVerificationService>();
            services.AddDistributedMemoryCache();
            services.AddScoped<CloudinaryService>();
            services.AddHttpClient<FaceVerificationService>();
            services.AddScoped<LoanService>();
            services.AddScoped<CreditScoreService>();
            services.AddScoped<PaymentService>();
            //services.AddScoped<AIAssessmentService>();
            services.AddScoped<AnalyticsService>();


            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            services.AddControllers();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseSession();
            app.UseAuthentication(); 
            app.UseAuthorization();  

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

    }

    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService()
        {
            var account = new Account(
                "dip5gm9sj",
                "942788152523517",
                "OFNSf-cxYC3zwb-uAozj7XwxXHU"
            );
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadIDAndSelfieAsync(IFormFile file)
        {
            var transformation = new Transformation()
                .Width(500)    // Set the width to 500px
                .Height(500)   // Set the height to 500px
                .Crop("fill");
            // Prepare the upload parameters with transformation
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                Folder = "id_photos",
                Transformation = transformation // Apply transformation
            };

            // Upload the image and get the result
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            // Return the secure URL of the uploaded image
            return uploadResult?.SecureUrl.ToString();
        }

    }

}
