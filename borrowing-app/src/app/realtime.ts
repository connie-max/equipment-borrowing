// realtime.ts
// One small job: stay connected to the API's SignalR hub and
// tell the pages when something changed.
//
// In plain words:
//   - The API is the radio station (see BorrowingHub.cs). This
//     service is the radio in the browser: it connects once and
//     listens for the station's announcements.
//   - The announcements have names: RequestCreated, RequestApproved,
//     RequestRejected, RequestReturned, ItemAdded, ItemChanged,
//     ItemDeleted. Every one of them means the same thing here:
//     "something changed, load the lists again".
//   - When one arrives, this service adds 1 to the tick signal.
//     The Items and Requests pages watch that number, and when it
//     changes they reload their lists from the API. That is how a
//     borrower's screen shows Approved the moment the admin clicks
//     Approve, with nobody pressing refresh.
//
// Why a signal and not a direct call:
//   - The service does not know which page is open. A signal is
//     the simple way to announce "something happened" and let
//     each open page decide to reload. (Week 2: signals tell
//     Angular something changed. Week 5: one publisher, many
//     subscribers, the Observer pattern.)

import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  // True when the live connection to the API is open.
  // The top bar shows "Live" when this is true.
  connected = signal(false);

  // A number that goes up by 1 every time a live event arrives.
  // The pages watch this number (see items.ts and requests.ts).
  tick = signal(0);

  // The connection to the hub. Built once, in the constructor.
  private connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5035/borrowingHub')
      .withAutomaticReconnect()
      .build();

    // Listen for every event name the API sends. Each one adds 1
    // to tick, which wakes up the pages that are watching it.
    const names = ['RequestCreated', 'RequestApproved', 'RequestRejected', 'RequestReturned', 'ItemAdded', 'ItemChanged', 'ItemDeleted'];
    for (const name of names) {
      this.connection.on(name, () => this.tick.update((n) => n + 1));
    }

    // If the connection drops, the green "Live" goes away.
    // withAutomaticReconnect tries again by itself, and when it
    // is back, "Live" comes back too.
    this.connection.onclose(() => this.connected.set(false));
    this.connection.onreconnected(() => this.connected.set(true));
  }

  // Open the connection. The App component calls this once when
  // the app starts. If the API is not running yet, try again in
  // 5 seconds, so starting the API after the app still works.
  start(): void {
    this.connection
      .start()
      .then(() => this.connected.set(true))
      .catch(() => setTimeout(() => this.start(), 5000));
  }
}
