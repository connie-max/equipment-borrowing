# Defence Notes — Equipment Borrowing (simple version)

Say this first, in your own words: "It is a simple borrowing system.
A borrower asks for an item, an admin approves or rejects it, and
when it comes back the item is available again."

## The 2-minute demo

1. Log in as Anthony, the borrower: anthony@school.ph / borrower123.
   On Items, click Borrow on the Projector. It opens the Borrow
   page for that item — fill in the dates and purpose, send.
   It takes you to Requests, where the request is now Pending.
2. Log out, log in as Admin: admin@school.ph / admin123.
   On Requests, Approve the Projector request. It becomes Approved
   and the Projector becomes Borrowed, so nobody else can take it.
   **Live demo (v7):** keep Anthony logged in in a second browser
   window on Requests. The moment you click Approve, Anthony's page
   shows Approved with the Return button — nobody pressed refresh.
   The green "● Live" dot in the top bar means the live connection
   is open.
3. On Requests, press Return. The request becomes Returned and the
   Projector is Available again.

## The flow to explain (the event chain)

```
Pending -> Approved -> Returned
Pending -> Rejected
```

- Approve: request becomes Approved AND item becomes Borrowed.
  Two things change together, in `BorrowRequestsController.Approve`.
- Reject: request becomes Rejected, item stays Available.
- Return: request becomes Returned AND item becomes Available again.
- A request can only be made for an Available item. That check is
  in `BorrowRequestsController.Create`, step 2, and it is what stops
  two people borrowing the same item at once.

## What each file does

Backend — `BorrowingApi/` (.NET 10, C#, SQLite)
- `Program.cs` — starts the app, allows the Angular app to call it
  (CORS for http://localhost:4200, credentials allowed for
  SignalR), creates the database and seeds it on first run, and
  turns on SignalR with the hub at `/borrowingHub`.
- `Hubs/BorrowingHub.cs` — the SignalR hub, the "radio station".
  Browsers connect to it and stay connected. It has no methods of
  its own; the controllers send the messages through it.
- `Models/User.cs`, `Item.cs`, `BorrowRequest.cs` — the 3 tables.
  User has a Role (Admin or Borrower). Item has a Status (Available
  or Borrowed). BorrowRequest has a Status (Pending, Approved,
  Rejected, Returned).
- `Data/AppDbContext.cs` — the database connection, one DbSet per table.
- `Data/SeedData.cs` — adds 2 demo users and 4 demo items, only if
  the database is empty.
- `Helpers/PasswordHelper.cs` — SHA256 hash for passwords. Simple
  school demo only, not production security.
- `Controllers/AuthController.cs` — register and login. Login checks
  email + password and returns the user. No token / JWT on purpose.
- `Controllers/ItemsController.cs` — list, get one, add, edit, delete items.
  After saving, it sends `ItemAdded`, `ItemChanged` or `ItemDeleted`
  to every connected browser.
- `Controllers/BorrowRequestsController.cs` — the heart of the system:
  make a request, approve, reject, return. After saving, it sends
  `RequestCreated`, `RequestApproved`, `RequestRejected` or
  `RequestReturned` to every connected browser.

Frontend — `borrowing-app/` (Angular 22)
- `src/app/api.ts` — the only file that talks to the API. Every page
  uses this one service. The API address is in one place here.
- `src/app/realtime.ts` — the live SignalR connection. It connects
  once when the app starts, listens for the 7 event names above,
  and adds 1 to a `tick` signal each time one arrives. The Items
  and Requests pages watch that number and reload their lists when
  it changes. It also shows if the connection is open (`connected`
  signal — the green "● Live" in the top bar) and reconnects by
  itself if it drops.
- `src/app/login/` — login page, saves the logged-in user in localStorage.
- `src/app/items/` — items list + add item + delete item.
  Borrow goes to its own page now, it is not a form at the
  bottom of this page any more.
- `src/app/borrow/` — the Borrow page: loads one item by its id
  from the address (`/borrow/2`), checks the dates, sends the
  request, then goes to Requests.
- `src/app/requests/` — requests list, Approve / Reject / Return buttons.
  Approve and Reject only show for Admin.
- `src/app/app.routes.ts` — the 4 routes: login, items, borrow/:id,
  requests.

## Likely defence questions (short answers)

1. **What does the system do?** A borrower requests an item, an
   admin approves or rejects it, and the item is marked returned
   when it comes back.
2. **How do you stop two people borrowing the same item?** Create
   only allows Available items, and Approve changes the item to
   Borrowed at the same time. On top of that, an item cannot get a
   second Pending request while the first is still open, and Approve
   checks the item is still Available before approving.
3. **What happens on Approve?** Request Pending -> Approved, item
   Available -> Borrowed, in one save.
4. **What happens on Return?** Request Approved -> Returned, item
   Borrowed -> Available.
5. **Why is there no JWT / token?** To keep it simple and explainable.
   Login returns the user, the frontend stores it in localStorage
   and checks the role before showing admin buttons.
6. **How are passwords stored?** As SHA256 hashes, never plain text.
   That is a school demo — a real app would use a slow hasher like
   BCrypt.
7. **Where is the data?** SQLite file `borrowing.db`, created on first
   run. Delete it and restart the API to get the seed data back.
8. **How does the frontend talk to the backend?** HTTP calls from
   `api.ts` to http://localhost:5035/api/..., allowed by CORS
   in Program.cs.
9. **What is the database?** 3 tables: Users, Items, BorrowRequests.
   A request links a User and an Item by their Ids.
10. **What would you add next?** Real roles checked on the server
    (not just in the frontend), and a due-date / overdue list.
    Saying this shows you know the limits of the simple version.
11. **What if I put the return date before the date needed?** The
    form stops you with the message "Return date must be after the
    date needed", and the date boxes will not even let you pick a
    past day. The API checks the dates again too, so a wrong
    request cannot sneak in another way — it answers 400 with the
    same message.
12. **What if I request the same item twice?** The second request
    is rejected: "That item already has a pending or approved
    request." The check is in `BorrowRequestsController.Create`,
    step 5.
13. **What if I delete an item that is borrowed?** The API refuses:
    "That item has a pending or approved request and cannot be
    deleted." Return or reject those requests first.
14. **What if I register with an email that is already used?** The
    API refuses: "That email is already registered." One email is
    one account.
15. **Why did the Items page show 0 items before, when the API
    sent 4?** In Angular 22 the page does not redraw by itself
    after the API answers. The lists are now signals, so when the
    data arrives the page updates itself. That is in `items.ts`
    and `requests.ts`.
16. **Why does Borrow open a new page instead of a form on the
    Items page?** The form used to sit at the bottom of the Items
    page, so clicking Borrow looked like nothing happened until
    you scrolled down. Its own page (`/borrow/:id`) shows just
    that item and the form, and after sending it takes you to
    Requests to see the new Pending request. One page, one job.
17. **What stops a borrower approving their own request?** The
    buttons are hidden from borrowers, but that is not the real
    check — anyone can call the API directly. The API checks too:
    the frontend sends the logged-in user's id in the `X-User-Id`
    header, the API looks that user up in the database, and only
    an Admin may approve or reject. A borrower calling approve
    gets 403 "Only an admin can do that."
18. **Can anyone register as an Admin?** No. New accounts are
    always Borrower — the role the register form sends is ignored.
    The one Admin account comes from the seed data.
19. **Can a borrower see other borrowers' requests?** No. A
    borrower sees only their own requests; an admin sees all of
    them. You can also only return your own request.
20. **Is the `X-User-Id` header real security?** Honestly, no —
    it is not a login token. Anyone who can call the API can still
    send any id in that header. It stops the panelist's demo
    attack (calling approve as a borrower) and it is simple to
    explain. The real fix later is a login token (JWT), and the
    code says so in the comments.
21. **What is SignalR?** Real-time: the server can push a message
    to the browsers the moment something happens, instead of the
    browsers asking again and again. That is the Week 9 topic of
    the course. In our system it means the borrower's screen
    updates itself when the admin approves — no refresh, no
    waiting.
22. **How does the live update work, step by step?** The admin
    clicks Approve. The API saves the change, then sends one
    message called `RequestApproved` to every browser connected
    to the hub (`BorrowingHub`). In the browser, `realtime.ts`
    hears it and adds 1 to a `tick` signal. The Requests page
    watches that number, and when it changes it loads the
    requests again from the API. So the page shows Approved.
    One message, every open screen updates.
23. **What is the hub?** The meeting point. Browsers connect to
    it once and stay connected, like joining a radio station.
    The API is the station: after saving a change, the controller
    sends the message through the hub to all listeners. The hub
    file itself is almost empty — it only has to exist so the
    controllers have somewhere to send from. The hub lives at
    `/borrowingHub` (see `Program.cs`).
24. **What if the live connection drops?** The green "● Live" in
    the top bar goes away, and the service tries to reconnect by
    itself (`withAutomaticReconnect`). Nothing breaks: the lists
    still reload after every button you click, because every page
    still loads from the API the normal way. Live updates are a
    bonus on top of normal HTTP, not a replacement for it.
25. **Did anything go wrong while adding SignalR?** Yes, one real
    bug. The SignalR client sends cookies with its requests by
    default, so the browser blocked every answer until the API's
    CORS rule gained `.AllowCredentials()` in `Program.cs`. The
    origin stays pinned to http://localhost:4200, so it is not
    open to everyone. The fix is one line, and it is in the code
    comments.
