# Equipment Borrowing — finished version (v7, AUREL design + Borrow page + API role checks + SignalR live updates)

A simple borrowing system: log in, borrow an item, an admin approves or
rejects it, then it is returned. Built in the simple style, so every file
has one small job and a comment at the top saying what it is.

Design v3 (22 Nov 2026): AUREL style — white background, big black bold
headings ("ENGINEERED TO BORROW"), orange accent buttons, black pill
buttons. Only the look changed; the login / borrow / approve / reject /
return logic is exactly the same as v2, and the Items and Requests pages
are still white cards, not tables.

**v3 fix (22 Nov 2026, same zip, re-made after the preview): the old
empty-Items bug is really fixed now.** The API always returned the 4
items, and the page had them in memory, but the screen stayed on
"Showing 0 items". The cause: in Angular 22 the page does not redraw
by itself after the API answers. The fix is simple and in two places:
`src/app/items/items.ts` and `src/app/requests/requests.ts` now keep
their list in a **signal** — a box that holds the list, and when the
list inside the box changes, Angular redraws the page by itself.
`main.ts` also imports `zone.js`, and `app.config.ts` adds
`provideZoneChangeDetection()`. If sir asks, say: "the list is a
signal, so when the API data arrives the page updates itself."

**v4 polish (22 Nov 2026): guards against the worst cases a panelist
will try, plus the small things.** The return date can no longer be
before the date needed, and the date needed cannot be in the past —
the date boxes will not even let you pick a past day, and the API
checks the dates again anyway, so a wrong request cannot sneak in
another way. Same idea everywhere: empty purpose, empty fields and
empty item name are all stopped with a clear message; one email can
only register once; the same item cannot get a second Pending request
while the first is still open; an admin cannot approve a request for
an item that is already borrowed; and an item with a Pending or
Approved request cannot be deleted. The top bar now shows which page
you are on (orange underline), and cards and buttons react on hover.
Tested here on 22 Nov 2026: 13 checks, all pass — the 3 bad-date /
empty-purpose / double-borrow requests are all rejected with a
400 and a plain message, the full Pending → Approved → Returned
flow works, and deleting a borrowed item is blocked until it is
returned.

**v5 Borrow page (22 Nov 2026): Borrow is now its own page.** The
borrow form used to sit at the bottom of the Items page, so clicking
Borrow looked like nothing happened until you scrolled all the way
down. Now Borrow goes to `/borrow/:id` — for example `/borrow/2`
for the Laptop — a page that shows just that item, the dates and
purpose, Send request, and Cancel. After sending, it takes you to
Requests so the new Pending request is visible. The date checks
moved to that page with it. The item and the message on that page
are signals too, for the same reason as the lists: a plain field
stayed blank after the API answered (found while testing v5, fixed
before delivery). Tested here on 22 Nov 2026 in a real browser:
the Borrow page loads the Laptop, a wrong return date shows
"Return date must be after the date needed.", and with correct
dates it sends the request and lands on Requests with the Laptop
request Pending.

**v6 API role checks (22 Nov 2026): the strict-panelist pass.** A
49-check battery was run against the API, trying everything a strict
panelist might try. It found 5 real holes, all fixed here:
1. Anyone could register as an Admin just by sending `"Admin"` — new
   accounts are now always Borrower; the one Admin comes from the seed data.
2. A borrower could approve their own request, or add/delete items,
   by calling the API directly — the buttons were hidden, the API
   was not checking. Now the frontend sends the logged-in user's id
   in the `X-User-Id` header, and the API looks that user up in the
   database and checks their role before approving, rejecting,
   adding, editing or deleting.
3. A borrower could make a request in another user's name — now you
   can only request as yourself, and you can only return your own
   request (or an admin can return any).
4. A borrower could read every other borrower's requests — now a
   borrower sees only their own; an admin sees all.
5. An item's status could be flipped back to Available with a direct
   edit while it was out on an approved request — now that edit is
   blocked until the request is returned.
After the fixes the same battery passed **49/49**. Honest limit, and
the defence answer says so: the header is not a login token. Anyone
who can call the API can still send any id in that header, so the
real fix later is a token (JWT). For a school demo this is the
simple middle step: it stops the panelist's demo attack, and it is
explained as what it is.

**v7 SignalR live updates (23 Nov 2026): the screens update by
themselves.** Before v7, a borrower's Requests page only changed
when they refreshed it. Now the API tells every open browser the
moment something changes, so no refresh is needed:
- Server: `BorrowingApi/Hubs/BorrowingHub.cs` is the hub — the
  "radio station". `Program.cs` turns SignalR on and maps the hub
  at `/borrowingHub`. After the controllers save a change, they
  send one named message to all connected browsers:
  `RequestCreated`, `RequestApproved`, `RequestRejected`,
  `RequestReturned` (from `BorrowRequestsController`) and
  `ItemAdded`, `ItemChanged`, `ItemDeleted` (from `ItemsController`).
- Browser: `borrowing-app/src/app/realtime.ts` is the one service
  that connects to the hub and listens. Each message adds 1 to a
  `tick` signal, and the Items and Requests pages watch that
  number — when it changes, they reload their lists from the API.
  The top bar shows a green "● Live" dot while the connection is
  open. If the connection drops, the dot goes away and the service
  reconnects by itself; the buttons still work either way, because
  the lists also reload after every click.
- One real bug found while testing v7: the SignalR client sends
  cookies with its requests by default, so the browser blocked
  every answer until the API's CORS rule gained
  `.AllowCredentials()` in `Program.cs`. The origin stays pinned
  to http://localhost:4200, so this is not open to everyone.
Tested here on 23 Nov 2026 in two real browsers at once: the
borrower sees "● Live", sends a request (Pending), the admin
approves it, and the borrower's page shows Approved — with the
Return button — without anyone pressing refresh. An item the
admin adds also appears on the borrower's Items page by itself.
The 49-check battery was re-run against v7: **49/49, no
regressions**. `npm install` the first time also installs the
SignalR client (`@microsoft/signalr`) from `package.json` — no
extra step.

## What is in here

- `BorrowingApi/` — ASP.NET Core API (.NET 10) + SQLite (`borrowing.db`,
  created on first run). 3 models (`User`, `Item`, `BorrowRequest`),
  3 controllers (`Auth`, `Items`, `BorrowRequests`), seed data,
  plus `Hubs/BorrowingHub.cs` (SignalR hub).
- `borrowing-app/` — Angular 22 app. 4 pages: Login, Items, Borrow,
  Requests, plus one service (`src/app/api.ts`) that does all the
  API calls, and one service (`src/app/realtime.ts`) that keeps the
  live SignalR connection.

## How to run it (two terminals, on your laptop)

Terminal 1 — the API:

```powershell
cd BorrowingApi
dotnet run
```

It listens on `http://localhost:5035`. On first run it creates the
database and adds the demo users and items.

Terminal 2 — the frontend:

```powershell
cd borrowing-app
npm install   # only the first time
npm start
```

Open `http://localhost:4200`.

## Demo accounts (seeded on first run)

| Who | Email | Password |
| --- | --- | --- |
| Admin | admin@school.ph | admin123 |
| Borrower | anthony@school.ph | borrower123 |

Demo items: Projector, Laptop, Speaker, Meeting Room — all Available.

## The demo to show in defence (2 minutes)

1. Log in as Anthony (borrower). On Items, click **Borrow** on the
   Projector — it opens the Borrow page for that item. Fill the
   dates and purpose → Send request. It takes you to Requests,
   where the request is now **Pending**.
2. Log out, log in as Admin. On Requests, the Projector request is
   Pending → **Approve**. The request becomes Approved and, on the
   Items page, the Projector is now **Borrowed** — nobody else can
   borrow it while it is out. **Live demo:** keep Anthony logged in
   in a second browser window (or a second browser) on Requests —
   the moment you click Approve, Anthony's page shows Approved with
   the Return button, with nobody pressing refresh, and the green
   "● Live" dot is in the top bar.
3. Still as Admin (or as Anthony), on Requests → **Return**. The request
   becomes Returned and the Projector is **Available** again.

## The flow to explain (the event chain)

```
Pending → Approved → Returned
Pending → Rejected
```

- Approve: request → Approved **and** item → Borrowed (two things
  change together, in `BorrowRequestsController.Approve`).
- Reject: request → Rejected, item stays Available.
- Return: request → Returned **and** item → Available again.
- A request can only be made for an Available item — that is what
  stops two people borrowing the same item at once
  (`BorrowRequestsController.Create`, step 2).

## How login works (simple, on purpose)

There is no token / JWT. Login checks the email and password and
returns the user (id, name, email, role). The frontend saves that
user in `localStorage` and reads it back to know who is logged in
and whether they are an Admin. Passwords are stored as SHA256
hashes, never as plain text (`Helpers/PasswordHelper.cs`) — that is
a school demo only; real apps use a slow hasher like BCrypt, and
you can say that if the panel asks about security.

## If something goes wrong

- Frontend shows nothing / CORS error: the API is not running.
  Start Terminal 1 first, then refresh `http://localhost:4200`.
- API port: it must be `http://localhost:5035`
  (`BorrowingApi/Properties/launchSettings.json`, profile "http").
  The frontend calls that address in `borrowing-app/src/app/api.ts`.
- Wrong data in the demo: stop the API, delete
  `BorrowingApi/borrowing.db`, start it again — the seed data comes
  back (2 users, 4 items).
