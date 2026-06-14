// Phase 1 client — Canvas2D renderer fed by the Scene + Tick SignalR feed.
// Camera is entirely client-side (pan/zoom/follow never cross the wire).
// This is the Phase 0 map-render de-risk in context; CanvasKit swaps in later.

const cv = document.getElementById('c');
const ctx = cv.getContext('2d');
const hud = document.getElementById('hud');

function resize() { cv.width = innerWidth; cv.height = innerHeight; }
addEventListener('resize', resize); resize();

let scene = null;      // SceneDto
let tick = null;       // TickDto
let connState = 'connecting…';

// Client-owned camera: world meters -> screen px.
let pxPerM = 4.0;            // zoom
let follow = true;          // re-center on the vehicle each tick
let camE = 0, camN = 0;     // camera center in field-local meters

const conn = new signalR.HubConnectionBuilder()
  .withUrl('/maphub')
  .withAutomaticReconnect()
  .build();

conn.on('scene', s => {
  scene = s;
  // First scene with geometry but no vehicle yet: center on the boundary.
  if (follow && (!tick || !tick.pose) && s.boundaries.length && s.boundaries[0].length) {
    const r = s.boundaries[0];
    camE = r.reduce((a, p) => a + p.e, 0) / r.length;
    camN = r.reduce((a, p) => a + p.n, 0) / r.length;
  }
});
conn.on('tick', t => {
  tick = t;
  if (follow && t.pose) { camE = t.pose.e; camN = t.pose.n; }
});

conn.onreconnecting(() => connState = 'reconnecting…');
conn.onreconnected(() => connState = 'connected');
conn.onclose(() => connState = 'disconnected');
conn.start().then(() => connState = 'connected').catch(e => connState = 'error: ' + e);

// ---- camera controls ----
addEventListener('wheel', e => {
  e.preventDefault();
  pxPerM *= e.deltaY < 0 ? 1.1 : 0.9;
  pxPerM = Math.min(200, Math.max(0.2, pxPerM));
}, { passive: false });

let dragging = false, lastX = 0, lastY = 0;
addEventListener('pointerdown', e => { dragging = true; follow = false; lastX = e.clientX; lastY = e.clientY; });
addEventListener('pointerup', () => dragging = false);
addEventListener('pointermove', e => {
  if (!dragging) return;
  camE -= (e.clientX - lastX) / pxPerM;
  camN += (e.clientY - lastY) / pxPerM;
  lastX = e.clientX; lastY = e.clientY;
});
addEventListener('keydown', e => { if (e.key === 'f' || e.key === 'F') follow = true; });

// ---- render ----
function w2s(e, n) {
  return [cv.width / 2 + (e - camE) * pxPerM, cv.height / 2 - (n - camN) * pxPerM];
}
function stroke(pts, close) {
  if (!pts || !pts.length) return;
  ctx.beginPath();
  pts.forEach((p, i) => { const [x, y] = w2s(p.e, p.n); i ? ctx.lineTo(x, y) : ctx.moveTo(x, y); });
  if (close) ctx.closePath();
  ctx.stroke();
}
function vehicle(p) {
  const [x, y] = w2s(p.e, p.n);
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(p.heading); // radians, 0 = north (up), clockwise
  ctx.fillStyle = '#39FF6A';
  ctx.beginPath();
  ctx.moveTo(0, -14); ctx.lineTo(9, 11); ctx.lineTo(-9, 11); ctx.closePath();
  ctx.fill();
  ctx.restore();
}
function draw() {
  ctx.clearRect(0, 0, cv.width, cv.height);
  if (scene) {
    ctx.lineWidth = 2; ctx.strokeStyle = '#46a0ff';
    for (const ring of scene.boundaries) stroke(ring, true);
    ctx.lineWidth = 1.5; ctx.strokeStyle = '#ffd24a';
    for (const tr of scene.tracks) stroke(tr.points, false);
  }
  if (tick && tick.pose) vehicle(tick.pose);

  const spd = tick && tick.pose ? (tick.pose.speed * 3.6).toFixed(1) + ' km/h' : '—';
  const secs = tick && tick.sections ? tick.sections.filter(Boolean).length + '/' + tick.sections.length : '—';
  hud.textContent =
    `${connState}\n` +
    (scene ? `field: ${scene.fieldName}\nboundaries: ${scene.boundaries.length}  tracks: ${scene.tracks.length}\n` : 'waiting for scene…\n') +
    `speed: ${spd}   sections on: ${secs}\nzoom: ${pxPerM.toFixed(1)} px/m   follow: ${follow ? 'on' : 'off'}`;

  requestAnimationFrame(draw);
}
draw();
