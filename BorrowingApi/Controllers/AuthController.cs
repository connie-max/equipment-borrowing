// AuthController.cs
// One small job: register a new user, and log a user in.
//
// There is NO token / JWT here on purpose, to keep it simple:
//   - Login checks the email and password and returns the user
//     (id, name, email, role) as plain JSON.
//   - The Angular frontend saves that user in localStorage.
//   - The frontend reads it back to know who is logged in and
//     whether they are an Admin or a Borrower.
//   - The frontend also sends that user's id in the X-User-Id
//     header on every other call, so the other controllers can
//     check the role in the database before doing admin-only work.
//     That stops a borrower approving their own request just by
//     calling the API. It is NOT a token: anyone who can call the
//     API can still send any id in that header, so the real fix
//     later is a login token (JWT). For a school demo this is
//     the simple middle step, and the defence answer says so.
//
// Routes (all start with /api/auth):
//   POST /api/auth/register  - make a new user (Borrower by default)
//   POST /api/auth/login     - check email + password, return the user

using BorrowingApi.Data;
using BorrowingApi.Helpers;
using BorrowingApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BorrowingApi.Controllers;

// This tells ASP.NET: this is a web API controller, and its address starts with /api/auth.
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // The database. ASP.NET gives us this automatically (see Program.cs).
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    // --- What the frontend sends when someone registers ---
    // A small box that only carries the fields we need. No Id, no PasswordHash.
    public class RegisterRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "Borrower"; // "Borrower" unless the form says "Admin"
    }

    // --- What the frontend sends when someone logs in ---
    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    // --- What we send back after register / login ---
    // Only safe fields. Never send the password hash back.
    public class UserReply
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }

    // Turn a User from the database into the safe reply above.
    private static UserReply ToReply(User user)
    {
        return new UserReply
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role
        };
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<UserReply>> Register(RegisterRequest request)
    {
        // Basic check: name, email and password must not be empty.
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Name, email and password are required.");
        }

        // One email = one account. If the email is already used, say so.
        bool emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailTaken)
        {
            return BadRequest("That email is already registered.");
        }

        // New accounts are ALWAYS Borrower. The role the form sends
        // is ignored on purpose: if we trusted it, anyone could
        // register as an Admin just by sending "Admin".
        // The one Admin account comes from the seed data, not from here.
        string role = "Borrower";

        // Make the new user. Save the HASH of the password, not the password itself.
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = PasswordHelper.Hash(request.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 201 Created + the new user (safe fields only).
        return CreatedAtAction(nameof(Register), ToReply(user));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<UserReply>> Login(LoginRequest request)
    {
        // Step 1: find the user by email. If there is none, the login fails.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return Unauthorized("Wrong email or password.");
        }

        // Step 2: hash the typed password and compare it with the saved hash.
        if (!PasswordHelper.Check(request.Password, user.PasswordHash))
        {
            return Unauthorized("Wrong email or password.");
        }

        // Step 3: password was right. Return the user (safe fields only).
        // The frontend saves this in localStorage.
        return Ok(ToReply(user));
    }
}
