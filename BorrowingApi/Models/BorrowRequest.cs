namespace BorrowingApi.Models;

public class BorrowRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public DateTime DateNeeded { get; set; }
    public DateTime ReturnDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Returned
}