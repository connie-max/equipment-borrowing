namespace BorrowingApi.Models;

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Equipment"; // "Equipment" or "Room"
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Available"; // "Available" or "Borrowed"
}