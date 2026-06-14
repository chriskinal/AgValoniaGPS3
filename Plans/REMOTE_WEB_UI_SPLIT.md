# Remote / Web UI Split — Plan

**Status:** Direction settled (2026-06-14). Goal and topology decided (see §0); contract design and coverage codec carried over from the original exploration. Phased plan in §7. Not yet scheduled.
**Question that started it:** "Can we have the AgValonia UI as a web page / WASM, with the ViewModel and Model living on the host PC?"

---

## 0. Decision (settled 2026-06-14)

- **Goal: replace the in-cab native app with a browser-based UI.** Not a remote-companion bolt-on — the browser becomes the primary in-cab View. (Remote monitoring then falls out for free; see below.)
- **Topology: a Windows/Linux PC in the cab runs the headless host server.** It owns all sockets (UDP/PGN, NTRIP/TCP), the control loop, all Services, ViewModels, and Config/State. Clients are **browsers on Android / iOS / Windows / Linux**, connecting over the LAN.
- **Consequence — the iPad/iOS background-server problem disappears.** Because the server lives on the cab PC (not the tablet), tablets are just LAN browser clients; Safari/Chrome talking to a LAN WebSocket is fine. There is no longer any architectural distinction between an "in-cab" and a "remote" screen — every display is a network client of the one cab PC.
- **This is Path B with a headless host** (see §1/§4). It is a *superset* of remote monitoring: once the in-cab UI is a web client, a phone/laptop on the LAN is the same client at a different IP.
- **Cost, stated plainly:** the entire View layer is rewritten in web tech (map renderer + 20+ dialogs + config tabs + wizards + panels), the host is made headless, and the browser becomes a safety-relevant display (kiosk, offline asset serving, watchdog). Models/Services/ViewModels are kept; only Views are eventually retired. See risks in §7.

---

## TL;DR

- **Avalonia → WASM** ships the *whole* app (View + VM + Model) into the browser. It does **not** give you a "View in browser, VM on host" split for free — Avalonia data binding is in-process. Rejected (§1).
- The split is a **client/server seam**, not a compile target: **the cab PC becomes a local server**; every browser is a client of a purpose-built realtime contract.
- A browser sandbox **cannot do raw UDP/TCP** — so the PGN/autosteer/NTRIP path *must* stay host-side regardless. This is the constraint that decides the whole architecture.
- WASM is **not** a hard requirement. **JS/TS + CanvasKit**, shipped as an installable PWA, is the recommended client tier (§4) — one web app covers all four client OSes, served entirely from the cab PC.
- Recommended shape: **hybrid seam** — stream *app state* for the map (client renders with CanvasKit, dead-reckons locally), use *RPC* for the chrome (panels/dialogs/settings). A typed contract (proto) is the team boundary.
- **Control authority stays host-side, always.** The browser is a view + command issuer, never in the realtime control path. A disconnected client must never stop autosteer.
- The one genuinely load-bearing piece is **coverage streaming** — solved with a keyframe + delta tile model that reuses machinery (dirty rects, `_newCells`, COVS RLE) the app already has.

---

## 1. Why "Avalonia as WASM" isn't the answer to the question asked

Avalonia has a first-class **Browser** backend (`net10.0-browser`) that compiles the .NET app to WebAssembly and renders via SkiaSharp onto an HTML canvas. Our ~92% shared layer would largely compile, and we'd add a `AgValoniaGPS.Browser` platform project alongside Desktop/iOS/Android.

But Avalonia binding is **in-process**: a View binds to a ViewModel as live .NET objects via `INotifyPropertyChanged` in the same address space. Compiling to WASM ships **View + VM + Model together into the browser**. There is no built-in seam that lets a browser-side View bind to a VM living in another process on the host.

It also reintroduces a hard blocker: **a browser sandbox cannot do raw UDP/TCP.** Our entire AgOpenGPS integration (PGN over UDP, autosteer, sections) and NTRIP (raw TCP) cannot run from inside the browser. That work has to stay host-side regardless.

So "View in browser, VM/Model on host" maps onto a **client/server architecture**, not a WASM build.

---

## 2. The real decision: where is the seam, and what crosses it?

Every approach is a choice of *which layer you remote*. The transport is almost an implementation detail once this is fixed.

| Seam | What crosses the wire | Verdict |
|---|---|---|
| **Pixels** | Rendered frames (RDP/VNC/game-stream, Avalonia remote proto) | Heaviest; map = video stream. Avoid. |
| **Draw calls** | Skia draw ops per frame | Dies at 60 FPS. Avoid. |
| **Retained UI / DOM** | UI-tree diffs (Blazor Server, LiveView, Hotwire) | Great for *forms*, bad for the *map* (server-side paint loop). |
| **App state** | Compact state deltas; client renders locally | **The map's home.** Fits our dead-reckoning model. |
| **Service / RPC** | Commands + query responses; client is its own app | **The chrome's home.** Classic SPA + backend. |
| **Domain model** | Sync the model (CRDT/local-first) | Overkill — single source of truth lives on the host. |

**Key realization: the seam need not be uniform.** The map and the chrome want *different* seams. Use a hybrid.

---

## 3. Technology survey (for context)

### Avalonia WASM
Whole app in browser. UDP blocker + ships VM/Model to client. Not the split asked for. (§1)

### Blazor + Blazing.Mvvm
`Blazing.Mvvm` bridges **CommunityToolkit.Mvvm** ViewModels to Blazor `.razor` components — and our VMs are *already* CommunityToolkit.Mvvm (`ObservableObject`/`SetProperty`/`RelayCommand`). So a Blazor port could reuse **Models + Services + ViewModels**; only the **View layer is a Razor rewrite**.

- **Blazor Server**: components + VM + Model + Services run **on the host**; only DOM diffs stream to the browser over SignalR. This *literally* matches "VM/Model on host, thin browser client," and UDP keeps working server-side. **But** the high-FPS Skia map fights the Server model: the paint callback runs server-side, so per-frame draw calls would marshal over SignalR — untenable at 60 FPS. Great for the form-y chrome, bad for the map.
- **Blazor WASM**: everything ships to browser → UDP blocker returns, VM/Model leave the host.

Net: Blazor Server fits everything *except* the map; the map remains the awkward case.

### CanvasKit (Google's WASM Skia) vs SkiaSharp.Views.Blazor
Both are **the same Skia engine compiled to WASM**, drawing to a WebGL/WebGPU canvas. The difference is the API surface:

- **CanvasKit** exposes Skia to **JavaScript/TS** (what Flutter web uses). For us, using it means **rewriting the map renderer in JS/TS** — our `SKCanvas` draw code is C#.
- **SkiaSharp.Views.Blazor** exposes the **C# `SKCanvas`** binding in the browser (`SKGLView`), so the existing draw code could be factored out and reused.

CanvasKit doesn't *help* the map relative to SkiaSharp-on-WASM; for a .NET codebase it's the more expensive option (throws away C# draw code for the same engine). **CanvasKit is the right call only if the front-end is standalone JS/TS** (no Blazor/Avalonia) — which the Dv team has said is acceptable.

Either way the choice of Skia build is **orthogonal** to the placement problem: the map renderer must run **client-side**, fed by a state feed.

---

## 4. Recommended architecture

1. **Host = local server.** Embed an HTTP+WebSocket server (ASP.NET Core/Kestrel or lightweight) *inside* the existing app. Browser connects to `ws://localhost` (or LAN IP). Same pattern as OctoPrint / Home Assistant / qBittorrent WebUI. This also keeps raw UDP host-side — the browser only speaks HTTP/WS to localhost.

2. **Hybrid seam:**
   - **Map → state seam.** Host streams a compact pose/track/coverage feed; the JS+CanvasKit renderer dead-reckons and draws at display rate.
   - **Chrome → RPC seam.** Low-frequency request/response for panels, dialogs, settings, NTRIP, network IO.

3. **Purpose-built contract, NOT VM mirroring.** Do not auto-serialize `MainViewModel`'s properties — that 19-file partial class would couple the client to internal VM shape. Define a deliberate wire contract around *what the View needs*; the VM stays host-side and *feeds* the contract.

4. **The contract is the team boundary.** A typed schema (proto / TS) codegens both sides and is the literal interface between the Dv front-end and our host.

5. **Headless-capable VM is the prize — and the litmus test.** Two flavors:
   - *(a)* Web UI **alongside** the Avalonia app (lowest disruption, good first step).
   - *(b)* Web UI as the **only** View on a headless host (the clean split).
   (b) only works if Models/Services/VM are genuinely View-independent. If map data is only reachable *through* view-state or only computed *inside* the VM, the seam will expose it — which aligns with the existing SoT/config-state cleanup (see `CONFIG_STATE_AUDIT.md`). **The split is a forcing function for the discipline we want anyway.**

### Guardrails
- **Control loop never crosses the wire.** Autosteer/section/coverage run host-side at 50/100 Hz, decoupled from GPS (see `Plans/Completed/UNIFIED_CONTROL_LOOP_PLAN.md`). The browser issues *requests*; a steering correction must not depend on a browser being connected.
- **Multi-client comes nearly free** once the host is a server — phone viewer, second cab screen, read-only observer are all just clients of the same contract.

### Transport
- **gRPC / ConnectRPC** — typed `.proto`, codegen TS client, server-streaming for feeds, unary for commands. Best when the contract *is* the cross-team interface doc.
- **SignalR** — easiest if the host stays .NET; auto-reconnect, typed hubs, first-class JS client.
- **Raw WebSocket + tiny binary schema** — most control; ideal for the high-rate feeds specifically.
- **WebTransport (HTTP/3)** — datagrams give browser-side lossy/drop-tolerant delivery (UDP-like), conceptually perfect for a dead-reckoned pose feed, but young. Watch, don't bet.

A clean combo: **gRPC/Connect (or SignalR) for commands + binary WS/server-stream for the feeds.**

### Client tier (settled)
**JS/TS SPA + CanvasKit, shipped as an installable PWA.** Rationale given the §0 topology:
- Clients span iOS / Android / Windows / Linux browsers — **one web app covers all four.**
- CanvasKit is the **same Skia engine** the app renders with today; the map renderer is JS/TS draw code fed by the state feed.
- A **PWA** installs fullscreen/kiosk-like on a tablet and serves **entirely from the cab PC** — no internet on a tractor, no CDN dependency.
- The **Blazor WASM** alternative keeps the stack all-.NET, but its one advantage (reusing CommunityToolkit VMs) **evaporates here**: the VMs are coupled to *in-process* services that now live in the headless host, so a Blazor client marshals over the wire just like a JS client would. Choose Blazor only if the team strongly prefers C# on the front-end.

---

## 5. Contract sketch (proto3)

Written as proto3 because it codegens both C# (host) and TS (client). **The design rules matter more than the field lists.**

### Design rules (agree on these first)
1. **Coordinate space is field-local meters.** Host sends field origin (lat/lon) once; everything after is `x = easting, y = northing`. Renderer never deals in lat/lon.
2. **Scene vs. Tick are separate.** *Scene* = static-ish geometry (boundaries, tracks), versioned, sent on change only, client-cached. *Tick* = ~10 Hz dynamic feed (pose, guidance, sections).
3. **The client owns the camera.** Pan/zoom/tilt/follow-mode are client-local and never cross the wire. Host streams *world* state; the View decides where to look. This is what lets the map-centric held-camera auto-pan at 60/120 Hz with zero round-trips.
4. **The client dead-reckons.** Tick carries pose + velocity + heading rate + GPS timestamp so the renderer extrapolates between ticks (same model the unified control loop already uses for screen position).
5. **Control authority stays host-side, always.** Commands are *requests*; the host's control loop acts. A disconnected client never stops steering.

### Channels

```proto
syntax = "proto3";
package agvalonia.remote.v1;

service MapService {
  rpc SceneStream(Subscribe) returns (stream Scene);   // static-ish, on change
  rpc TickStream(Subscribe)  returns (stream Tick);    // ~10 Hz dynamic
}
service ControlService { rpc Execute(Command) returns (CommandResult); }
service ConfigService {
  rpc Get(ConfigQuery)   returns (Config);
  rpc Update(ConfigPatch) returns (CommandResult);
  rpc Watch(Subscribe)   returns (stream Config);
}
service EventService { rpc EventStream(Subscribe) returns (stream Event); }
service CoverageService { rpc Stream(Subscribe) returns (stream CoverageMessage); } // §6

message Subscribe { string client_id = 1; }
message Vec2 { double x = 1; double y = 2; }            // field-local meters
message Vec3 { double x = 1; double y = 2; double heading = 3; }
```

### Scene — static-ish, versioned, sent on change only

```proto
message Scene {
  uint64 version       = 1;     // bump on any change; client diffs against cache
  GeoOrigin origin     = 2;     // lat/lon of local (0,0), once per field
  string field_name    = 3;
  repeated Boundary boundaries = 4;   // outer + inner holes, DP-simplified
  Boundary headland    = 5;
  repeated Track tracks = 6;
}
message GeoOrigin { double latitude = 1; double longitude = 2; }
message Boundary {
  bool is_outer        = 1;     // false = inner hole
  bool is_drive_through = 2;
  repeated Vec2 points = 3;     // already simplified at source
}
message Track {                  // unified model: AB line = 2 pts, curve = N pts
  string id = 1; string name = 2; TrackMode mode = 3;
  repeated Vec3 points = 4; bool is_visible = 5; double nudge_distance = 6;
}
enum TrackMode { AB_LINE = 0; CURVE = 1; PIVOT = 2; }
```

### Tick — dynamic, ~10 Hz, the feed the renderer extrapolates from

```proto
message Tick {
  uint64 scene_version = 1;     // which Scene this tick is valid against
  double gps_time      = 2;     // for dead-reckoning extrapolation
  Pose pose            = 3;
  FixQuality fix       = 4;
  Guidance guidance    = 5;
  repeated SectionState sections = 6;
  YouTurn youturn      = 7;      // present only while a turn is active
}
message Pose {
  Vec2 position = 1; double heading = 2;     // radians
  double speed = 3;  double heading_rate = 4; // client extrapolates from these
}
enum FixQuality { NO_FIX = 0; GPS = 1; DGPS = 2; RTK_FLOAT = 3; RTK_FIX = 4; }
message Guidance {
  bool engaged = 1;             // autosteer state (host-owned; readout only)
  string active_track_id = 2;
  double cross_track_error = 3; // meters, signed
  double steer_angle = 4;       // radians
  Vec2 lookahead_point = 5;
  repeated Vec2 guidance_line = 6;
}
message SectionState {
  uint32 index = 1; bool is_on = 2;
  double left_offset = 3; double right_offset = 4; // tool-relative meters
}
message YouTurn { repeated Vec2 path = 1; double progress = 2; bool is_manual = 3; }
```

### Commands — client → host requests

```proto
message Command {
  oneof kind {
    MarkPoint mark_a         = 1;
    MarkPoint mark_b         = 2;
    SelectTrack select_track = 3;
    NudgeTrack nudge         = 4;
    SetBool engage_autosteer = 5;   // default OFF / readout-only until safety owner decides
    SetBool auto_youturn     = 6;
    TriggerYouTurn youturn   = 7;
    SetSection section       = 8;
    OpenField open_field     = 9;
    SetBool record_boundary  = 10;
    SimControl simulator     = 11;  // testing only
  }
}
message MarkPoint { }                              // uses current pose
message SelectTrack { string track_id = 1; }
message NudgeTrack { string track_id = 1; double meters = 2; }
message SetBool { bool value = 1; }
message TriggerYouTurn { bool turn_left = 1; bool skip = 2; }
message SetSection { uint32 index = 1; SectionMode mode = 2; }
enum SectionMode { OFF = 0; ON = 1; AUTO = 2; }
message OpenField { string field_name = 1; }
message SimControl { double speed = 1; double steer = 2; bool reset = 3; }
message CommandResult { bool ok = 1; string message = 2; }
```

### Config — maps onto ConfigurationStore; coarse CRUD, not per-tick

```proto
message ConfigQuery { ConfigSection section = 1; }
message ConfigPatch { ConfigSection section = 1; bytes json = 2; }
message Config      { ConfigSection section = 1; bytes json = 2; uint64 version = 3; }
enum ConfigSection { VEHICLE = 0; TOOL = 1; GUIDANCE = 2; NTRIP = 3; NETWORK = 4; DISPLAY = 5; }
```

### Events — semantic; client chooses the UI (no dialogs cross the wire)

```proto
message Event {
  oneof kind { Alert alert = 1; ConnectionState conn = 2; FieldState field = 3; }
}
message Alert { Severity severity = 1; string text = 2; }
enum Severity { INFO = 0; WARN = 1; ERROR = 2; }
message ConnectionState { bool gps = 1; bool modules = 2; bool ntrip = 3; FixQuality fix = 4; }
message FieldState { bool field_open = 1; bool unsaved_changes = 2; }
```

**Note what's absent:** no camera/zoom/pan, no dialog state, no `IsXVisible` flags. That absence is the litmus test (§4.5) — a clean fill of `Scene` + `Tick` from the *services/pipeline* (not the VM) proves the VM is View-independent.

---

## 6. Coverage streaming (the load-bearing message)

### What the implementation forces

The coverage system is already a two-layer, dirty-tracked, RLE-serialized design (`Shared/AgValoniaGPS.Services/Coverage/CoverageMapService.cs`), so most streaming machinery already exists:

| Fact from the code | Consequence |
|---|---|
| **Detection layer** = 0.1 m bits, authoritative, **up to ~72 MB** (582 ha) | Never goes on the wire. Exists for section-control/overlap decisions → stays host-side. |
| **Display layer** = RGB565, palette-able, capped 25 M pixels, coarser cell | This is what the renderer draws → **stream this.** |
| `ConsumeDirtyRect()` → changed region in display-pixel coords | Free delta source. |
| `_newCells` / `GetNewCoverageBitmapCells()` | Exact newly-painted cells — tighter delta source. |
| `CoverageUpdated` fires on flush (~10 Hz, coalesced) | Natural pacing hook. |
| **COVS disk format** = palette + **RLE over palette-index bytes** | Don't invent a codec — the on-disk format *is* the codec; ship it per-tile. |
| Display cell size **rescales** when bounds expand past 25 M cap | Resamples the whole bitmap → invalidates all client tiles → must be a protocol event. |

**Decisive call:** stream the **display layer**, display resolution, **palette-indexed** — never the detection layer. Keeps authoritative coverage (and section logic that depends on it) host-side; same data COVS persists.

### Model: keyframe + delta, tiled (a video-codec shape)

- **Tiles** (e.g. 256×256 display cells) enable progressive snapshot, partial invalidation, and bounds-expansion (new tiles just appear).
- **Snapshot (join):** stream all non-empty tiles, RLE'd, progressively; client shows "loading coverage."
- **Delta (steady state):** per flush, tiles intersecting the dirty rect; per tile send full RLE *or* sparse changed-cells patch — whichever is smaller (codec skip/intra decision).
- **Epoch:** `coverage_epoch` bumps on display cell-size / origin change (rescale). Client drops all tiles → re-snapshots.
- **Coverage rides its own stream, decoupled from the pose tick** — pose is latency-critical, coverage can lag 100–200 ms. Matches the decoupled-cadence architecture.

```proto
message CoverageMessage {
  oneof kind {
    CoverageInit init   = 1;   // on subscribe, and on every epoch change
    CoverageTile tile   = 2;   // snapshot tiles (burst) AND deltas (ongoing)
    CoverageEpoch epoch = 3;   // "drop everything, re-snapshot incoming"
  }
}
message CoverageInit {
  uint32 epoch = 1; double cell_size = 2;   // meters per display cell this epoch
  Vec2 grid_origin = 3;                      // world coords of tile (0,0)
  uint32 tile_cells = 4;                     // tile edge, e.g. 256
  repeated uint32 palette = 5;               // RGB565 zone colors; index 0 = uncovered
  uint32 snapshot_tile_count = 6;            // for a progress bar
}
message CoverageTile {
  uint32 epoch = 1;                          // stale-epoch tiles dropped on arrival
  sint32 tile_x = 2; sint32 tile_y = 3;
  TileEncoding encoding = 4; bytes data = 5;
}
enum TileEncoding {
  RLE_FULL = 0;   // run-length palette indices, whole tile (COVS format, per tile)
  CELLS    = 1;   // sparse: repeated (uint16 cell_offset, uint8 palette_idx)
}
message CoverageEpoch { uint32 new_epoch = 1; }
```

### Bandwidth — and where the cost actually is

| Phase | Volume | Verdict |
|---|---|---|
| **Steady-state delta** | ~250–1000 cells/flush; CELLS ≈ 3 B/cell ⇒ ~1–3 KB/flush; ~5–10 Hz ⇒ **~10–30 KB/s** | Non-problem. |
| **Join snapshot** | ≤25 M cells × ~1 B pre-RLE = ≤25 MB worst case; RLE crushes contiguous swaths → **hundreds of KB to a few MB** | **The load-bearing case.** Tiled + progressive + RLE = resumable, show-progress fill. |
| **Epoch rescale** | Re-snapshot | Rare; same path as join. |

The per-tick delta is trivial; the real engineering is the **late-join snapshot of a heavily-worked field**, which tiling + COVS-RLE makes streamable instead of a 25 MB stall.

### Alternative considered: client-side rasterization

Have the client rasterize its own coverage from the pose + section + tool-geometry feed it already receives (run the same quad-rasterization the host runs). Steady-state coverage bandwidth → ~0.

**Recorded, not recommended as primary:**
- Reimplements `RasterizeQuadToBitmap` + coverage-margin + yaw + debounce in JS/TS, kept bit-identical forever → divergence-bug farm; exactly the "computation duplicated into the View tier" the MVVM/SoT discipline forbids.
- Trades a bandwidth non-problem (~10–30 KB/s) for a correctness/maintenance problem.
- Dropped ticks → missed coverage → still needs the host snapshot to heal, so it doesn't even escape the snapshot path.

Host-authoritative tile streaming keeps coverage single-source-of-truth, reuses existing code, and the only price (join snapshot) is owed under the alternative too. Documented escape hatch if snapshot bandwidth on a weak LAN link ever proves painful.

### Open items
- **Tile encoding heuristic** — RLE_FULL vs CELLS per tile (pick smaller bytes; one-line rule so host/client agree on decode).
- **Palette recolor** — if the user recolors a section mid-job (`Tool.GetSectionColor`), prefer a cheap **palette-only message** that recolors existing tiles client-side over a full epoch re-snapshot.

---

## 7. Phased plan

The native app keeps running throughout. **Do not cut over until parity is field-validated** (feature-branch discipline). The leaner codebase is reached only at Phase 5 — and the win is *removing Views*, not "going web."

### Phase 0 — De-risk the two unknowns (throwaway spikes)
- **Headless boot:** run the existing Services + ViewModels with **no Avalonia View attached** and prove `Scene` + `Tick` can be filled from the *services/pipeline*, not from view-state. This is the litmus test (§4.5) and the same discipline the parked config/state audit is about. If map data is only reachable *through* the View, that is the first real finding.
- **Map render:** CanvasKit drawing vehicle + tracks + boundaries off a mock feed, on a real tablet browser, at acceptable FPS. De-risk the *render*, not the MVVM binding.

### Phase 1 — Cab PC becomes a LAN server + live-map MVP
- Embed Kestrel in the host. `SceneStream` + `TickStream` over binary WS only. SPA renders the live map with a **client-owned camera** (pan/zoom/tilt/follow never cross the wire) and dead-reckons pose between ticks. Validate from one tablet on the LAN. This is also the cleanest proof of VM View-independence.

### Phase 2 — Coverage streaming (the load-bearing piece)
- `CoverageService` with snapshot + delta tiles (§6), reusing `ConsumeDirtyRect` / `_newCells` / COVS-RLE. Stream the **display** layer only — the detection layer stays host-side. Validate **join-snapshot timing on a real heavily-worked field**; that is the only bandwidth case that bites.

### Phase 3 — Full UI parity (the bulk of the effort)
- RPC for config CRUD (onto `ConfigurationStore`), commands (`ControlService`), events/alerts (§5). Rebuild every panel, dialog, config tab, and wizard in the SPA. Native app stays live; this phase is most of the calendar.

### Phase 4 — In-cab hardening
- Fullscreen/kiosk PWA, fully offline asset serving, robust reconnect. **Run a browser on the cab PC's own screen as the guaranteed primary display**, with tablets as preferred/additional screens — this removes the "primary display depends on WiFi" risk. Multi-screen falls out for free. Settle the safety question: remote `engage_autosteer` defaults **OFF / readout-only** until a safety owner signs it off.

### Phase 5 — Retire the native View
- Once parity holds in the field, delete the Avalonia Views. Models / Services / ViewModels stay; the platform projects shrink to "launch the headless host." This is when the codebase actually gets leaner.

## 8. Risks (in-cab replacement)

1. **Primary display over WiFi.** The operator's main screen is now a LAN client. Mitigated by the host-local browser as the guaranteed screen (Phase 4); control authority is host-side, so a dropped client never stops steering.
2. **The View rewrite is large** — map + 20+ dialogs + config tabs + wizards. Phase 3 is most of the schedule. The native app must stay live until parity is field-proven.
3. **VM/service view-independence is a hard prerequisite** (Phase 0 litmus). It is work already wanted via the config/state audit — the split is a forcing function for it.

---

## References
- `Plans/ARCHITECTURE.md` — services, state, data flow
- `Plans/Completed/UNIFIED_CONTROL_LOOP_PLAN.md` — decoupled cadences (100/50/10 Hz, dead-reckoned position)
- `Plans/CONFIG_STATE_AUDIT.md` — SoT cleanup the headless-VM split would force/benefit from
- `Shared/AgValoniaGPS.Services/Coverage/CoverageMapService.cs` — coverage layers, dirty rects, COVD/COVS formats
- `Shared/AgValoniaGPS.Models/Track/Track.cs` — unified track model
- Memory: "Coverage is cell-based, not patches"; "Control cadences decoupled from GPS"; "Map-centric is non-negotiable"
