// Transport adapter — the ONLY file that knows the wire protocol.
//
// Today: SignalR + JSON. At Phase 2 (coverage) this is swapped for a binary
// WebSocket transport for the high-rate feeds, WITHOUT touching the renderer:
// the renderer only ever sees the { onScene, onTick, onStatus } interface and
// plain JS objects, never `signalR` or message names. That decoupling is the
// whole point — SignalR is provisional, the contract/renderer are not.

window.RemoteTransport = {
  /**
   * @param {{ onScene?: (scene:object)=>void,
   *           onTick?:  (tick:object)=>void,
   *           onStatus?:(state:string)=>void }} handlers
   */
  create(handlers) {
    const status = s => handlers.onStatus && handlers.onStatus(s);

    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/maphub')
      .withAutomaticReconnect()
      .build();

    conn.on('scene', s => handlers.onScene && handlers.onScene(s));
    conn.on('tick', t => handlers.onTick && handlers.onTick(t));
    conn.onreconnecting(() => status('reconnecting…'));
    conn.onreconnected(() => status('connected'));
    conn.onclose(() => status('disconnected'));

    return {
      start() {
        status('connecting…');
        conn.start().then(() => status('connected')).catch(e => status('error: ' + e));
      },
      stop() { conn.stop(); },
    };
  },
};
