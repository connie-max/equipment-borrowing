// SeedData.cs
// One small job: put the first users and items in the database
// the first time the app runs, so the demo works straight away.
//
// Rule: only seed when the tables are EMPTY.
// If there is already data, do nothing, so we never add duplicates
// and never wipe out real data.
//
// Demo accounts (write these on the board in defence):
//   Admin:    admin@school.ph  / admin123
//   Borrower: anthony@school.ph   / borrower123
//
// Demo items:
//   1. Projector (Equipment), 2. Laptop (Equipment),
//   3. Speaker (Equipment), 4. Meeting Room (Room)

using BorrowingApi.Helpers;
using BorrowingApi.Models;

namespace BorrowingApi.Data;

public static class SeedData
{
    public static void Seed(AppDbContext db)
    {
        // If there is already at least one user, the database is not empty. Stop.
        if (db.Users.Any())
        {
            return;
        }

        // Two users. Passwords are saved as hashes, never as plain text.
        var users = new List<User>
        {
            new User
            {
                Name = "Admin",
                Email = "admin@school.ph",
                PasswordHash = PasswordHelper.Hash("admin123"),
                Role = "Admin"
            },
            new User
            {
                Name = "Anthony Dela Cruz",
                Email = "anthony@school.ph",
                PasswordHash = PasswordHelper.Hash("borrower123"),
                Role = "Borrower"
            }
        };
        db.Users.AddRange(users);

        // Four items. All start as Available.
        var items = new List<Item>
        {
            new Item { Name = "Projector", Type = "Equipment", Description = "HD projector for classroom use", Status = "Available" },
            new Item { Name = "Laptop", Type = "Equipment", Description = "Laptop for presentations", Status = "Available" },
            new Item { Name = "Speaker", Type = "Equipment", Description = "Portable speaker", Status = "Available" },
            new Item { Name = "Meeting Room", Type = "Room", Description = "Small meeting room, seats 8", Status = "Available" }
        };
        db.Items.AddRange(items);

        // Save both lists to the database in one go.
        db.SaveChanges();
    }
}
