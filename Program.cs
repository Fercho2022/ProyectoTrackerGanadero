using Microsoft.EntityFrameworkCore;
using ApiWebTrackerGanado.Models;
using ApiWebTrackerGanado.Data;

class Program
{
    public static async Task Main(string[] args)
    {
        var connectionString = "Host=localhost;Database=TrackerGanadero;Username=postgres;Password=123456;";

        var optionsBuilder = new DbContextOptionsBuilder<CattleTrackingContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var context = new CattleTrackingContext(optionsBuilder.Options);

        // Check if license already exists
        var existingLicense = await context.Licenses
            .FirstOrDefaultAsync(l => l.LicenseKey == "TG-2024-1234-5678-9ABC");

        if (existingLicense == null)
        {
            // First create a customer if not exists
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.CompanyName == "Test Company");
            if (customer == null)
            {
                customer = new Customer
                {
                    UserId = 1, // Assuming user with ID 1 exists
                    CompanyName = "Test Company",
                    ContactEmail = "test@company.com",
                    ContactPhone = "123-456-7890",
                    Address = "Test Address 123",
                    Status = "Active"
                };
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
            }

            var license = new License
            {
                CustomerId = customer.Id,
                LicenseKey = "TG-2024-1234-5678-9ABC",
                LicenseType = "Premium",
                MaxTrackers = 50,
                MaxFarms = 5,
                MaxUsers = 10,
                Status = "Active"
            };

            context.Licenses.Add(license);
            await context.SaveChangesAsync();

            Console.WriteLine($"✅ Test license created: {license.LicenseKey}");
            Console.WriteLine($"✅ Customer: {customer.CompanyName}");
        }
        else
        {
            Console.WriteLine($"✅ Test license already exists: {existingLicense.LicenseKey}");
        }
    }
}