namespace Wayfarer.Mockup;

/// <summary>The design bench: a bin of every piece the game's own windows are cut from, and a
/// stage to arrange them on. Everything on the stage is drawn by one routine, which is also what
/// writes the picture out, so what is saved is exactly what was on screen.</summary>
internal static class DesignPage
{
    public const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Wayfarer design bench</title>
<style>
  :root { --chrome:#1b1d21; --panel:#24272c; --edge:#383c44; --text:#d9d5cc; --dim:#8b8f98; --pick:#c8a24a; }
  * { box-sizing: border-box; }
  body {
    margin:0; height:100vh; display:grid; grid-template-columns:300px 1fr 250px;
    background:var(--chrome); color:var(--text);
    font:13px/1.45 "Segoe UI", system-ui, sans-serif; overflow:hidden; user-select:none;
  }
  aside { background:var(--panel); overflow-y:auto; }
  #bin { border-right:1px solid var(--edge); }
  #props { border-left:1px solid var(--edge); }
  h2 { margin:0; padding:10px 12px 6px; font-size:11px; letter-spacing:.08em; text-transform:uppercase; color:var(--dim); }
  .find { position:sticky; top:0; z-index:2; background:var(--panel); padding:10px; border-bottom:1px solid var(--edge); }
  .find input { width:100%; }
  details { border-bottom:1px solid #2c3037; }
  summary { padding:7px 12px; cursor:pointer; font-size:12px; }
  summary:hover { color:var(--pick); }
  summary span { color:var(--dim); font-size:10px; margin-left:5px; }
  .shelf { display:flex; flex-wrap:wrap; gap:5px; padding:4px 10px 8px; }
  .sheet { border-top:1px solid #2c3037; }
  .sheetname { padding:7px 12px 2px; font-size:11px; color:var(--text); }
  .sheetname span { color:var(--dim); font-size:10px; display:block; }
  .chip {
    background:#14161a; border:1px solid var(--edge); border-radius:2px; cursor:pointer;
    display:grid; place-items:center; padding:3px; min-width:34px; min-height:34px;
  }
  .chip:hover { border-color:var(--pick); }
  .chip i { display:block; background-repeat:no-repeat; image-rendering:pixelated; }
  .icons { display:grid; grid-template-columns:repeat(auto-fill,32px); gap:4px; padding:4px 10px 12px; }
  .ico { width:32px; height:32px; background-size:32px 32px; cursor:pointer; outline:1px solid transparent; }
  .ico:hover { outline-color:var(--pick); }

  main { display:flex; flex-direction:column; min-width:0; }
  .bar { display:flex; gap:8px; align-items:center; padding:8px 12px; border-bottom:1px solid var(--edge); flex-wrap:wrap; }
  .bar label { color:var(--dim); font-size:11px; display:flex; gap:4px; align-items:center; }
  input, select { background:#14161a; color:var(--text); border:1px solid var(--edge); border-radius:3px; padding:3px 5px; font:inherit; font-size:12px; }
  input[type=number] { width:60px; }
  button { background:#2f343c; color:var(--text); border:1px solid var(--edge); border-radius:3px; padding:4px 10px; font:inherit; cursor:pointer; }
  button:hover { border-color:var(--pick); }
  button.go { background:var(--pick); color:#1b1d21; border-color:var(--pick); font-weight:600; }
  .room { flex:1; overflow:auto; display:grid; place-items:center; padding:24px; }
  #stage { position:relative; background:#0d0f12; box-shadow:0 0 0 1px var(--edge), 0 18px 50px #0008; }
  #grid { position:absolute; inset:0; pointer-events:none; opacity:.16; }
  .item { position:absolute; }
  .item canvas { width:100%; height:100%; display:block; }
  .item.on { outline:1px solid var(--pick); }
  .grip { position:absolute; right:-4px; bottom:-4px; width:9px; height:9px; background:var(--pick); cursor:nwse-resize; }
  .row { display:flex; gap:6px; padding:0 12px 7px; align-items:center; }
  .row label { width:56px; color:var(--dim); font-size:11px; }
  .row input:not([type=number]) { flex:1; min-width:0; }
  .hint { padding:6px 12px 16px; color:var(--dim); font-size:11px; line-height:1.7; }
  .palette { display:grid; grid-template-columns:repeat(auto-fill,20px); gap:3px; padding:0 12px 10px; max-height:200px; overflow-y:auto; }
  .swatch { width:20px; height:20px; border:1px solid #0006; border-radius:2px; cursor:pointer;
            background-image:linear-gradient(45deg,#555 25%,transparent 25%,transparent 75%,#555 75%),
                             linear-gradient(45deg,#555 25%,transparent 25%,transparent 75%,#555 75%);
            background-size:8px 8px; background-position:0 0,4px 4px; }
  .swatch span { display:block; width:100%; height:100%; }
  .target { width:22px; height:22px; border:1px solid var(--edge); border-radius:3px; }
  #what { padding:0 12px 8px; font-size:11px; color:var(--dim); word-break:break-all; }
</style>
</head>
<body>

<aside id="bin"><div class="find"><input id="find" placeholder="filter by window or picture…"></div></aside>

<main>
  <div class="bar">
    <label>window <input type="number" id="sw" value="680"> × <input type="number" id="sh" value="620"></label>
    <label><input type="checkbox" id="snap" checked> snap</label>
    <label><input type="number" id="gridsize" value="4" style="width:46px"></label>
    <label><input type="checkbox" id="showgrid" checked> grid</label>
    <button id="back">send back</button>
    <button id="front">bring front</button>
    <button id="dupe">duplicate</button>
    <button id="wipe">clear</button>
    <button class="go" id="shot">save picture</button>
  </div>
  <div class="room"><div id="stage"><canvas id="grid"></canvas></div></div>
</main>

<aside id="props">
  <h2>Selected</h2>
  <div id="what">nothing</div>
  <div class="row"><label>x</label><input type="number" id="px"></div>
  <div class="row"><label>y</label><input type="number" id="py"></div>
  <div class="row"><label>width</label><input type="number" id="pw"></div>
  <div class="row"><label>height</label><input type="number" id="ph"></div>
  <div class="row"><label>opacity</label><input type="range" id="po" min="0" max="100" value="100"></div>
  <div class="row"><label>9-slice</label><input type="number" id="pslice" value="0"></div>
  <div class="row"><button id="actual" style="width:100%">back to its own size</button></div>
  <h2>Add words</h2>
  <div class="row"><input type="text" id="words" placeholder="type a line"></div>
  <div class="row">
    <label>size</label>
    <select id="wordsize"><option>12</option><option selected>14</option><option>18</option><option>36</option></select>
    <select id="theme"><option value="dark" selected>Dark theme</option><option value="light">Light theme</option></select>
  </div>
  <div class="row"><label>ink</label><div class="target" id="fillnow"></div>
    <label style="width:auto">edge</label><div class="target" id="edgenow"></div>
    <button id="noedge">none</button></div>
  <div class="row"><label>picking</label>
    <select id="target"><option value="fill" selected>the ink</option><option value="edge">the edge</option></select></div>
  <div class="palette" id="palette"></div>
  <div class="row"><button id="addwords" style="width:100%">place it</button></div>
  <div class="hint">
    Click a piece to drop it. Drag to move, corner to resize.<br>
    Arrows nudge, shift+arrows by ten. Delete removes.<br>
    9-slice keeps a frame's corners sharp when stretched.
  </div>
</aside>

<script>
const stage = document.getElementById('stage');
const gridCanvas = document.getElementById('grid');
let A = null, items = [], picked = null, ink = '#ffffff', edge = null, cascade = 0;

// ---- art, fetched once and kept -----------------------------------------
const art = new Map();
function image(src) {
  let img = art.get(src);
  if (!img) {
    img = new Image();
    img.onload = () => items.filter(i => i.src === src).forEach(paint);
    img.src = src;
    art.set(src, img);
  }
  return img;
}

// ---- the one routine that draws an item ---------------------------------
// Used both by an item's own canvas and by the picture that gets saved, so the two cannot drift.
function render(pen, item, atX, atY) {
  const img = image(item.src);
  if (!img.complete || !img.naturalWidth) return;
  pen.globalAlpha = item.o;
  const s = item.slice;
  if (s > 0) {
    const cuts = [[0,0,s,s],[s,0,item.sw-2*s,s],[item.sw-s,0,s,s],
                  [0,s,s,item.sh-2*s],[s,s,item.sw-2*s,item.sh-2*s],[item.sw-s,s,s,item.sh-2*s],
                  [0,item.sh-s,s,s],[s,item.sh-s,item.sw-2*s,s],[item.sw-s,item.sh-s,s,s]];
    const puts = [[0,0,s,s],[s,0,item.w-2*s,s],[item.w-s,0,s,s],
                  [0,s,s,item.h-2*s],[s,s,item.w-2*s,item.h-2*s],[item.w-s,s,s,item.h-2*s],
                  [0,item.h-s,s,s],[s,item.h-s,item.w-2*s,s],[item.w-s,item.h-s,s,s]];
    for (let i = 0; i < 9; i++) {
      const [cx,cy,cw,ch] = cuts[i], [dx,dy,dw,dh] = puts[i];
      if (cw > 0 && ch > 0 && dw > 0 && dh > 0)
        pen.drawImage(img, item.su + cx, item.sv + cy, cw, ch, atX + dx, atY + dy, dw, dh);
    }
  } else {
    pen.drawImage(img, item.su, item.sv, item.sw, item.sh, atX, atY, item.w, item.h);
  }
  pen.globalAlpha = 1;
}

function paint(item) {
  const cut = item.el.firstChild;
  cut.width = Math.max(1, item.w);
  cut.height = Math.max(1, item.h);
  const pen = cut.getContext('2d');
  pen.imageSmoothingEnabled = false;
  pen.clearRect(0, 0, cut.width, cut.height);
  render(pen, item, 0, 0);
}

function lay(item) {
  item.el.style.left = item.x + 'px';
  item.el.style.top = item.y + 'px';
  item.el.style.width = item.w + 'px';
  item.el.style.height = item.h + 'px';
  paint(item);
  if (item === picked) fields();
}

function drop(src, su, sv, sw, sh, name) {
  const el = document.createElement('div');
  el.className = 'item';
  el.append(document.createElement('canvas'));
  const at = 16 + (cascade++ % 12) * 14;
  const item = { el, src, su, sv, sw, sh, name, x: at, y: at, w: sw, h: sh, o: 1, slice: 0 };
  el.onmousedown = e => startDrag(e, item);
  stage.append(el);
  items.push(item);
  pick(item);
  lay(item);
  return item;
}

// ---- the bin -------------------------------------------------------------
function chip(tex, u, v, w, h, label) {
  const cell = document.createElement('div');
  cell.className = 'chip';
  cell.title = `${label}  ${w}×${h}  ${tex}`;
  const seen = document.createElement('i');
  const scale = Math.min(1, 72 / w, 72 / h);
  seen.style.width = Math.max(4, w * scale) + 'px';
  seen.style.height = Math.max(4, h * scale) + 'px';
  seen.style.backgroundImage = `url(/tex?p=${encodeURIComponent(tex)})`;
  seen.style.backgroundPosition = `-${u * scale}px -${v * scale}px`;
  seen.style.backgroundSize = `${A.textures[tex][0] * scale}px ${A.textures[tex][1] * scale}px`;
  cell.append(seen);
  cell.onclick = () => drop(`/tex?p=${encodeURIComponent(tex)}`, u, v, w, h, label);
  return cell;
}

// One picture and the pieces cut out of it, with the whole thing kept apart from its parts: a
// sheet of eight buttons and one of those buttons are not the same kind of thing to reach for.
function sheet(tex, cuts) {
  const box = document.createElement('div');
  box.className = 'sheet';
  box.innerHTML = `<div class="sheetname">${tex.split('/').pop()}
    <span>${A.textures[tex][0]}×${A.textures[tex][1]}${cuts.length ? ' · ' + cuts.length + (cuts.length === 1 ? ' piece' : ' pieces') : ''}</span></div>`;
  const whole = document.createElement('div');
  whole.className = 'shelf';
  whole.append(chip(tex, 0, 0, A.textures[tex][0], A.textures[tex][1], 'whole picture'));
  box.append(whole);
  if (cuts.length) {
    const parts = document.createElement('div');
    parts.className = 'shelf';
    for (const [u, v, w, h] of cuts) parts.append(chip(tex, u, v, w, h, 'piece'));
    box.append(parts);
  }
  return box;
}

function buildBin() {
  const bin = document.getElementById('bin');
  // A window is its pictures; the rectangles are what has been cut out of them. Listing it by its
  // pictures means a window whose parts are cut elsewhere still turns up.
  const byWindow = new Map();
  for (const [win, tex] of A.uses) {
    if (!byWindow.has(win)) byWindow.set(win, new Map());
    if (!byWindow.get(win).has(tex)) byWindow.get(win).set(tex, []);
  }
  for (const [win, tex, u, v, w, h] of A.pieces) {
    if (!byWindow.has(win)) byWindow.set(win, new Map());
    if (!byWindow.get(win).has(tex)) byWindow.get(win).set(tex, []);
    byWindow.get(win).get(tex).push([u, v, w, h]);
  }

  for (const win of A.windows) {
    const used = byWindow.get(win);
    if (!used || !used.size) continue;
    const count = [...used.values()].reduce((n, cuts) => n + cuts.length, 0);
    const box = document.createElement('details');
    // Searched by what it is called and by what its pictures are called, because a picture's name
    // is often the thing being looked for.
    box.dataset.name = (win + ' ' + [...used.keys()].join(' ')).toLowerCase();
    box.innerHTML = `<summary>${win}<span>${used.size} pic · ${count} pieces</span></summary><div class="shelf"></div>`;
    const shelf = box.querySelector('.shelf');
    // Filled only when opened: ten thousand pieces built up front would take a while and most of
    // them are never looked at.
    box.ontoggle = () => {
      if (!box.open || shelf.children.length) return;
      for (const [tex, cuts] of used) shelf.append(sheet(tex, cuts));
    };
    bin.append(box);
  }

  const allBox = document.createElement('details');
  allBox.dataset.name = 'every picture';
  const paths = Object.keys(A.textures).sort();
  allBox.innerHTML = `<summary>Every picture<span>${paths.length}</span></summary><div class="shelf"></div>`;
  const allShelf = allBox.querySelector('.shelf');
  allBox.ontoggle = () => {
    if (!allBox.open || allShelf.children.length) return;
    for (const tex of paths) allShelf.append(chip(tex, 0, 0, A.textures[tex][0], A.textures[tex][1], tex.split('/').pop()));
  };
  bin.append(allBox);

  const iconBox = document.createElement('details');
  iconBox.dataset.name = 'icons';
  iconBox.innerHTML = `<summary>Icons<span>${A.icons.length}</span></summary>
    <div class="row"><input id="iconfind" placeholder="by number"></div><div class="icons"></div>`;
  bin.append(iconBox);
  const iconShelf = iconBox.querySelector('.icons');
  const showIcons = filter => {
    iconShelf.textContent = '';
    for (const id of A.icons.filter(i => !filter || String(i).includes(filter)).slice(0, 300)) {
      const cell = document.createElement('div');
      cell.className = 'ico';
      cell.title = id;
      cell.style.backgroundImage = `url(/icon?id=${id})`;
      cell.onclick = () => drop(`/icon?id=${id}`, 0, 0, 32, 32, 'icon ' + id);
      iconShelf.append(cell);
    }
  };
  iconBox.ontoggle = () => { if (iconBox.open && !iconShelf.children.length) showIcons(''); };
  iconBox.querySelector('#iconfind').oninput = e => showIcons(e.target.value.trim());

  document.getElementById('find').oninput = e => {
    const want = e.target.value.trim().toLowerCase();
    for (const box of bin.querySelectorAll('details'))
      box.style.display = !want || box.dataset.name.includes(want) ? '' : 'none';
  };
}

// ---- words ---------------------------------------------------------------
function placeWords(text, size, colour, rim) {
  const face = A.faces[size];
  let across = 0;
  for (const ch of text) across += (face.glyphs[ch] ?? face.glyphs['?'])[5];

  const cut = document.createElement('canvas');
  cut.width = Math.max(1, across) + 2;
  cut.height = face.lineHeight + 6;

  const sheet = new Image();
  sheet.onload = () => {
    // The letters first, as coverage and nothing else; the colour goes under them afterwards.
    const mask = document.createElement('canvas');
    mask.width = cut.width;
    mask.height = cut.height;
    const stamped = mask.getContext('2d');
    let x = 1;
    for (const ch of text) {
      const [gx, gy, gw, gh, top, adv] = face.glyphs[ch] ?? face.glyphs['?'];
      if (gw > 0) stamped.drawImage(sheet, gx, gy, gw, gh, x, top + 1, gw, gh);
      x += adv;
    }

    const pen = cut.getContext('2d');
    if (rim) {
      // The game draws nearly all its text with an edge under it, and that is most of why flat
      // words never look like the game's. Eight stamps a pixel out is what an edge is.
      for (const [dx, dy] of [[-1,-1],[0,-1],[1,-1],[-1,0],[1,0],[-1,1],[0,1],[1,1]]) pen.drawImage(mask, dx, dy);
      pen.globalCompositeOperation = 'source-in';
      pen.fillStyle = rim;
      pen.fillRect(0, 0, cut.width, cut.height);
      pen.globalCompositeOperation = 'source-over';
    }

    const body = document.createElement('canvas');
    body.width = cut.width;
    body.height = cut.height;
    const under = body.getContext('2d');
    under.drawImage(mask, 0, 0);
    under.globalCompositeOperation = 'source-in';
    under.fillStyle = colour;
    under.fillRect(0, 0, cut.width, cut.height);
    pen.drawImage(body, 0, 0);

    // The words become a picture like any other, so they move, stretch and save the same way.
    drop(cut.toDataURL(), 0, 0, cut.width, cut.height, `"${text}"`);
  };
  sheet.src = `/face?size=${size}`;
}

// ---- the game's own colours ---------------------------------------------
// The whole UIColor table, in whichever of the game's looks is being designed for. Every colour the
// game writes with is in here by its row number, so a colour chosen on the bench is one that can be
// named in the code afterwards rather than matched by eye.
function buildPalette() {
  const grid = document.getElementById('palette');
  const theme = document.getElementById('theme');
  const draw = () => {
    grid.textContent = '';
    for (const colour of A.colours) {
      const css = colour[theme.value];
      const swatch = document.createElement('div');
      swatch.className = 'swatch';
      swatch.title = 'UIColor row ' + colour.row + ' — ' + css;
      const fill = document.createElement('span');
      fill.style.background = css;
      swatch.append(fill);
      swatch.onclick = () => {
        if (document.getElementById('target').value === 'edge') edge = css;
        else ink = css;
        showInks();
      };
      grid.append(swatch);
    }
  };
  theme.onchange = () => { draw(); ink = listText(theme.value); edge = null; showInks(); };
  draw();
  ink = listText(theme.value);
  showInks();
}

// Row 8 is what the game writes its lists in, which is the sensible thing to start on.
function listText(look) {
  const row = A.colours.find(c => c.row === 8);
  return row ? row[look] : '#ffffff';
}

function showInks() {
  document.getElementById('fillnow').style.background = ink;
  document.getElementById('edgenow').style.background = edge || 'transparent';
}

// ---- choosing, dragging, resizing ---------------------------------------
function pick(item) {
  picked?.el.classList.remove('on');
  picked?.el.querySelector('.grip')?.remove();
  picked = item;
  if (item) {
    item.el.classList.add('on');
    const grip = document.createElement('div');
    grip.className = 'grip';
    grip.onmousedown = e => startResize(e, item);
    item.el.append(grip);
  }
  fields();
}

function fields() {
  document.getElementById('what').textContent = picked ? picked.name : 'nothing';
  for (const [id, key] of [['px','x'],['py','y'],['pw','w'],['ph','h']])
    document.getElementById(id).value = picked ? Math.round(picked[key]) : '';
  document.getElementById('po').value = picked ? picked.o * 100 : 100;
  document.getElementById('pslice').value = picked ? picked.slice : 0;
}

function snapped(n) {
  const step = +document.getElementById('gridsize').value || 1;
  return document.getElementById('snap').checked ? Math.round(n / step) * step : Math.round(n);
}

function drags(move) {
  const stop = () => { removeEventListener('mousemove', move); removeEventListener('mouseup', stop); };
  addEventListener('mousemove', move);
  addEventListener('mouseup', stop);
}

function startDrag(e, item) {
  if (e.target.classList.contains('grip')) return;
  e.preventDefault();
  pick(item);
  const from = { x: e.clientX, y: e.clientY, ix: item.x, iy: item.y };
  drags(m => {
    item.x = snapped(from.ix + m.clientX - from.x);
    item.y = snapped(from.iy + m.clientY - from.y);
    lay(item);
  });
}

function startResize(e, item) {
  e.preventDefault();
  e.stopPropagation();
  const from = { x: e.clientX, y: e.clientY, w: item.w, h: item.h };
  drags(m => {
    item.w = Math.max(2, snapped(from.w + m.clientX - from.x));
    item.h = Math.max(2, snapped(from.h + m.clientY - from.y));
    lay(item);
  });
}

stage.onmousedown = e => { if (e.target === stage || e.target === gridCanvas) pick(null); };

for (const [id, key] of [['px','x'],['py','y'],['pw','w'],['ph','h']])
  document.getElementById(id).oninput = e => { if (picked) { picked[key] = +e.target.value; lay(picked); } };
document.getElementById('po').oninput = e => { if (picked) { picked.o = e.target.value / 100; lay(picked); } };
document.getElementById('pslice').oninput = e => { if (picked) { picked.slice = +e.target.value; lay(picked); } };
document.getElementById('actual').onclick = () => { if (picked) { picked.w = picked.sw; picked.h = picked.sh; lay(picked); } };

addEventListener('keydown', e => {
  if (!picked || /input|select|textarea/i.test(e.target.tagName)) return;
  const step = e.shiftKey ? 10 : 1;
  if (e.key === 'Delete' || e.key === 'Backspace') {
    picked.el.remove();
    items = items.filter(i => i !== picked);
    pick(null);
    e.preventDefault();
    return;
  }
  if (e.key === 'ArrowLeft') picked.x -= step;
  else if (e.key === 'ArrowRight') picked.x += step;
  else if (e.key === 'ArrowUp') picked.y -= step;
  else if (e.key === 'ArrowDown') picked.y += step;
  else return;
  e.preventDefault();
  lay(picked);
});

// ---- stage, order, saving ------------------------------------------------
function resize() {
  const w = +document.getElementById('sw').value, h = +document.getElementById('sh').value;
  stage.style.width = w + 'px';
  stage.style.height = h + 'px';
  gridCanvas.width = w; gridCanvas.height = h;
  const pen = gridCanvas.getContext('2d');
  pen.clearRect(0, 0, w, h);
  if (!document.getElementById('showgrid').checked) return;
  const step = (+document.getElementById('gridsize').value || 4) * 8;
  pen.strokeStyle = '#ffffff'; pen.lineWidth = 1;
  for (let x = step; x < w; x += step) { pen.beginPath(); pen.moveTo(x + .5, 0); pen.lineTo(x + .5, h); pen.stroke(); }
  for (let y = step; y < h; y += step) { pen.beginPath(); pen.moveTo(0, y + .5); pen.lineTo(w, y + .5); pen.stroke(); }
}
['sw','sh','gridsize','showgrid'].forEach(id => document.getElementById(id).oninput = resize);

document.getElementById('front').onclick = () => {
  if (!picked) return;
  stage.append(picked.el);
  items = items.filter(i => i !== picked).concat(picked);
};
document.getElementById('back').onclick = () => {
  if (!picked) return;
  stage.insertBefore(picked.el, gridCanvas.nextSibling);
  items = [picked].concat(items.filter(i => i !== picked));
};
document.getElementById('wipe').onclick = () => { items.forEach(i => i.el.remove()); items = []; pick(null); };
document.getElementById('dupe').onclick = () => {
  if (!picked) return;
  const from = picked;
  const copy = drop(from.src, from.su, from.sv, from.sw, from.sh, from.name);
  Object.assign(copy, { w: from.w, h: from.h, o: from.o, slice: from.slice, x: from.x + 12, y: from.y + 12 });
  lay(copy);
};
document.getElementById('noedge').onclick = () => { edge = null; showInks(); };
document.getElementById('addwords').onclick = () => {
  const text = document.getElementById('words').value;
  if (text) placeWords(text, document.getElementById('wordsize').value, ink, edge);
};

document.getElementById('shot').onclick = () => {
  const w = +document.getElementById('sw').value, h = +document.getElementById('sh').value;
  const shot = document.createElement('canvas');
  shot.width = w; shot.height = h;
  const pen = shot.getContext('2d');
  pen.imageSmoothingEnabled = false;
  pen.fillStyle = '#0d0f12';
  pen.fillRect(0, 0, w, h);
  for (const item of items) render(pen, item, item.x, item.y);
  shot.toBlob(blob => {
    const link = document.createElement('a');
    link.download = 'wayfarer-design.png';
    link.href = URL.createObjectURL(blob);
    link.click();
  });
};

// ---- go ------------------------------------------------------------------
fetch('/catalogue.json').then(r => r.json()).then(loaded => {
  A = loaded;
  buildPalette();
  buildBin();
  resize();
});
</script>
</body>
</html>
""";
}
