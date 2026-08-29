# Electron shell: threat model

Scope: `apps/studio`'s desktop shell (`electron/`), and the local web app it
loads (`server.ts` and everything under `src/`) when run in desktop mode
(`NEXT_PUBLIC_MODELLER_STUDIO_MODE` unset — not `playground`). Written after
the second-instance workspace-routing work landed; revisit if the shell's
process model changes materially (new windows, a remote content source, a
different IPC surface).

## Trust boundary

Studio is a local HTTP server (`server.ts`, Next.js) fronted by an Electron
`BrowserWindow`. The server has full read/write access to whatever directory
the user points it at (the "workspace"), and can shell out to the Modeller
CLI to run code generation against that directory. The renderer is not a
sandboxed, low-trust surface in the traditional web sense — it's the primary
UI for a tool whose entire job is editing files and running a generator on
disk. The two things that matter for this shell's security are:

1. **Who can reach the local server.** Everything the server can do (read
   workspace files, write them, run generation) is only as safe as the set of
   callers who can send it a request.
2. **What the renderer process can do beyond talking to that server.** The
   preload bridge, node integration, and navigation/popup behavior determine
   whether a hostile page loaded into the window (deliberately or via a bug)
   gains anything beyond "a browser tab pointed at localhost."

## Findings

### 1. The local server has no origin/CSRF defense — Gap

**Status: gap, real impact.**

`server.ts` binds to `hostname = 'localhost'` in desktop mode (not `0.0.0.0`,
which is playground-only), so the server is unreachable from other machines
on the network. That's the one mitigation already in place. It is *not*
unreachable from other processes and other browser tabs on the same machine.

None of the API routes (`src/app/api/workspace`, `.../document`,
`.../generate`, `.../projection`) check `Origin`/`Referer`, use a
CSRF token, or require any credential. `localOnlyRouteGuard` (see
`src/server/playground-guard.ts`) only disables these routes in playground
mode — in desktop mode they're fully live with no additional gate. This is
the classic "localhost server" problem shared by tools like Jupyter or a dev
server with an exposed API: any web page the user has open in their regular
browser while Studio is running can issue same-machine requests to
`http://localhost:3100` and have them treated as first-party.

Concretely, with Studio running, a malicious or compromised page open in the
user's ordinary browser could, without any user interaction beyond having
that tab open:

- `POST /api/workspace` to repoint the active workspace at an
  attacker-chosen directory (`setWorkspaceRoot` resolves and reads whatever
  directory it's given, provided it contains a `.modeller/config.json`).
- `PUT /api/document?path=...` to overwrite any file the current workspace
  already declares as a source, with attacker-controlled content.
- `GET /api/generate` to run the Modeller CLI's code-generation pipeline
  against the (possibly now attacker-controlled) workspace, writing whatever
  the generator produces to disk.

None of this requires the Electron window itself to be compromised — it's a
pure browser-to-localhost request, independent of the renderer/preload
hardening discussed below.

**Proposed fix:** reject cross-origin requests to the local server's API
routes. The simplest version is an `Origin`/`Referer` allow-list (only
`http://localhost:<port>` accepted) enforced centrally — e.g. in the guard
that `localOnlyRouteGuard` already sits next to, so every route gets it by
construction rather than needing each route to remember to check. A
same-site cookie or a per-launch random token embedded in the page and
required on state-changing requests is a more thorough alternative if origin
checks alone feel too easy to spoof from a non-browser HTTP client.

### 2. Renderer navigation and popups are not restricted — Gap

**Status: gap.**

Neither `main.ts` nor `panel-windows.ts` registers a `will-navigate`
listener or a `setWindowOpenHandler` on any `BrowserWindow`/`WebContents`.
Electron's own default deny-by-default behavior for `window.open()` (current
since Electron ~14) is the only thing standing between today's code and an
attacker-controlled page opening arbitrary new windows — and it does nothing
about in-place navigation.

This matters more than it would for a typical "renders trusted first-party
content" Electron app, because of how the preload bridge works: a preload
script re-runs on *every* navigation of the `WebContents` it's attached to,
not just the first load. If the main window's `WebContents` were ever
navigated away from `http://localhost:<port>` — a bug in a future feature
that renders a link or redirect from workspace content, for instance — the
new page would still get `contextBridge`'s `window.modeller` object
(`onOpenFolder`, `detachPanel`, `requestOpenFolder`,
`recordRecentWorkspace`, `onPanelDetachState`, `requestPanelDetachState`)
wired up, on whatever origin it landed on. None of those calls hand over raw
filesystem or process access, but `requestOpenFolder` still drives a native
file-picker dialog and `recordRecentWorkspace` still writes to the recent-
workspaces list from a page that is, at that point, not Studio's own server.

There is currently no code path that navigates the window to
attacker-influenced content, so this is a defense-in-depth gap, not an
exploited-today vulnerability. It is exactly the kind of thing that becomes
one the first time someone renders a clickable link or an
`<iframe>`/redirect sourced from workspace or generated content.

**Proposed fix:** on every `BrowserWindow` this app creates (main window and
both panel windows), add a `will-navigate` handler that `preventDefault()`s
any navigation whose target origin isn't `http://localhost:<port>`, and a
`setWindowOpenHandler` that returns `{ action: 'deny' }` unconditionally (or
routes specific, known-safe external links through `shell.openExternal`
instead of a new `BrowserWindow`).

### 3. Content-Security-Policy — Present

**Status: present, shared with the hosted playground.**

`next.config.mjs`'s `SECURITY_HEADERS` sets a `Content-Security-Policy` (plus
`X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`) on every
response, applied via Next's `headers()` — this covers pages served to both
the Electron windows and the hosted playground, since it's the same Next app
in two modes. Notable choices already made and documented inline: `'self'`
default, `'unsafe-inline' 'wasm-unsafe-eval'` on `script-src` (Next's RSC
hydration and Monaco/onigasm need them), `frame-ancestors 'none'`, and a
`connect-src` scoped to `'self'` plus the specific external origins the app
actually calls (PostHog, Vercel Analytics, and the hosted API origin in
playground mode).

This CSP is a genuine content-injection mitigation, but it is not a
navigation control — it doesn't stop the top-level window from navigating to
a new URL (see finding 2). The two are complementary, not substitutes for
each other.

The one documented gap in the CSP itself: `script-src` needs
`'unsafe-inline'` because Next's RSC streaming uses inline
`<script>self.__next_f.push(...)</script>` tags rather than nonced or
external scripts. The comment in `next.config.mjs` already identifies the
real fix (a nonce-based CSP wired through middleware) as future work, not
attempted in this pass. No change proposed here beyond what's already
tracked there.

### 4. Process isolation (contextIsolation / nodeIntegration / sandbox) — Present

**Status: present for the settings that are explicit; one worth making
explicit.**

Every `BrowserWindow` this app creates (`main.ts`'s main window,
`panel-windows.ts`'s two panel windows) sets `nodeIntegration: false` and
`contextIsolation: true`. That's the correct pair for a window whose page
content is untrusted-by-default and should only reach Node/Electron APIs
through the preload bridge.

`sandbox` is not set explicitly on any window. Electron (this app pins
`^44.0.0`) has defaulted `sandbox: true` for all renderers since Electron 20,
independent of whether a preload script is present, so the effective
behavior today is already sandboxed. Recommend setting `sandbox: true`
explicitly anyway: relying on a version-dependent default is fragile against
a future Electron major bump changing that default, and an explicit setting
makes the security posture readable from the `webPreferences` object itself
rather than requiring the reader to know Electron's version history.

### 5. Preload capability surface — Present, narrow

**Status: present and narrow — no changes proposed.**

`electron/preload.ts` exposes exactly one object, `window.modeller`, via
`contextBridge.exposeInMainWorld`, with six methods:

- `onOpenFolder` / `onPanelDetachState` — subscribe to a main-to-renderer
  IPC channel, return an unsubscribe function. No data flows renderer-to-main
  here beyond the fact of subscribing.
- `detachPanel(kind)`, `requestOpenFolder()`, `recordRecentWorkspace(root)`,
  `requestPanelDetachState()` — one-way `ipcRenderer.send` calls into
  specific, narrow main-process handlers.

None of these expose `ipcRenderer` itself, a generic `invoke`/`send` passthrough,
or any Node built-in (`fs`, `path`, `child_process`) to the renderer. Every
handler on the main-process side (`main.ts`) validates its input before
acting — e.g. `panel:detach`'s handler checks `isPanelKind(kind)` before
using the renderer-supplied value as a lookup key, guarding exactly the kind
of "renderer sends unexpected data" case this bridge shape makes possible.
This is the shape a preload bridge should have: a short list of specific
actions, not a generic escape hatch. The one caveat is finding 2 above — the
bridge's safety depends on the window only ever loading Studio's own server.

### 6. Release code signing — Gap

**Status: gap.**

`electron-builder.json` has no `win.certificateFile`, `win.certificateSha1`,
`win.certificateSubjectName`, or any other signing configuration, and
`asar: false` means the installed application's JS is plain files on disk
rather than packed into a single archive. Combined, this means:

- The installer (`ModellerStudioSetup.exe`) is unsigned. Windows SmartScreen
  will warn on first run, and there is no way for a user (or their antivirus)
  to verify the installer came from this project rather than a tampered
  redistribution.
- Because `asar` is disabled, anyone with write access to an already-
  installed copy (e.g. malware already running as the same user, or a
  shared/multi-user machine without per-user installs — though `nsis.
  perMachine: false` already limits this to the installing user's profile)
  can edit the shipped JS in place without needing to touch a packed archive.
  This is a deliberate, documented tradeoff (`asar: false` is required for
  the packaged server's own file layout — see the comment in
  `server-supervisor.ts`), not an oversight, but it raises the value of
  installer-level integrity since post-install tampering has no packing
  format to make it harder.

**Proposed fix:** code-sign the Windows installer (and ideally the
`ModellerStudio.exe` executable itself) with an Authenticode certificate
before distribution. Acquiring the certificate is a business/ops step, not
engineering — see the follow-up release-policy issue — but wiring
`electron-builder`'s signing config once a certificate exists is a small,
mechanical change.

## Summary

| # | Area | Status |
|---|------|--------|
| 1 | Local server origin/CSRF defense | Gap — real impact today |
| 2 | Renderer navigation / popup restrictions | Gap — defense-in-depth, not yet exploited |
| 3 | Content-Security-Policy | Present (documented follow-up: nonce-based CSP) |
| 4 | Process isolation (contextIsolation/nodeIntegration/sandbox) | Present (make `sandbox: true` explicit) |
| 5 | Preload capability surface | Present, narrow — no changes needed |
| 6 | Release code signing | Gap — installer unsigned |

Findings 1 and 2 are the ones worth prioritizing: 1 has a concrete exploit
path with no Electron-specific precondition, and 2 is the gap that would
turn any future content-injection bug into full preload-bridge access on a
hostile origin.
