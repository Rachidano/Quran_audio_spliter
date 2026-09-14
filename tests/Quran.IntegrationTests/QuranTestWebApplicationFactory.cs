using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Quran.Infrastructure.Identity;
using Quran.Infrastructure.Persistence;

namespace Quran.IntegrationTests;

public class QuranTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString = "Host=localhost;Port=5432;Database=quran_db_test;Username=admin;Password=admin;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Storage:BasePath"] = Path.Combine(AppContext.BaseDirectory, "TestStorage")
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace QuranDbContext with test connection
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<QuranDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<QuranDbContext>(options =>
            {
                options.UseNpgsql(TestConnectionString, b =>
                    b.MigrationsAssembly(typeof(QuranDbContext).Assembly.FullName));
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            IdentitySeeder.SeedRolesAsync(roleManager).GetAwaiter().GetResult();
            IdentitySeeder.CreateUserAsync(userManager, "admin@quranwarsh.local", "Admin@123456", IdentitySeeder.RoleAdmin).GetAwaiter().GetResult();
        });
    }
}
