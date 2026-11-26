// BorrowingHub.cs
// The SignalR hub: the "radio station" of the system.
//
// What it is, in plain words:
//   - A hub is a place browsers connect to and stay connected to.
//   - The API can send a message to EVERY connected browser at
//     once, the moment something changes. No browser has to ask
//     again or press refresh.
//   - The browsers only listen. They never send messages to the hub.
//
// How it is used:
//   - BorrowRequestsController sends RequestCreated, RequestApproved,
//     RequestRejected and RequestReturned after it saves a change.
//   - ItemsController sends ItemAdded, ItemChanged and ItemDeleted.
//   - The Angular app (see realtime.ts) listens for those names
//     and reloads the lists when one arrives.
//
// The hub itself has no methods, because the browsers do not need
// to call it. The controllers do the talking, through
// IHubContext<BorrowingHub> (see Program.cs: AddSignalR).

using Microsoft.AspNetCore.SignalR;

namespace BorrowingApi.Hubs;

public class BorrowingHub : Hub
{
}
