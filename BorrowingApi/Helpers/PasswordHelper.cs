// PasswordHelper.cs
// One small job: turn a plain password into a hash (a scrambled text)
// so we never save the real password in the database.
//
// How it works, in plain words:
//   1. Take the password text.
//   2. Run SHA256 on it. SHA256 always turns the same text into the
//      same long scrambled string, and you cannot turn it back.
//   3. Save that scrambled string in User.PasswordHash.
//
// To check a login, we hash the password the user typed and compare
// the two scrambled strings. If they match, the password was right.
//
// WARNING FOR DEFENCE: this is a school demo only. Real apps use a
// slow password hasher (like BCrypt) with a salt. SHA256 alone is
// NOT safe for real users and real passwords. Say that out loud
// if the panel asks about security.

using System.Security.Cryptography;
using System.Text;

namespace BorrowingApi.Helpers;

public static class PasswordHelper
{
    // Turn a plain password into a SHA256 hash (a hex string).
    public static string Hash(string password)
    {
        // Turn the text into bytes, hash the bytes, turn the result back into text.
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    // Check a typed password against a saved hash.
    // We hash the typed password and see if it matches the saved one.
    public static bool Check(string password, string savedHash)
    {
        return Hash(password) == savedHash;
    }
}
