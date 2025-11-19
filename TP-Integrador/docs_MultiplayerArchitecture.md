# Tetris VS Multiplayer Architecture & Network Topology

## 1. Overview

This document describes the architecture, network topology, message protocol, concurrency model, and extension points of the Unity-based socket multiplayer Tetris VS implementation found in `TP-Integrador/TetrisVS`.

The game uses a custom TCP line-based JSON protocol over `TcpListener` / `TcpClient` to synchronize:
- Game state (running / waiting / paused / playing)
- Piece queue (deterministic sequence with a seed)
- Board state snapshots (locked tiles + active piece)
- Player actions (movement, rotation, drops)
- Versus mechanics placeholders (garbage lines)

## 2. High-Level Topology

```
+-----------------+         +------------------+
|   Host (Server) |         |  Client Player   |
|  Unity Instance |         |  Unity Instance  |
|-----------------|         |------------------|
| MultiplayerMgr  |<--- Game State / Queue -->| MultiplayerMgr   |
|  (role=Server)  |         |  (role=Client)   |
|   |     |         |   |    |
|  TcpServer  |<-- TCP Socket (lines) --> |  TcpClientPeer   |
|   |     |         |   |    |
| AcceptLoop Thr  |         | ReceiveLoop Thr  |
| ClientConnection|         |      |
| RemoteBoardView |<-- BoardStateMessage ---->| BoardMultiplayer |
| Local Board   |        /| Adapter + Board  |
| (Authoritative) |       / | Local Board  |
+-----------------+      /  +------------------+
             /
         (ThreadDispatcher marshals to Unity main thread)
```

## 3. Core Components

| Component | Responsibility | Thread Context |
|-----------|----------------|----------------|
| `TcpServer` | Listen, accept, broadcast messages | AcceptLoop thread + main thread coordination |
| `ClientConnection` | Per-client reader/writer on server | Per-connection recv thread |
| `TcpClientPeer` | Client socket peer, sends handshake, reads messages | Client ReceiveLoop thread |
| `MultiplayerManager` | Orchestrates roles, state, broadcasts, lifecycle | Unity main thread (handlers enqueued) |
| `Board` | Core Tetris logic (spawn, move, lock, clear, queue) | Unity main thread |
| `BoardMultiplayerAdapter` | Serializes / deserializes board & queue state | Unity main thread |
| `RemoteBoardView` | Renders opponent board state | Unity main thread |
| `NetMessageFactory` | JSON envelope wrap/unwrap | Any (pure data) |
| `ThreadDispatcher` | Marshals network thread actions back to Unity | Unity main thread executes queue |

## 4. Message Protocol

All messages are newline-delimited JSON envelopes:

```
{
  "type": "<string>",
  "payload": "<json-stringified-object>"
}
```

Supported payload types (from `NetworkMessages.cs`):

| Envelope `type` | Payload Class | Purpose |
|-----------------|---------------|---------|
| `handshake` | `HandshakeMessage` | Identifies role (`client`) to server |
| `board_state` | `BoardStateMessage` | Full snapshot: dimensions, locked tiles, active piece, queue preview |
| `player_action` | `PlayerActionMessage` | Input intent (move, rotate, drop, etc.) |
| `game_state_update` or `game_state` | `GameStateUpdateMessage` / `GameStateMessage` | High-level game state synchronization |
| `queue_sync` | `QueueSyncMessage` | Deterministic upcoming shapes + RNG seed |
| `garbage` | `GarbageMessage` (placeholder) | Versus mechanic (inject lines) |

Utility:
- `NetMessageFactory.Wrap(type, obj)` returns envelope + newline.
- `NetMessageFactory.TryUnwrap(raw, out envelope)` parses safely.

## 5. Lifecycle Flows

### Hosting

```
MultiplayerManager.HostGame()
  -> new TcpServer(port).Start()
  -> AcceptLoop thread begins
  -> Server awaits client handshakes
  -> InitializeServerQueue()
  -> (If already initialized) InitializeGameState()
```

### Client Joining

```
MultiplayerManager.JoinGame(ip)
  -> new TcpClientPeer(ip, port).Connect()
  -> Establish TCP socket, start ReceiveLoop thread
  -> Send handshake { role = "client" }
  -> If game already initialized on client:
   UpdateGameState(Playing) & ResumeGame()
  -> Optionally send immediate board snapshot (if configured)
```

### Handshake Handling (Server)

On receiving `handshake` with `role="client"`:
- BroadcastQueueUpdate()
- BroadcastGameState()
- Send immediate board_state snapshot (if implemented)

### Player Action Propagation

```
Client Input -> Create PlayerActionMessage -> Wrap & Send
Server recv thread -> HandleIncomingServerSide()
  -> case "player_action": apply or schedule action
  -> Possibly recompute board state
  -> Broadcast updated board_state to all clients
```

### Queue Sync

Server authoritative queue:
- Created via `SharedShapesQueue` (seed stored in `QueueSyncMessage`)
- Broadcast on start / every spawn triggering update
Client:
- `Board.SynchronizeQueue(queueState)` ensures same RNG sequence.

### Board State Snapshot

Server:
- Serialize live board (locked cells, active piece offsets, queue preview).
- Broadcast as `board_state`.
Client:
- ReceiveLoop -> ThreadDispatcher -> `RemoteBoardView.ApplyBoardState(boardState)`.

### Game State Update

Server calls `BroadcastGameState()` on state transitions (start, pause, resume).
Clients update UI / internal state accordingly.

### Disconnection

Client:
- Socket close -> ReceiveLoop ends -> OnDisconnected -> MultiplayerManager resets role/state.
Server:
- ClientConnection thread EOF -> removal from `_clients` -> OnClientDisconnected -> possibly pause game if requireClientToStart.

## 6. Concurrency & Thread Safety

| Thread | Work | Synchronization |
|--------|------|-----------------|
| Unity Main | Game objects, UI, board logic | Receives enqueued actions from dispatcher |
| Server AcceptLoop | Accepts sockets | Guard `_clients` list with lock |
| Server ClientConnection | Reads lines -> OnRawMessage | Marshals Unity-affecting operations via dispatcher |
| Client ReceiveLoop | Reads server lines -> OnRawMessage | Uses dispatcher to modify GameObjects |

Rules:
- Never mutate Unity `GameObject` or `MonoBehaviour` from background threads.
- Use a dispatcher pattern (e.g., `ThreadDispatcher.Instance.Enqueue(Action)`).

## 7. Board & Queue Mechanics (Summary)

`Board`:
- Initializes `TetrisBlocks[]` with cell patterns pulled from data.
- Maintains a `SharedShapesQueue` object (seed-based deterministic piece order).
- `SpawnPiece()`:
  - Fetch shape index from queue.
  - Validate position; if invalid => `GameOver()`.
  - Sets tiles for piece and notifies `MultiplayerManager` (server side) to broadcast updated queue.
- `ClearLines()`:
  - Detects full rows, clears them, updates score & level.
  - Calls `BoardMultiplayerAdapter.NotifyLinesCleared(clearedLines)` (inferred) for versus effects (e.g., sending garbage).
- `SynchronizeQueue(QueueStateMessage)` aligns local queue to server.

## 8. Event Routing (Server-Side Example)

Pseudo-dispatch (simplified from `MultiplayerManager` logic):

```csharp
switch (envelope.type)
{
  case "handshake":
   // Initialize sync (queue + game state)
   BroadcastQueueUpdate();
   BroadcastGameState();
   break;

  case "board_state":
   // Possibly ignored on server if server is authoritative
   break;

  case "player_action":
   // Apply action -> update board -> broadcast board_state
   break;

  case "queue_sync":
   // Usually server -> clients; server seldom ingests this
   break;

  case "game_state_update":
   // Might validate timestamps or origin
   break;

  case "garbage":
   // Adjust board (inject lines), re-broadcast board_state
   break;
}
```

## 9. Extension Points

| Feature | How to Implement | Benefit |
|---------|------------------|---------|
| Garbage Lines | On ClearLines() compute lines to send -> Broadcast GarbageMessage -> Client applies | Adds versus pressure |
| Spectator Mode | New handshake role "spectator"; skip input sending | Expand audience |
| Heartbeats | Add `ping` / `pong` messages periodically | Faster stale connection detection |
| Compression | GZip large `board_state` payload | Bandwidth reduction |
| Action Rate Limiting | Track timestamps before sending `player_action` | Prevent abuse / flooding |
| Reconciliation | Include frame index or tick in messages; server corrects divergence | Consistency under latency |
| Delta Updates | Send only changed tiles instead of full locked arrays | Network efficiency |
| Authentication | Extend `HandshakeMessage` with token/userId | Secure multi-user sessions |
| Logging | Structured JSON logs per thread with severity | Improved diagnostics |

## 10. Potential Pitfalls

| Pitfall | Symptom | Mitigation |
|---------|---------|------------|
| Race conditions on server client list | Random exceptions or missing broadcasts | Keep `lock(_clients)` usage consistent |
| Blocking reads stalling threads | Server AcceptLoop high CPU | Sleep or use async sockets (Unity-friendly wrapper) |
| Large board_state overhead | Latency spikes when many tiles | Delta encoding / compression |
| Unmarshaled background actions | Unity exceptions: “Must be called from main thread” | Always use dispatcher |
| Desync in queue | Different piece order on client vs server | Confirm seed + upcoming shapes alignment after each spawn |
| Silent disconnects | No updates; client appears frozen | Implement heartbeat & timeout logic |

## 11. Sample Data Shape (BoardStateMessage)

```
{
  "owner": "Player1",
  "width": 10,
  "height": 20,
  "lockedCount": 14,
  "lockedX": [0,1,2,...],
  "lockedY": [0,0,0,...],
  "lockedShapeIndex": [3,3,1,...],
  "hasActive": true,
  "activeShapeIndex": 2,
  "activePosX": 4,
  "activePosY": 17,
  "activeCellOffsetX": [0,1,-1,0],
  "activeCellOffsetY": [0,0,0,1],
  "queueLength": 5,
  "upcomingShapes": [5,3,2,0,6],
  "gameOver": false
}
```

## 12. ASCII Interaction Slice

```
Server:
  MultiplayerManager
   -> TcpServer
  -> AcceptLoop Thread
     -> ClientConnection (StartReceiving)
    -> OnRawMessage -> HandleIncomingServerSide()

Client:
  MultiplayerManager
   -> TcpClientPeer
  -> ReceiveLoop Thread -> OnRawMessage -> Dispatcher.Enqueue()
     -> ApplyBoardState / UpdateGameState / QueueSync
```

## 13. Conversion Instructions (to PDF)

You can convert this Markdown file to PDF using one of:

### Option A: Using VS Code
1. Install extension: “Markdown PDF”.
2. Open `docs/MultiplayerArchitecture.md`.
3. Right-click → “Markdown PDF: Export (pdf)”.

### Option B: Using Pandoc (CLI)
```bash
pandoc docs/MultiplayerArchitecture.md -o MultiplayerArchitecture.pdf
```

(Optionally add `--from gfm --pdf-engine=xelatex` for better styling.)

### Option C: Browser Print
1. Open the file in a browser (e.g., drag into Chrome).
2. Ctrl+P / Cmd+P → Destination: “Save as PDF”.

## 14. Suggested Repository Placement

Place this file under `TP-Integrador/TetrisVS/docs/MultiplayerArchitecture.md` (create `docs/` if missing) to signal architecture documentation.

## 15. Next Steps

- Implement garbage line logic and finalize versus mechanic.
- Add handshake extension for versioning (e.g., `protocolVersion`).
- Introduce delta board updates to cut payload size.
- Add latency compensation (client prediction for piece movement with server correction).

---

_This document is a generated synthesis based on inspected source fragments (network, board, adapter, messages). For deeper accuracy, cross-check with any omitted scripts (e.g., `Piece`, `SharedShapesQueue`, `ThreadDispatcher`)._
