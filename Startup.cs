namespace KuwagoAPI.Controllers
{
    using FirebaseAdmin;
    using Google.Apis.Auth.OAuth2;
    using Google.Cloud.Firestore;

    namespace TMS
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
                // Set the path for the Firebase service account key
                string path = Path.Combine(Directory.GetCurrentDirectory(), "json", "json.json");
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);

                // Initialize Firebase if not already initialized
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(path)
                    });
                }

                // Register FirestoreDb instance
                services.AddSingleton(provider =>
                {
                    return FirestoreDb.Create("mimanutms-d516c");
                });

                services.AddSession(options =>
                {
                    options.IdleTimeout = TimeSpan.FromMinutes(30);
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                });
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                app.UseSession();
            }
        }
    }

}
