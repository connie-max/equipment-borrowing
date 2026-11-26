// ItemsController.cs
// One small job: the list of things that can be borrowed.
//
// Routes (all start with /api/items):
//   GET    /api/items       - list all items
//   GET    /api/items/{id}  - get one item
//   POST   /api/items       - add a new item
//   PUT    /api/items/{id}  - change an item
//   DELETE /api/items/{id}  - delete an item
//
// This controller talks to the database directly (DbContext).
// No repository, no service layer - that is on purpose, so the
// code stays short and easy to explain in defence.
// Each item has a Status: "Available" or "Borrowed".
// That status is changed by BorrowRequestsController, not here.

using BorrowingApi.Data;
using BorrowingApi.Hubs;
using BorrowingApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BorrowingApi.Controllers;

[ApiController]
[Route("api/items")]
public class ItemsController : ControllerBase
{
    // The database. ASP.NET gives us this automatically (see Program.cs).
    private readonly AppDbContext _db;

    // The SignalR hub (see BorrowRequestsController for how it works).
    private readonly IHubContext<BorrowingHub> _hub;

    public ItemsController(AppDbContext db, IHubContext<BorrowingHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // Who is calling? The frontend sends the logged-in user's id
    // in the X-User-Id header. We look that user up in the
    // database, so the role comes from the database, not from
    // what the caller claims. Returns null when nobody is logged in.
    private async Task<User?> GetCaller()
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var value)) return null;
        if (!int.TryParse(value, out int id)) return null;
        return await _db.Users.FindAsync(id);
    }

    // Only an admin may do this. Returns null when it is allowed,
    // or the error to send back when it is not.
    private async Task<ActionResult?> MustBeAdmin()
    {
        var caller = await GetCaller();
        if (caller == null) return Unauthorized("Log in first.");
        if (caller.Role != "Admin") return StatusCode(403, "Only an admin can do that.");
        return null;
    }

    // GET /api/items - return every item, in Id order.
    [HttpGet]
    public async Task<ActionResult<List<Item>>> GetAll()
    {
        return await _db.Items.OrderBy(i => i.Id).ToListAsync();
    }

    // GET /api/items/3 - return one item, or 404 if it does not exist.
    [HttpGet("{id}")]
    public async Task<ActionResult<Item>> GetOne(int id)
    {
        var item = await _db.Items.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        return item;
    }

    // POST /api/items - add a new item. Admin only.
    // The frontend sends Name, Type, Description. Status starts as Available.
    [HttpPost]
    public async Task<ActionResult<Item>> Create(Item item)
    {
        var blocked = await MustBeAdmin();
        if (blocked != null) return blocked;

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return BadRequest("Item name is required.");
        }

        // New items are always Available at first.
        item.Status = "Available";

        _db.Items.Add(item);
        await _db.SaveChangesAsync();

        // Tell every connected browser: a new item was added.
        await _hub.Clients.All.SendAsync("ItemAdded", item.Id);

        // 201 Created + the new item (now with its Id).
        return CreatedAtAction(nameof(GetOne), new { id = item.Id }, item);
    }

    // PUT /api/items/3 - replace an item's details. Admin only.
    // The id in the address must match the id in the body.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Item updated)
    {
        var blocked = await MustBeAdmin();
        if (blocked != null) return blocked;

        if (id != updated.Id)
        {
            return BadRequest("Id in the address does not match the item.");
        }

        // Status can only be Available or Borrowed. Anything else
        // (for example a typo) is rejected instead of saved.
        if (updated.Status != "Available" && updated.Status != "Borrowed")
        {
            return BadRequest("Status must be Available or Borrowed.");
        }

        // The item's status belongs to the borrow flow, not to this
        // edit form: while an Approved request has the item out,
        // nobody may flip it back to Available here. That would
        // free the item without returning the request. Return it
        // first, then the status changes by itself.
        var current = await _db.Items.FindAsync(id);
        if (current != null && current.Status != updated.Status)
        {
            bool isOut = await _db.BorrowRequests.AnyAsync(r =>
                r.ItemId == id && r.Status == "Approved");
            if (isOut)
            {
                return BadRequest("That item is out on an approved request. Return it first.");
            }
        }

        // Tell the database: this item has changed, save it.
        _db.Entry(updated).State = EntityState.Modified;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // If the item was deleted in the meantime, say 404.
            if (!await _db.Items.AnyAsync(i => i.Id == id))
            {
                return NotFound();
            }
            throw;
        }

        // Tell every connected browser: this item changed.
        await _hub.Clients.All.SendAsync("ItemChanged", id);

        return NoContent(); // 204 = saved OK, nothing to send back.
    }

    // DELETE /api/items/3 - delete an item. Admin only.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var blocked = await MustBeAdmin();
        if (blocked != null) return blocked;

        var item = await _db.Items.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        // Do not delete an item that is out or waiting: a Pending
        // or Approved request still needs it. Reject or return
        // those requests first, then delete.
        bool hasActiveRequest = await _db.BorrowRequests.AnyAsync(r =>
            r.ItemId == id && (r.Status == "Pending" || r.Status == "Approved"));
        if (hasActiveRequest)
        {
            return BadRequest("That item has a pending or approved request and cannot be deleted.");
        }

        _db.Items.Remove(item);
        await _db.SaveChangesAsync();

        // Tell every connected browser: this item was deleted.
        await _hub.Clients.All.SendAsync("ItemDeleted", id);
        return NoContent();
    }
}
