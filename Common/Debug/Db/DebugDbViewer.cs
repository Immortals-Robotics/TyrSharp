using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;

namespace Tyr.Common.Debug.Db;

/// <summary>
/// Embeddable HTTP server that exposes the DebugDatabase as a browsable table UI.
/// Uses HttpListener — no ASP.NET Core dependency.
///
/// Usage:
///   var db = new DebugDatabase("/tmp/debug_session");
///   var viewer = new DebugDbViewer(db, port: 9000)
///       .Register&lt;LogEntry&gt;()
///       .Register&lt;DrawEntry&gt;();
///   viewer.Start();
///   // Open http://localhost:9000 in a browser
/// </summary>
public sealed class DebugDbViewer : IDisposable
{
    private readonly DebugDb _db;
    private readonly int _port;
    private readonly Dictionary<string, RegisteredType> _types = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Thread? _thread;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public DebugDbViewer(DebugDb db, int port = 9000)
    {
        _db = db;
        _port = port;
    }

    public DebugDbViewer Register<T>() where T : IEntry
    {
        _db.RegisterType<T>();

        var name = GetRegisteredTypeName(typeof(T));
        _types[name] = new RegisteredType
        {
            Name = name,
            Query = (module, t0, t1) =>
            {
                IEnumerable<T> results = module is null
                    ? _db.QueryAll<T>(t0, t1)
                    : _db.Query<T>(module, t0, t1);
                return results.Select(EntryToRow);
            },
            GetFields = GetFieldNames<T>,
        };
        return this;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();

        // Try wildcard first, fall back to localhost if it fails (needs elevation on Windows)
        try
        {
            _listener.Prefixes.Add($"http://+:{_port}/");
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            _listener.Close();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();
        }

        _thread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "DebugDbViewer",
        };
        _thread.Start();
    }

    private void ListenLoop()
    {
        while (_cts is { IsCancellationRequested: false })
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener!.GetContext();
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }

            try
            {
                HandleRequest(ctx);
            }
            catch (Exception ex)
            {
                try { Respond(ctx, 500, "text/plain", Encoding.UTF8.GetBytes(ex.ToString())); }
                catch { /* swallow */ }
            }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var qs = ctx.Request.QueryString;

        switch (path)
        {
            case "/":
                Respond(ctx, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(HtmlPage()));
                break;

            case "/api/types":
                RespondJson(ctx, _types.Keys.ToArray());
                break;

            case "/api/fields":
            {
                var type = qs["type"];
                if (type is null || !_types.TryGetValue(type, out var reg))
                { Respond(ctx, 404, "text/plain", "not found"u8.ToArray()); break; }
                RespondJson(ctx, reg.GetFields());
                break;
            }

            case "/api/query":
            {
                var type = qs["type"];
                if (type is null || !_types.TryGetValue(type, out var reg))
                { Respond(ctx, 404, "text/plain", "not found"u8.ToArray()); break; }

                var module = qs["module"];
                if (string.IsNullOrEmpty(module)) module = null;

                var t0    = long.TryParse(qs["t0"], out var v0) ? v0 : 0L;
                var t1    = long.TryParse(qs["t1"], out var v1) ? v1 : long.MaxValue;
                var limit = int.TryParse(qs["limit"], out var lv) ? lv : 10_000;

                var sw = Stopwatch.StartNew();
                var rows = reg.Query(module, Timestamp.FromNanoseconds(t0), Timestamp.FromNanoseconds(t1)).Take(limit).ToArray();
                sw.Stop();
                RespondJson(ctx, rows, sw.Elapsed.TotalMilliseconds);
                break;
            }

            case "/api/frames":
            {
                var module = qs["module"];
                if (string.IsNullOrEmpty(module))
                { Respond(ctx, 400, "text/plain", "module required"u8.ToArray()); break; }

                var t0    = long.TryParse(qs["t0"], out var v0) ? v0 : 0L;
                var t1    = long.TryParse(qs["t1"], out var v1) ? v1 : long.MaxValue;
                var limit = int.TryParse(qs["limit"], out var lv) ? lv : 10_000;

                var sw = Stopwatch.StartNew();
                var frames = _db.QueryFrames(module, Timestamp.FromNanoseconds(t0), Timestamp.FromNanoseconds(t1))
                    .Take(limit)
                    .Select(f => new { moduleName = f.ModuleName, startTimestamp = f.StartTimestamp.Nanoseconds })
                    .ToArray();
                sw.Stop();
                RespondJson(ctx, frames, sw.Elapsed.TotalMilliseconds);
                break;
            }

            case "/api/frame-at":
            {
                var module = qs["module"];
                if (string.IsNullOrEmpty(module))
                { Respond(ctx, 400, "text/plain", "module required"u8.ToArray()); break; }

                if (!long.TryParse(qs["t"], out var t))
                { Respond(ctx, 400, "text/plain", "t required"u8.ToArray()); break; }

                var sw = Stopwatch.StartNew();
                var result = _db.GetFrameAt(module, Timestamp.FromNanoseconds(t));
                sw.Stop();
                if (result is null)
                { RespondJson(ctx, new { found = false }, sw.Elapsed.TotalMilliseconds); break; }

                var (start, end) = result.Value;
                RespondJson(ctx, new { found = true, start = start.Nanoseconds, end = end.Nanoseconds }, sw.Elapsed.TotalMilliseconds);
                break;
            }

            default:
                Respond(ctx, 404, "text/plain", "not found"u8.ToArray());
                break;
        }
    }

    private static void Respond(HttpListenerContext ctx, int status, string contentType, byte[] body)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = body.Length;
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Response.OutputStream.Write(body);
        ctx.Response.Close();
    }

    private static void RespondJson(HttpListenerContext ctx, object data, double? queryMs = null)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data, JsonOpts);
        if (queryMs.HasValue)
            ctx.Response.Headers.Add("X-Query-Ms", queryMs.Value.ToString("F3"));
        Respond(ctx, 200, "application/json", json);
    }

    private static string[] GetFieldNames<T>()
    {
        return typeof(T).GetProperties()
            .Where(p => p.Name != nameof(IEntry.Meta))
            .Select(p => p.Name)
            .Concat(["Module", "Layer", "File", "Member", "Line", "Expression"])
            .ToArray();
    }

    private static Dictionary<string, object?> EntryToRow<T>(T entry) where T : IEntry
    {
        var row = new Dictionary<string, object?>();
        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.Name == nameof(IEntry.Meta))
            {
                var src = entry.Meta;
                row["Module"]     = src.Module;
                row["Layer"]      = src.Layer;
                row["File"]       = src.File;
                row["Member"]     = src.Member;
                row["Line"]       = src.Line;
                row["Expression"] = src.Expression;
            }
            else
            {
                row[prop.Name] = NormalizeCellValue(prop.GetValue(entry));
            }
        }
        return row;
    }

    private static string GetRegisteredTypeName(Type type)
    {
        const string debugNamespacePrefix = "Tyr.Common.Debug.";

        if (type.FullName is { } fullName && fullName.StartsWith(debugNamespacePrefix, StringComparison.Ordinal))
            return fullName[debugNamespacePrefix.Length..];

        return type.FullName ?? type.Name;
    }

    private static object? NormalizeCellValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or Half or float or double or decimal => value,
            Enum e => e.ToString(),
            Timestamp timestamp => timestamp.Nanoseconds,
            DeltaTime deltaTime => deltaTime.Nanoseconds,
            Vector2 vector2 => FormatVector(vector2.X, vector2.Y),
            Vector3 vector3 => FormatVector(vector3.X, vector3.Y, vector3.Z),
            PlotValue plotValue => FormatPlotValue(plotValue),
            _ => value.ToString()
        };
    }

    private static object? FormatPlotValue(PlotValue value)
    {
        return value.Kind switch
        {
            PlotValueKind.None => null,
            PlotValueKind.Number => value.Number,
            PlotValueKind.Boolean => value.Boolean,
            PlotValueKind.Vector2 => FormatVector(value.X, value.Y),
            PlotValueKind.Vector3 => FormatVector(value.X, value.Y, value.Z),
            _ => value.ToString()
        };
    }

    private static string FormatVector(float x, float y) => $"({x}, {y})";

    private static string FormatVector(float x, float y, float z) => $"({x}, {y}, {z})";

    private sealed class RegisteredType
    {
        public required string Name { get; init; }
        public required Func<string?, Timestamp, Timestamp, IEnumerable<Dictionary<string, object?>>> Query { get; init; }
        public required Func<string[]> GetFields { get; init; }
    }

    private string HtmlPage() => """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>DebugDb Viewer</title>
<style>
  @import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;700&family=IBM+Plex+Sans:wght@400;500;600&display=swap');

  :root {
    --bg:         #0c0c0e;
    --bg-surface: #141418;
    --bg-raised:  #1c1c22;
    --bg-hover:   #24242c;
    --border:     #2a2a35;
    --border-dim: #1e1e28;
    --text:       #e0e0e6;
    --text-dim:   #7a7a8c;
    --text-muted: #50505e;
    --accent:     #6ee7b7;
    --accent-dim: #2d6b55;
    --blue:       #60a5fa;
    --mono:       'JetBrains Mono', monospace;
    --sans:       'IBM Plex Sans', system-ui, sans-serif;
  }

  * { margin: 0; padding: 0; box-sizing: border-box; }

  body {
    font-family: var(--sans);
    background: var(--bg);
    color: var(--text);
    font-size: 13px;
    line-height: 1.5;
  }

  .app {
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
  }

  header {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 16px;
    background: var(--bg-surface);
    border-bottom: 1px solid var(--border-dim);
    flex-shrink: 0;
  }

  header h1 {
    font-family: var(--mono);
    font-size: 14px;
    font-weight: 700;
    color: var(--accent);
    letter-spacing: -0.5px;
  }

  header h1 span { color: var(--text-dim); font-weight: 400; }

  .toolbar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;
    background: var(--bg-surface);
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
    flex-wrap: wrap;
  }

  .toolbar label {
    font-size: 11px;
    color: var(--text-dim);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    font-weight: 600;
  }

  .toolbar select, .toolbar input {
    font-family: var(--mono);
    font-size: 12px;
    padding: 5px 8px;
    background: var(--bg-raised);
    border: 1px solid var(--border);
    border-radius: 4px;
    color: var(--text);
    outline: none;
  }

  .toolbar select:focus, .toolbar input:focus { border-color: var(--accent-dim); }
  .toolbar input::placeholder { color: var(--text-muted); }
  .toolbar select { cursor: pointer; }
  .toolbar .sep { width: 1px; height: 20px; background: var(--border); margin: 0 4px; }

  button.query-btn {
    font-family: var(--mono);
    font-size: 12px;
    font-weight: 600;
    padding: 5px 14px;
    background: var(--accent-dim);
    color: var(--accent);
    border: 1px solid var(--accent-dim);
    border-radius: 4px;
    cursor: pointer;
  }

  button.query-btn:hover { background: #347a60; border-color: var(--accent); }

  .tabs {
    display: flex;
    gap: 0;
    padding: 0 16px;
    background: var(--bg-surface);
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
  }

  .tab {
    font-family: var(--mono);
    font-size: 12px;
    font-weight: 500;
    padding: 8px 16px;
    background: none;
    border: none;
    border-bottom: 2px solid transparent;
    color: var(--text-dim);
    cursor: pointer;
  }

  .tab:hover { color: var(--text); }
  .tab.active { color: var(--accent); border-bottom-color: var(--accent); }

  .tab-panel { display: none; flex-direction: column; flex: 1; overflow: hidden; }
  .tab-panel.active { display: flex; }

  .frame-lookup {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;
    background: var(--bg-raised);
    border-bottom: 1px solid var(--border-dim);
  }

  .frame-lookup label { font-size: 11px; color: var(--text-dim); text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600; }
  .frame-lookup input { font-family: var(--mono); font-size: 12px; padding: 5px 8px; background: var(--bg); border: 1px solid var(--border); border-radius: 4px; color: var(--text); outline: none; }
  .frame-lookup input:focus { border-color: var(--accent-dim); }

  .frame-result {
    padding: 8px 16px;
    font-family: var(--mono);
    font-size: 12px;
    color: var(--text-dim);
    background: var(--bg-surface);
    border-bottom: 1px solid var(--border-dim);
  }

  .frame-result .found { color: var(--accent); }

  .status {
    margin-left: auto;
    font-family: var(--mono);
    font-size: 11px;
    color: var(--text-muted);
  }

  .status .count { color: var(--accent); }

  .table-wrap { flex: 1; overflow: auto; }

  table {
    width: 100%;
    border-collapse: collapse;
    font-family: var(--mono);
    font-size: 12px;
  }

  thead { position: sticky; top: 0; z-index: 10; }

  th {
    background: var(--bg-raised);
    padding: 7px 10px;
    text-align: left;
    font-weight: 600;
    font-size: 11px;
    color: var(--text-dim);
    text-transform: uppercase;
    letter-spacing: 0.3px;
    border-bottom: 2px solid var(--border);
    white-space: nowrap;
    user-select: none;
    cursor: pointer;
  }

  th:hover { color: var(--text); }

  td {
    padding: 4px 10px;
    border-bottom: 1px solid var(--border-dim);
    white-space: nowrap;
    max-width: 500px;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  tr:hover td { background: var(--bg-hover); }
  td:first-child { color: var(--blue); }

  .empty {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    color: var(--text-muted);
    font-size: 14px;
    font-family: var(--sans);
  }

  .filter-input {
    font-family: var(--mono);
    font-size: 11px;
    padding: 3px 6px;
    background: var(--bg);
    border: 1px solid var(--border-dim);
    border-radius: 3px;
    color: var(--text);
    width: 100%;
    outline: none;
  }

  .filter-input:focus { border-color: var(--accent-dim); }
  th .filter-input { margin-top: 4px; display: block; }

  ::-webkit-scrollbar { width: 8px; height: 8px; }
  ::-webkit-scrollbar-track { background: var(--bg); }
  ::-webkit-scrollbar-thumb { background: var(--border); border-radius: 4px; }
  ::-webkit-scrollbar-thumb:hover { background: var(--text-muted); }
</style>
</head>
<body>
<div class="app">
  <header>
    <h1>DebugDb<span> viewer</span></h1>
  </header>

  <div class="tabs">
    <button class="tab active" data-tab="entries">Entries</button>
    <button class="tab" data-tab="frames">Frames</button>
  </div>

  <div class="tab-panel active" id="panel-entries">
    <div class="toolbar">
      <label for="type-sel">Type</label>
      <select id="type-sel"></select>
      <div class="sep"></div>

      <label for="mod-input">Module</label>
      <input id="mod-input" type="text" placeholder="all" style="width:130px">
      <div class="sep"></div>

      <label for="t0-input">t0</label>
      <input id="t0-input" type="text" placeholder="0" style="width:120px">
      <label for="t1-input">t1</label>
      <input id="t1-input" type="text" placeholder="max" style="width:120px">
      <div class="sep"></div>

      <label for="limit-input">Limit</label>
      <input id="limit-input" type="text" placeholder="10000" style="width:80px" value="10000">

      <button class="query-btn" id="query-btn">Query</button>
      <div class="status" id="status"></div>
    </div>

    <div class="table-wrap" id="table-wrap">
      <div class="empty" id="empty-msg">Select a type and click Query</div>
    </div>
  </div>

  <div class="tab-panel" id="panel-frames">
    <div class="toolbar">
      <label for="fr-mod-input">Module</label>
      <input id="fr-mod-input" type="text" placeholder="required" style="width:130px">
      <div class="sep"></div>

      <label for="fr-t0-input">t0</label>
      <input id="fr-t0-input" type="text" placeholder="0" style="width:120px">
      <label for="fr-t1-input">t1</label>
      <input id="fr-t1-input" type="text" placeholder="max" style="width:120px">
      <div class="sep"></div>

      <label for="fr-limit-input">Limit</label>
      <input id="fr-limit-input" type="text" placeholder="10000" style="width:80px" value="10000">

      <button class="query-btn" id="fr-query-btn">Query Frames</button>
      <div class="status" id="fr-status"></div>
    </div>

    <div class="frame-lookup">
      <label for="fr-at-mod">Module</label>
      <input id="fr-at-mod" type="text" placeholder="required" style="width:130px">
      <div class="sep"></div>
      <label for="fr-at-t">Timestamp</label>
      <input id="fr-at-t" type="text" placeholder="nanoseconds" style="width:160px">
      <button class="query-btn" id="fr-at-btn">Find Frame</button>
      <div class="frame-result" id="fr-at-result"></div>
    </div>

    <div class="table-wrap" id="fr-table-wrap">
      <div class="empty">Enter a module name and click Query Frames</div>
    </div>
  </div>
</div>

<script>
const $ = s => document.querySelector(s);
const $$ = s => document.querySelectorAll(s);
async function api(path) {
  const r = await fetch(path);
  const data = await r.json();
  data._queryMs = r.headers.get('X-Query-Ms');
  return data;
}

// ─── Tabs ──────────────────────────────────────────────────────────────
$$('.tab').forEach(tab => {
  tab.addEventListener('click', () => {
    $$('.tab').forEach(t => t.classList.remove('active'));
    $$('.tab-panel').forEach(p => p.classList.remove('active'));
    tab.classList.add('active');
    $('#panel-' + tab.dataset.tab).classList.add('active');
  });
});

// ─── Entries tab ───────────────────────────────────────────────────────
let fields = [], rows = [], sortCol = null, sortAsc = true, colFilters = {};

async function init() {
  const types = await api('/api/types');
  const sel = $('#type-sel');
  types.forEach(t => {
    const o = document.createElement('option');
    o.value = t; o.textContent = t;
    sel.appendChild(o);
  });
  $('#query-btn').onclick = runQuery;
  $('#panel-entries .toolbar').querySelectorAll('input').forEach(el => {
    el.addEventListener('keydown', e => { if (e.key === 'Enter') runQuery(); });
  });

  // Frames tab
  $('#fr-query-btn').onclick = runFramesQuery;
  $('#fr-at-btn').onclick = runFrameAt;
  $('#panel-frames .toolbar').querySelectorAll('input').forEach(el => {
    el.addEventListener('keydown', e => { if (e.key === 'Enter') runFramesQuery(); });
  });
  $('#fr-at-mod').addEventListener('keydown', e => { if (e.key === 'Enter') runFrameAt(); });
  $('#fr-at-t').addEventListener('keydown', e => { if (e.key === 'Enter') runFrameAt(); });
}

async function runQuery() {
  const type = $('#type-sel').value;
  if (!type) return;

  const module = $('#mod-input').value || '';
  const t0 = $('#t0-input').value || '0';
  const t1 = $('#t1-input').value || '9223372036854775807';
  const limit = $('#limit-input').value || '10000';

  const params = new URLSearchParams({ type, t0, t1, limit });
  if (module) params.set('module', module);

  const ts = performance.now();
  [fields, rows] = await Promise.all([
    api('/api/fields?type=' + type),
    api('/api/query?' + params),
  ]);
  const elapsed = (performance.now() - ts).toFixed(0);

  const serverMs = rows._queryMs;
  delete rows._queryMs;

  colFilters = {};
  sortCol = null;
  renderTable();
  $('#status').innerHTML = '<span class="count">' + rows.length + '</span> rows · query ' + serverMs + 'ms · total ' + elapsed + 'ms';
}

function renderTable() {
  const wrap = $('#table-wrap');
  const empty = $('#empty-msg');
  if (empty) empty.remove();

  let filtered = rows;
  for (const [col, val] of Object.entries(colFilters)) {
    if (!val) continue;
    const lv = val.toLowerCase();
    filtered = filtered.filter(r => {
      const c = r[col];
      return c !== null && c !== undefined && String(c).toLowerCase().includes(lv);
    });
  }

  if (sortCol !== null) {
    const key = fields[sortCol];
    filtered = [...filtered].sort((a, b) => {
      const va = a[key], vb = b[key];
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      if (typeof va === 'number' && typeof vb === 'number')
        return sortAsc ? va - vb : vb - va;
      return sortAsc ? String(va).localeCompare(String(vb)) : String(vb).localeCompare(String(va));
    });
  }

  let h = '<table><thead><tr>';
  fields.forEach((f, i) => {
    const arrow = sortCol === i ? (sortAsc ? ' ↑' : ' ↓') : '';
    h += '<th data-idx="' + i + '">' + f + arrow +
         '<input class="filter-input" data-col="' + f +
         '" placeholder="filter…" value="' + (colFilters[f] || '') + '"></th>';
  });
  h += '</tr></thead><tbody>';

  for (const row of filtered) {
    h += '<tr>';
    for (const f of fields) {
      const v = row[f];
      h += '<td data-col="' + f + '">' + esc(v === null || v === undefined ? '' : String(v)) + '</td>';
    }
    h += '</tr>';
  }
  h += '</tbody></table>';
  wrap.innerHTML = h;

  wrap.querySelectorAll('th').forEach(th => {
    th.addEventListener('click', e => {
      if (e.target.classList.contains('filter-input')) return;
      const idx = parseInt(th.dataset.idx);
      if (sortCol === idx) sortAsc = !sortAsc;
      else { sortCol = idx; sortAsc = true; }
      renderTable();
    });
  });

  wrap.querySelectorAll('.filter-input').forEach(inp => {
    inp.addEventListener('input', () => {
      colFilters[inp.dataset.col] = inp.value;
      renderTable();
      const el = wrap.querySelector('.filter-input[data-col="' + inp.dataset.col + '"]');
      if (el) { el.focus(); el.selectionStart = el.selectionEnd = el.value.length; }
    });
    inp.addEventListener('click', e => e.stopPropagation());
  });
}

// ─── Frames tab ────────────────────────────────────────────────────────
let frFrames = [], frSortCol = null, frSortAsc = true;
const frFields = ['moduleName', 'startTimestamp'];

async function runFramesQuery() {
  const module = $('#fr-mod-input').value;
  if (!module) return;

  const t0 = $('#fr-t0-input').value || '0';
  const t1 = $('#fr-t1-input').value || '9223372036854775807';
  const limit = $('#fr-limit-input').value || '10000';

  const params = new URLSearchParams({ module, t0, t1, limit });
  const ts = performance.now();
  frFrames = await api('/api/frames?' + params);
  const elapsed = (performance.now() - ts).toFixed(0);

  const serverMs = frFrames._queryMs;
  delete frFrames._queryMs;

  frSortCol = null;
  renderFramesTable();
  $('#fr-status').innerHTML = '<span class="count">' + frFrames.length + '</span> frames · query ' + serverMs + 'ms · total ' + elapsed + 'ms';
}

function renderFramesTable() {
  const wrap = $('#fr-table-wrap');
  wrap.querySelectorAll('.empty')?.forEach(e => e.remove());

  let data = [...frFrames];
  if (frSortCol !== null) {
    const key = frFields[frSortCol];
    data.sort((a, b) => {
      const va = a[key], vb = b[key];
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      if (typeof va === 'number' && typeof vb === 'number')
        return frSortAsc ? va - vb : vb - va;
      return frSortAsc ? String(va).localeCompare(String(vb)) : String(vb).localeCompare(String(va));
    });
  }

  let h = '<table><thead><tr>';
  h += '<th>#</th>';
  h += '<th data-idx="0">Module' + (frSortCol === 0 ? (frSortAsc ? ' ↑' : ' ↓') : '') + '</th>';
  h += '<th data-idx="1">Start' + (frSortCol === 1 ? (frSortAsc ? ' ↑' : ' ↓') : '') + '</th>';
  h += '<th>Duration (ms)</th>';
  h += '</tr></thead><tbody>';

  for (let idx = 0; idx < data.length; idx++) {
    const f = data[idx];
    const durNs = idx + 1 < data.length ? data[idx + 1].startTimestamp - f.startTimestamp : null;
    const dur = durNs !== null ? fmtDurMs(durNs) : '';
    h += '<tr><td>' + idx + '</td><td>' + esc(f.moduleName) + '</td><td title="' + f.startTimestamp + ' ns">' + fmtTs(f.startTimestamp) + '</td><td>' + dur + '</td></tr>';
  }
  h += '</tbody></table>';
  wrap.innerHTML = h;

  wrap.querySelectorAll('th[data-idx]').forEach(th => {
    th.addEventListener('click', () => {
      const idx = parseInt(th.dataset.idx);
      if (frSortCol === idx) frSortAsc = !frSortAsc;
      else { frSortCol = idx; frSortAsc = true; }
      renderFramesTable();
    });
  });
}

async function runFrameAt() {
  const module = $('#fr-at-mod').value;
  const t = $('#fr-at-t').value;
  if (!module || !t) return;

  const result = await api('/api/frame-at?module=' + encodeURIComponent(module) + '&t=' + t);
  const el = $('#fr-at-result');
  const qMs = result._queryMs ? ' (query ' + result._queryMs + 'ms)' : '';
  if (result.found) {
    const durMs = fmtDurMs(result.end - result.start);
    el.innerHTML = '<span class="found">start:</span> ' + fmtTs(result.start)
      + ' <span class="found">end:</span> ' + fmtTs(result.end)
      + ' <span class="found">duration:</span> ' + durMs + ' ms' + qMs;
  } else {
    el.textContent = 'No frame found at that timestamp' + qMs;
  }
}

function esc(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }

// Nanoseconds since Unix epoch → "HH:MM:SS.mmm"
function fmtTs(ns) {
  const ms = Number(BigInt(ns) / 1000000n);
  const d = new Date(ms);
  const hh = String(d.getHours()).padStart(2, '0');
  const mm = String(d.getMinutes()).padStart(2, '0');
  const ss = String(d.getSeconds()).padStart(2, '0');
  const ml = String(d.getMilliseconds()).padStart(3, '0');
  return hh + ':' + mm + ':' + ss + '.' + ml;
}

// Duration in nanoseconds → milliseconds string
function fmtDurMs(ns) { return (ns / 1e6).toFixed(2); }

init();
</script>
</body>
</html>
""";

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
    }
}
