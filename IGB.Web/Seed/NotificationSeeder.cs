using IGB.Web.Notifications;

namespace IGB.Web.Seed;

public static class NotificationSeeder
{
    public static async Task SeedAsync(INotificationStore store, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Global
        for (int i = 1; i <= 10; i++)
        {
            await store.AddGlobalAsync(new NotificationItem(
                Id: $"seed-global-{i}",
                Title: "Welcome to IGB (Seed)",
                Message: $"This is seeded global notification #{i}.",
                CreatedAtUtc: now.AddMinutes(-i)
            ), ct);
        }

        // Roles
        for (int i = 1; i <= 10; i++)
        {
            await store.AddForRoleAsync("Admin", new NotificationItem(
                Id: $"seed-admin-{i}",
                Title: "Admin Alert (Seed)",
                Message: $"Seeded admin notification #{i}.",
                CreatedAtUtc: now.AddMinutes(-i)
            ), ct);

            await store.AddForRoleAsync("Tutor", new NotificationItem(
                Id: $"seed-tutor-{i}",
                Title: "Tutor Alert (Seed)",
                Message: $"Seeded tutor notification #{i}.",
                CreatedAtUtc: now.AddMinutes(-i)
            ), ct);

            await store.AddForRoleAsync("Student", new NotificationItem(
                Id: $"seed-student-{i}",
                Title: "Student Alert (Seed)",
                Message: $"Seeded student notification #{i}.",
                CreatedAtUtc: now.AddMinutes(-i)
            ), ct);
        }
    }
}


