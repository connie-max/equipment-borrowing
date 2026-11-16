using Microsoft.EntityFrameworkCore;
using BorrowingApi.Models;

namespace BorrowingApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<BorrowRequest> BorrowRequests => Set<BorrowRequest>();
}