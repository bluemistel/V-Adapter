# DropPayload Specification (v1)

> 日本語版: [drop-payload-spec.md](drop-payload-spec.md)

## 1. Overview & Design Philosophy

V-Adapter detects voice-synth exports (a `*.wav` paired with a same-named `*.txt`) and drops them
onto a video editor's timeline. To **decouple editor-specific knowledge from the core**, the
detection result is normalized into an **editor-neutral contract, `DropPayload`**, and handed to a
**pluggable adapter (`IImportAdapter`)**.

```
[Watcher WavTxtWatcher] → [Speaker routing DropRouting] → DropPayload (neutral contract)
                                                          → IImportAdapter (pluggable)
                                                            ├ built-in gcmz (AviUtl,  v1)
                                                            ├ built-in gcmz (AviUtl2, v2)
                                                            └ external command (payload.json + run command)
```

Goals:

- **Loose coupling** — the core only does "detect → normalize → hand off"; it holds no
  editor-specific operations.
- **Easy extension** — support for a new editor (Premiere / DaVinci, etc.) can be added by a
  volunteer as an **out-of-tree external adapter**, with no change to the core.
- **Intent-based** — the payload carries intent, not fps-dependent concrete values (e.g. frame
  counts). The adapter computes concrete numbers itself.

## 2. `DropPayload` v1 Schema

Serialized with `System.Text.Json` (camelCase, enums as strings, nulls omitted). Passed to external
adapters as a UTF-8 `payload.json`.

| Field | Type | Req. | Description |
|-------|------|:--:|------|
| `schemaVersion` | int | ✓ | Schema version. Currently `1`. |
| `source` | object | ✓ | Source app info (below). |
| `source.app` | string | ✓ | Source app name. Always `"V-Adapter"`. |
| `source.version` | string |  | Source app version. |
| `files` | array | ✓ | Role-tagged files (below). One or more. |
| `files[].role` | string | ✓ | `"audio"` / `"subtitle"`. |
| `files[].path` | string | ✓ | Full path of the file. |
| `files[].encoding` | string |  | Text encoding when known (e.g. `"utf-8"`). |
| `speaker` | string |  | Resolved speaker name. |
| `routing` | object | ✓ | Placement hint (below). |
| `routing.trackHint` | int? |  | Desired track (layer) number. `null` defers to adapter default. |
| `timing` | object | ✓ | Timing intent (below). |
| `timing.advanceToItemEnd` | bool | ✓ | Intent to advance the cursor/seek to the end of the inserted item after dropping. |

### Example JSON

```json
{
  "schemaVersion": 1,
  "source": { "app": "V-Adapter", "version": "0.0.2" },
  "files": [
    { "role": "audio", "path": "C:\\out\\0001_zundamon_hello.wav" },
    { "role": "subtitle", "path": "C:\\out\\0001_zundamon_hello.txt" }
  ],
  "speaker": "zundamon",
  "routing": { "trackHint": 5 },
  "timing": { "advanceToItemEnd": true }
}
```

## 3. Adapter Contract

### 3.1 Built-in gcmz adapter (reference: mapping rules)

The built-in `GcmzImportAdapter` maps the neutral payload onto the Gochamaze Drops external API
parameters. The mapping is consolidated into the pure function `GcmzPayloadMapper`.

| Payload | gcmz parameter |
|---------|----------------|
| `files[].path` (all) | `files` (array of full paths) |
| `routing.trackHint` (null → default layer) | `layer` |
| `timing.advanceToItemEnd` + audio duration + project fps | `frameAdvance` (= ceil(seconds × fps)); falls back to manual value if not computable |
| (AviUtl2 only) configured margin | `margin` |
| AviUtl=1 / AviUtl2=2 | `COPYDATASTRUCT.dwData` |

A gcmz-specific constraint: paths in `files` may only contain **characters representable in
Shift-JIS** (paths with emoji, etc. are unsupported).

### 3.2 External command adapter

V-Adapter writes the neutral payload to a temporary `payload.json` (UTF-8, no BOM) and runs the
configured **command template**.

- **Invocation**: `{payload}` in the template is replaced with the full path of `payload.json`.
  Paths with spaces are quoted automatically. The first token is the executable, the rest are
  arguments.
  - e.g. `python davinci_import.py {payload}`
  - e.g. `import_to_myapp.exe {payload}`
  - e.g. `"C:\Program Files\app\run.exe" --in {payload}`
- **Working directory / environment**: inherited from the parent process (V-Adapter).
- **Encoding**: `payload.json` is UTF-8 (no BOM). stdout/stderr are read as UTF-8.
- **Exit code**: `0` is success, non-zero is failure.
- **stdout JSON (optional)**: if the following JSON appears at the end of stdout, it takes
  precedence over the exit code.
  ```json
  { "success": true, "message": "1 item inserted" }
  ```
  If `success` is `false`, it is treated as a failure regardless of the exit code (`message` is
  surfaced as the error).
- **Timeout**: configurable (default 15000ms). On timeout the process tree is killed and the import
  fails.
- **Cleanup**: `payload.json` is deleted after the run. If the adapter needs to keep the contents,
  copy it immediately at startup.

### 3.3 Minimal reference adapter (pseudocode / Python)

```python
import json, sys

# V-Adapter runs: <this script> <path-to-payload.json>
with open(sys.argv[1], encoding="utf-8") as f:
    payload = json.load(f)

audio = next(x["path"] for x in payload["files"] if x["role"] == "audio")
subtitle = next((x["path"] for x in payload["files"] if x["role"] == "subtitle"), None)
track = payload["routing"].get("trackHint")

# Implement the actual import via the target editor's API / scripting here
# ... import_into_editor(audio, subtitle, track, payload["timing"]["advanceToItemEnd"]) ...

print(json.dumps({"success": True, "message": "ok"}))
sys.exit(0)
```

## 4. Versioning Policy

- Bump `schemaVersion` only for breaking changes (do not bump for backward-compatible changes such
  as adding fields).
- Adapters may ignore unknown fields. If `schemaVersion` is newer than expected, either process what
  can be interpreted, or explicitly fail (`success:false`).

## 5. Out of Scope (Future)

- Long-running stdio JSON-RPC adapters (for editors with heavy connection setup). Currently
  one-shot execution only.
- Concrete Premiere / DaVinci adapter bodies (to be authored and distributed out-of-tree by
  volunteers).
