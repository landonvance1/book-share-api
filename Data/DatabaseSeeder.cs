using BookSharingApp.Models;
using BookSharingApp.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<User> userManager)
        {
            await context.Database.EnsureCreatedAsync();

            // Seed users first
            await SeedUsersAsync(userManager);

            // Seed communities
            await SeedCommunitiesAsync(context);

            // Seed community users
            await SeedCommunityUsersAsync(context);

            // Seed books
            await SeedBooksAsync(context);

            // Seed user books
            await SeedUserBooksAsync(context);

            // Seed shares
            await SeedSharesAsync(context);
        }

        private static async Task SeedUsersAsync(UserManager<User> userManager)
        {
            // Check if users already exist
            if (userManager.Users.Count() > 1)
                return;

            var seedUsers = new List<(User user, string password)>
            {
                (new User
                {
                    Id = "user-001",
                    UserName = "l@v.com",
                    Email = "l@v.com",
                    FirstName = "Landon",
                    LastName = "Vance",
                    EmailConfirmed = true
                }, "password"),

                (new User
                {
                    Id = "user-002",
                    UserName = "john.doe@email.com",
                    Email = "john.doe@email.com",
                    FirstName = "John",
                    LastName = "Doe",
                    EmailConfirmed = true
                }, "password"),

                (new User
                {
                    Id = "user-003",
                    UserName = "jane.smith@email.com",
                    Email = "jane.smith@email.com",
                    FirstName = "Jane",
                    LastName = "Smith",
                    EmailConfirmed = true
                }, "password"),

                (new User
                {
                    Id = "user-004",
                    UserName = "bob.wilson@email.com",
                    Email = "bob.wilson@email.com",
                    FirstName = "Bob",
                    LastName = "Wilson",
                    EmailConfirmed = true
                }, "password"),

                (new User
                {
                    Id = "user-005",
                    UserName = "alice.brown@email.com",
                    Email = "alice.brown@email.com",
                    FirstName = "Alice",
                    LastName = "Brown",
                    EmailConfirmed = true
                }, "password")
            };

            foreach (var (user, password) in seedUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(user.Email!);
                if (existingUser == null)
                {
                    var result = await userManager.CreateAsync(user, password);
                    if (!result.Succeeded)
                    {
                        throw new Exception($"Failed to create user {user.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
            }
        }

        private static async Task SeedBooksAsync(ApplicationDbContext context)
        {
            if (await context.Books.AnyAsync())
                return; // Database has been seeded

            var books = new List<Book>
            {
                new Book { Title = "The Great Gatsby", Author = "F. Scott Fitzgerald" }, //id 1
                new Book { Title = "To Kill a Mockingbird", Author = "Harper Lee" }, //id 2
                new Book { Title = "1984", Author = "George Orwell" }, //id 3
                
                // Hilary Mantel
                new Book { Title = "Wolf Hall", Author = "Hilary Mantel" }, //id 4
                new Book { Title = "Bring Up the Bodies", Author = "Hilary Mantel" }, //id 5
                new Book { Title = "The Mirror & the Light", Author = "Hilary Mantel" }, //id 6
                new Book { Title = "A Place of Greater Safety", Author = "Hilary Mantel" }, //id 7
                
                // Joe Abercrombie
                new Book { Title = "The Blade Itself", Author = "Joe Abercrombie" }, //id 8
                new Book { Title = "Before They Are Hanged", Author = "Joe Abercrombie" }, //id 9
                new Book { Title = "Last Argument of Kings", Author = "Joe Abercrombie" }, //id 10
                new Book { Title = "Best Served Cold", Author = "Joe Abercrombie" }, //id 11
                
                // Ursula K Le Guin
                new Book { Title = "The Left Hand of Darkness", Author = "Ursula K Le Guin" }, //id 12
                new Book { Title = "A Wizard of Earthsea", Author = "Ursula K Le Guin" }, //id 13
                new Book { Title = "The Dispossessed", Author = "Ursula K Le Guin" }, //id 14
                new Book { Title = "The Lathe of Heaven", Author = "Ursula K Le Guin" }, //id 15
                
                // Robin Hobb
                new Book { Title = "Assassin's Apprentice", Author = "Robin Hobb" }, //id 16
                new Book { Title = "Royal Assassin", Author = "Robin Hobb" }, //id 17
                new Book { Title = "Assassin's Quest", Author = "Robin Hobb" }, //id 18
                new Book { Title = "Ship of Magic", Author = "Robin Hobb" }, //id 19
                
                // Susanna Clarke
                new Book { Title = "Jonathan Strange & Mr Norrell", Author = "Susanna Clarke" }, //id 20
                new Book { Title = "Piranesi", Author = "Susanna Clarke" }, //id 21
                new Book { Title = "The Ladies of Grace Adieu", Author = "Susanna Clarke" }, //id 22
                new Book { Title = "The Wood at Midwinter", Author = "Susanna Clarke" } //id 23
            };

            context.Books.AddRange(books);
            await context.SaveChangesAsync();
        }

        private static async Task SeedCommunitiesAsync(ApplicationDbContext context)
        {
            if (await context.Communities.AnyAsync())
                return; // Database has been seeded

            var communities = new List<Community>
            {
                new Community { Name = "The Loop", Active = true, CreatedBy = "user-001" }, // Created by Landon
                new Community { Name = "Cornerstone", Active = true, CreatedBy = "user-002" }, // Created by John
                new Community { Name = "Tower Grove South", Active = true, CreatedBy = "user-003" }, // Created by Jane
                new Community { Name = "Inactive Group", Active = false, CreatedBy = "user-004" }, // Created by Bob
                new Community { Name = "DnD Boys", Active = true, CreatedBy = "user-005" } // Created by Alice
            };

            context.Communities.AddRange(communities);
            await context.SaveChangesAsync();
        }

        private static async Task SeedCommunityUsersAsync(ApplicationDbContext context)
        {
            if (await context.CommunityUsers.AnyAsync())
                return; // Database has been seeded

            var communityUsers = new List<CommunityUser>
            {
                // The Loop community members
                new CommunityUser { CommunityId = 1, UserId = "user-001", IsModerator = true }, // Landon (creator & moderator)
                new CommunityUser { CommunityId = 1, UserId = "user-002", IsModerator = false }, // John
                new CommunityUser { CommunityId = 1, UserId = "user-003", IsModerator = false }, // Jane

                // Cornerstone community members
                new CommunityUser { CommunityId = 2, UserId = "user-001", IsModerator = false }, // Landon
                new CommunityUser { CommunityId = 2, UserId = "user-004", IsModerator = true }, // Bob (moderator)

                // Tower Grove South community members
                new CommunityUser { CommunityId = 3, UserId = "user-002", IsModerator = false }, // John
                new CommunityUser { CommunityId = 3, UserId = "user-003", IsModerator = true }, // Jane (creator & moderator)
                new CommunityUser { CommunityId = 3, UserId = "user-004", IsModerator = false }, // Bob

                // Inactive Group community members
                new CommunityUser { CommunityId = 4, UserId = "user-001", IsModerator = false }, // Landon
                new CommunityUser { CommunityId = 4, UserId = "user-005", IsModerator = true }, // Alice (moderator)

                // DnD Boys community members
                new CommunityUser { CommunityId = 5, UserId = "user-001", IsModerator = false }, // Landon
                new CommunityUser { CommunityId = 5, UserId = "user-002", IsModerator = true }, // John (moderator)
                new CommunityUser { CommunityId = 5, UserId = "user-004", IsModerator = false }  // Bob
            };

            context.CommunityUsers.AddRange(communityUsers);
            await context.SaveChangesAsync();
        }

        private static async Task SeedUserBooksAsync(ApplicationDbContext context)
        {
            if (await context.UserBooks.AnyAsync())
                return; // Database has been seeded

            var userBooks = new List<UserBook>
            {
                // Landon's books (user-001)
                new UserBook { UserId = "user-001", BookId = 1 }, // The Great Gatsby - SHARED
                new UserBook { UserId = "user-001", BookId = 4 }, // Wolf Hall
                new UserBook { UserId = "user-001", BookId = 8 }, // The Blade Itself - SHARED
                new UserBook { UserId = "user-001", BookId = 12 }, // The Left Hand of Darkness - SHARED

                // John's books (user-002)
                new UserBook { UserId = "user-002", BookId = 1 }, // The Great Gatsby - SHARED
                new UserBook { UserId = "user-002", BookId = 10 }, // Last Argument of Kings - SHARED
                new UserBook { UserId = "user-002", BookId = 9 }, // Before They Are Hanged
                new UserBook { UserId = "user-002", BookId = 16 }, // Assassin's Apprentice
                new UserBook { UserId = "user-002", BookId = 20 }, // Jonathan Strange & Mr Norrell

                // Jane's books (user-003)
                new UserBook { UserId = "user-003", BookId = 3 }, // 1984
                new UserBook { UserId = "user-003", BookId = 5 }, // Bring Up the Bodies
                new UserBook { UserId = "user-003", BookId = 8 }, // The Blade Itself - SHARED
                new UserBook { UserId = "user-003", BookId = 21 }, // Piranesi

                // Bob's books (user-004)
                new UserBook { UserId = "user-004", BookId = 6 }, // The Mirror & the Light
                new UserBook { UserId = "user-004", BookId = 10 }, // Last Argument of Kings
                new UserBook { UserId = "user-004", BookId = 12 }, // The Left Hand of Darkness - SHARED
                new UserBook { UserId = "user-004", BookId = 17 }, // Royal Assassin

                // Alice's books (user-005)
                new UserBook { UserId = "user-005", BookId = 7 }, // A Place of Greater Safety
                new UserBook { UserId = "user-005", BookId = 11 }, // Best Served Cold
                new UserBook { UserId = "user-005", BookId = 12 }, // The Left Hand of Darkness - SHARED
                new UserBook { UserId = "user-005", BookId = 22 }  // The Ladies of Grace Adieu
            };

            context.UserBooks.AddRange(userBooks);
            await context.SaveChangesAsync();
        }

        private static async Task SeedSharesAsync(ApplicationDbContext context)
        {
            if (await context.Shares.AnyAsync())
                return; // Database has been seeded

            var shares = new List<Share>
            {
                new Share
                {
                    UserBookId = 7,
                    Borrower = "user-001",
                    ReturnDate = null,
                    Status = ShareStatus.Ready
                },

                // Reputation test data: Jane (user-003) borrowing from John (user-002)
                // Two completed borrows and one disputed, so the reputation endpoint has
                // meaningful data on a fresh database.
                new Share
                {
                    UserBookId = 5,  // user-002's The Great Gatsby
                    Borrower = "user-003",
                    Status = ShareStatus.HomeSafe
                },
                new Share
                {
                    UserBookId = 6,  // user-002's Last Argument of Kings
                    Borrower = "user-003",
                    Status = ShareStatus.HomeSafe
                },
                new Share
                {
                    UserBookId = 7,  // user-002's Before They Are Hanged
                    Borrower = "user-003",
                    Status = ShareStatus.PickedUp,
                    IsDisputed = true,
                    DisputedBy = "user-002"
                }
            };

            context.Shares.AddRange(shares);
            await context.SaveChangesAsync();
        }
    }
}