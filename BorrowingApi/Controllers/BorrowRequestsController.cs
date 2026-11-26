// BorrowRequestsController.cs
// One small job: the borrow flow. This is the heart of the system.
//
// A request has a Status that only moves in this order:
//   Pending -> Approved -> Returned
//   Pending -> Rejected
//
// The three buttons in the frontend are the three events:
//   Approve:  Pending -> Approved,  AND the item becomes Borrowed.
//   Reject:   Pending -> Rejected,  the item stays Available.
//   Return:   Approved -> Returned, AND the item becomes Available again.
//
// That is the event chain to explain in defence:
//   "When the admin approves, two things change together:
//    the request becomes Approved and the item becomes Borrowed,
//    so nobody else can borrow the same item while it is out."
//
// Routes (all start with /api/requests):
//   GET  /api/requests            - list all requests (with user + item names)
//   GET  /api/requests/{id}       - get one request
//   POST /api/requests            - make a new request (starts as Pending)
//   POST /api/requests/{id}/approve - admin approves it
//   POST /api/requests/{id}/reject  - admin rejects it
//   POST /api/requests/{id}/return  - mark it returned

using BorrowingApi.Data;
using BorrowingApi.Hubs;
using BorrowingApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BorrowingApi.Controllers;

[ApiController]
[Route("api/requests")]
public class BorrowRequestsController : ControllerBase
{
    // The database. ASP.NET gives us this automatically (see Program.cs).
    private readonly AppDbContext _db;

    // The SignalR hub. ASP.NET gives us this too. After we save a
    // change, we use it to tell every connected browser at once,
    // so their screens update without a refresh.
    private readonly IHubContext<BorrowingHub> _hub;

    public BorrowRequestsController(AppDbContext db, IHubContext<BorrowingHub> hub)
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

    // --- What the frontend sends when making a new request ---
    public class CreateRequest
    {
        public int UserId { get; set; }
        public int ItemId { get; set; }
        public DateTime DateNeeded { get; set; }
        public DateTime ReturnDate { get; set; }
        public string Purpose { get; set; } = "";
    }

    // --- What we send back when listing requests ---
    // We add UserName and ItemName so the frontend does not need
    // a second call to find out who borrowed what.
    public class RequestReply
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public DateTime DateNeeded { get; set; }
        public DateTime ReturnDate { get; set; }
        public string Purpose { get; set; } = "";
        public string Status { get; set; } = "";
    }

    // Turn a BorrowRequest from the database into the reply above.
    private static RequestReply ToReply(BorrowRequest r)
    {
        return new RequestReply
        {
            Id = r.Id,
            UserId = r.UserId,
            UserName = r.User?.Name ?? "",
            ItemId = r.ItemId,
            ItemName = r.Item?.Name ?? "",
            DateNeeded = r.DateNeeded,
            ReturnDate = r.ReturnDate,
            Purpose = r.Purpose,
            Status = r.Status
        };
    }

    // GET /api/requests - list requests, newest first.
    // An admin sees all of them. A borrower sees only their own,
    // so one borrower cannot read another borrower's requests.
    // Include() loads the linked User and Item in the same query.
    [HttpGet]
    public async Task<ActionResult<List<RequestReply>>> GetAll()
    {
        var caller = await GetCaller();
        if (caller == null)
        {
            return Unauthorized("Log in first.");
        }

        var query = _db.BorrowRequests
            .Include(r => r.User)
            .Include(r => r.Item)
            .OrderByDescending(r => r.Id);

        // A borrower only gets their own rows. An admin gets all.
        var requests = caller.Role == "Admin"
            ? await query.ToListAsync()
            : await query.Where(r => r.UserId == caller.Id).ToListAsync();

        // Turn each one into a reply with names included.
        return requests.Select(ToReply).ToList();
    }

    // GET /api/requests/3 - get one request, or 404.
    // A borrower may only open their own request.
    [HttpGet("{id}")]
    public async Task<ActionResult<RequestReply>> GetOne(int id)
    {
        var caller = await GetCaller();
        if (caller == null)
        {
            return Unauthorized("Log in first.");
        }

        var request = await _db.BorrowRequests
            .Include(r => r.User)
            .Include(r => r.Item)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return NotFound();
        }
        if (caller.Role != "Admin" && request.UserId != caller.Id)
        {
            return StatusCode(403, "That is not your request.");
        }
        return ToReply(request);
    }

    // POST /api/requests - make a new request. It always starts as Pending.
    [HttpPost]
    public async Task<ActionResult<RequestReply>> Create(CreateRequest body)
    {
        // Step 0: you can only request as yourself. Without this,
        // anyone could make a request in another user's name just
        // by putting their id in the body.
        var caller = await GetCaller();
        if (caller == null)
        {
            return Unauthorized("Log in first.");
        }
        if (caller.Id != body.UserId)
        {
            return StatusCode(403, "You can only request as yourself.");
        }

        // Step 1: the item must exist.
        var item = await _db.Items.FindAsync(body.ItemId);
        if (item == null)
        {
            return BadRequest("That item does not exist.");
        }

        // Step 2: the item must be Available. If it is Borrowed, stop here.
        // This is what stops two people borrowing the same item at once.
        if (item.Status != "Available")
        {
            return BadRequest("That item is already borrowed.");
        }

        // Step 3: the user must exist.
        var user = await _db.Users.FindAsync(body.UserId);
        if (user == null)
        {
            return BadRequest("That user does not exist.");
        }

        // Step 4: the dates must make sense. This is the check the
        // panelist will try to break: a return date before the date
        // needed, or a date needed in the past, is not allowed.
        // We only compare the date part, not the time.
        if (string.IsNullOrWhiteSpace(body.Purpose))
        {
            return BadRequest("Purpose is required.");
        }
        if (body.DateNeeded.Date < DateTime.Today)
        {
            return BadRequest("Date needed cannot be in the past.");
        }
        if (body.ReturnDate.Date <= body.DateNeeded.Date)
        {
            return BadRequest("Return date must be after the date needed.");
        }

        // Step 5: no double borrow. If this item already has a
        // Pending or Approved request, stop here — even if the
        // item still says Available (it does until approval).
        bool alreadyRequested = await _db.BorrowRequests.AnyAsync(r =>
            r.ItemId == body.ItemId && (r.Status == "Pending" || r.Status == "Approved"));
        if (alreadyRequested)
        {
            return BadRequest("That item already has a pending or approved request.");
        }

        // Step 6: save the new request as Pending.
        // Note: the item stays Available until an admin approves it.
        var request = new BorrowRequest
        {
            UserId = body.UserId,
            ItemId = body.ItemId,
            DateNeeded = body.DateNeeded,
            ReturnDate = body.ReturnDate,
            Purpose = body.Purpose,
            Status = "Pending"
        };

        _db.BorrowRequests.Add(request);
        await _db.SaveChangesAsync();

        // Tell every connected browser: a new request was made.
        // Their Requests and Items pages reload by themselves.
        await _hub.Clients.All.SendAsync("RequestCreated", request.Id);

        // Load the names back so the reply has them.
        request.User = user;
        request.Item = item;
        return CreatedAtAction(nameof(GetOne), new { id = request.Id }, ToReply(request));
    }

    // Find a request by id, or return null. Used by approve / reject / return.
    private async Task<BorrowRequest?> FindRequest(int id)
    {
        return await _db.BorrowRequests
            .Include(r => r.User)
            .Include(r => r.Item)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    // POST /api/requests/3/approve
    // Only a Pending request can be approved. Admin only:
    // hiding the button from borrowers is not enough, because
    // anyone can call the API directly.
    // Two things change together: request -> Approved, item -> Borrowed.
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<RequestReply>> Approve(int id)
    {
        var blocked = await MustBeAdmin();
        if (blocked != null) return blocked;

        var request = await FindRequest(id);
        if (request == null)
        {
            return NotFound();
        }
        if (request.Status != "Pending")
        {
            return BadRequest("Only a Pending request can be approved.");
        }

        // The item must still be Available. If another request for
        // the same item was approved first, this one cannot be.
        if (request.Item!.Status != "Available")
        {
            return BadRequest("That item is already borrowed.");
        }

        request.Status = "Approved";
        // The item is now out, so nobody else can borrow it.
        request.Item!.Status = "Borrowed";

        await _db.SaveChangesAsync();

        // Tell every connected browser: this request was approved.
        // The borrower's Requests page shows Approved, and the item
        // shows Borrowed, without anyone pressing refresh.
        await _hub.Clients.All.SendAsync("RequestApproved", request.Id);
        return ToReply(request);
    }

    // POST /api/requests/3/reject
    // Only a Pending request can be rejected. Admin only,
    // for the same reason as approve.
    // The item stays Available, because it was never borrowed.
    [HttpPost("{id}/reject")]
    public async Task<ActionResult<RequestReply>> Reject(int id)
    {
        var blocked = await MustBeAdmin();
        if (blocked != null) return blocked;

        var request = await FindRequest(id);
        if (request == null)
        {
            return NotFound();
        }
        if (request.Status != "Pending")
        {
            return BadRequest("Only a Pending request can be rejected.");
        }

        request.Status = "Rejected";
        await _db.SaveChangesAsync();

        // Tell every connected browser: this request was rejected.
        await _hub.Clients.All.SendAsync("RequestRejected", request.Id);
        return ToReply(request);
    }

    // POST /api/requests/3/return
    // Only an Approved request can be returned.
    // Only the borrower who made the request, or an admin, may
    // return it — nobody may return someone else's request.
    // Two things change together: request -> Returned, item -> Available.
    [HttpPost("{id}/return")]
    public async Task<ActionResult<RequestReply>> Return(int id)
    {
        var caller = await GetCaller();
        if (caller == null)
        {
            return Unauthorized("Log in first.");
        }

        var request = await FindRequest(id);
        if (request == null)
        {
            return NotFound();
        }
        if (caller.Role != "Admin" && request.UserId != caller.Id)
        {
            return StatusCode(403, "That is not your request.");
        }
        if (request.Status != "Approved")
        {
            return BadRequest("Only an Approved request can be returned.");
        }

        request.Status = "Returned";
        // The item is back, so it can be borrowed again.
        request.Item!.Status = "Available";

        await _db.SaveChangesAsync();

        // Tell every connected browser: this request was returned.
        await _hub.Clients.All.SendAsync("RequestReturned", request.Id);
        return ToReply(request);
    }
}
