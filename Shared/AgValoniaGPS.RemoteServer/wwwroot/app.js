// Renderer — consumes plain Scene + Tick objects via the transport interface.
// It has NO knowledge of SignalR or the wire format (see transport.js); swapping
// the transport at Phase 2 leaves this file untouched. Canvas2D for now; the
// CanvasKit swap (and client-side dead-reckoning) slot in here without touching
// the transport seam.

const cv = document.getElementById('c');
const ctx = cv.getContext('2d');
const hud = document.getElementById('hud');

function resize() { cv.width = innerWidth; cv.height = innerHeight; }
addEventListener('resize', resize); resize();

// ---- model (fed by the transport) ----
let scene = null;      // SceneDto
let tick = null;       // TickDto (latest — for sections/HUD)
let lastTick = null;   // { e, n, heading, speed, t } authoritative pose + receipt time, for DR
let connState = 'connecting…';

// ---- client-owned camera (never crosses the wire) ----
let pxPerM = 4.0;
let follow = true;
let camE = 0, camN = 0;

// ---- transport wiring (the only coupling point) ----
const transport = RemoteTransport.create({
  onScene(s) {
    scene = s;
    if (follow && (!tick || !tick.pose) && s.boundaries.length && s.boundaries[0].length) {
      const r = s.boundaries[0];
      camE = r.reduce((a, p) => a + p.e, 0) / r.length;
      camN = r.reduce((a, p) => a + p.n, 0) / r.length;
    }
  },
  onTick(t) {
    tick = t;
    if (t.pose) {
      lastTick = { e: t.pose.e, n: t.pose.n, heading: t.pose.heading, speed: t.pose.speed, t: performance.now() };
    }
  },
  onStatus(s) { connState = s; },
});
transport.start();

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
function strokePts(pts, close) {
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
// Dead-reckon the pose from the last authoritative tick: extrapolate along the
// heading at the reported speed. dt is clamped so motion freezes (rather than
// flies off) if ticks stall — same spirit as the app's own stale-pose cap.
function renderPose() {
  if (!lastTick) return null;
  let dt = (performance.now() - lastTick.t) / 1000;
  dt = Math.min(Math.max(dt, 0), 0.5);
  return {
    e: lastTick.e + lastTick.speed * Math.sin(lastTick.heading) * dt,
    n: lastTick.n + lastTick.speed * Math.cos(lastTick.heading) * dt,
    heading: lastTick.heading,
    speed: lastTick.speed,
  };
}
function draw() {
  ctx.clearRect(0, 0, cv.width, cv.height);

  const rp = renderPose();
  if (rp && follow) { camE = rp.e; camN = rp.n; }

  if (scene) {
    ctx.lineWidth = 2; ctx.strokeStyle = '#46a0ff';
    for (const ring of scene.boundaries) strokePts(ring, true);
    ctx.lineWidth = 1.5; ctx.strokeStyle = '#ffd24a';
    for (const tr of scene.tracks) strokePts(tr.points, false);
  }
  if (rp) vehicle(rp);

  const spd = rp ? (rp.speed * 3.6).toFixed(1) + ' km/h' : '—';
  const secs = tick && tick.sections ? tick.sections.filter(Boolean).length + '/' + tick.sections.length : '—';
  hud.textContent =
    `${connState}\n` +
    (scene ? `field: ${scene.fieldName}\nboundaries: ${scene.boundaries.length}  tracks: ${scene.tracks.length}\n` : 'waiting for scene…\n') +
    `speed: ${spd}   sections on: ${secs}\nzoom: ${pxPerM.toFixed(1)} px/m   follow: ${follow ? 'on' : 'off'}  (DR)`;

  requestAnimationFrame(draw);
}
draw();
