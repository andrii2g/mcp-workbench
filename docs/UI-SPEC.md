# Web UI Specification

The bundled UI is a small static single-page dashboard implemented with semantic HTML,
CSS, and browser-native JavaScript modules. It has no Node.js build step and no external
CDN dependencies.

## Design goals

- make server lifecycle and current target unmistakable;
- expose raw MCP data without overwhelming normal workflows;
- support keyboard and narrow-screen use;
- never execute or interpret untrusted MCP content;
- remain deployable as static files in the Native AOT executable.

## Routes

Client-side route state may use hash routes:

```text
#/servers
#/servers/new
#/servers/{id}
#/servers/{id}/edit
#/servers/{id}/tools/{encodedToolName}
```

A hash router avoids server fallback-route complexity. Reloading `index.html` works at
every route.

## Layout

```text
┌───────────────────────────────────────────────────────────┐
│ MCP Workbench                     Health  API settings     │
├──────────────────┬────────────────────────────────────────┤
│ Servers          │ Main content                           │
│                  │                                        │
│ ● Local Demo     │ Selected server / tool / form/result   │
│ ○ Remote MCP     │                                        │
│                  │                                        │
│ + Add server     │                                        │
└──────────────────┴────────────────────────────────────────┘
```

On narrow screens, the server list becomes a top drawer or stacked section.

## Pages

### Server list

Displays:

- name;
- transport;
- enabled state;
- runtime status;
- tool count;
- last safe error summary;
- actions: Open, Connect/Disconnect, Delete.

Sorting: name ascending by default, then ID for stability.
Search is client-side for the currently loaded set.

Empty state explains how to add a stdio or HTTP server.

### Add/edit server

Common fields:

- name;
- description;
- enabled;
- transport selector;
- operation timeout.

Stdio fields:

- command;
- repeatable argument rows preserving order;
- working directory;
- repeatable environment key/value rows;
- shutdown timeout.

HTTP fields:

- endpoint;
- mode;
- repeatable header key/value rows.

Secret guidance appears beside environment/header values:

```text
Use ${ENV:NAME} to reference a process environment variable.
```

Actions:

```text
Save
Save and connect
Cancel
```

Editing a connected definition warns that saving disconnects it first.

Client validation improves usability, but the API remains authoritative.

### Server details

Header:

- name and transport;
- status badge;
- Connect/Disconnect;
- Ping;
- Edit;
- Delete.

Metadata cards:

- endpoint or command summary without secrets;
- connected since;
- protocol version;
- remote server name/version;
- selected HTTP mode;
- capability flags;
- last operation/error.

Tools section:

- Refresh;
- filter;
- tool list with name, title, brief description, annotations;
- loading, empty, and error states.

### Tool page

Header makes target explicit:

```text
Server: Local Demo
Tool: add
```

Sections:

1. Description and annotations.
2. Input schema viewer.
3. Input editor tabs: Form / Raw JSON.
4. Run controls and timeout.
5. Latest result.
6. Optional metadata-only recent execution list.

### Settings/help

Version 1 may use a modal or static help section instead of another API surface. Show:

- API base path;
- whether API-key protection is active, without revealing the key;
- registry health;
- build version;
- links to bundled documentation where packaged.

## Input editor

### Raw JSON mode

This is authoritative.

Requirements:

- plain `<textarea>` or safe editor implemented without third-party runtime dependency;
- default `{}`;
- parse on input/debounce;
- line/column error when possible;
- require root object;
- format button;
- preserve content when switching tabs.

### Generated form mode

Support only common schema constructs:

- object properties;
- required;
- string;
- number;
- integer;
- boolean;
- enum;
- array of scalar values;
- nested object to a bounded depth;
- title, description, default;
- minimum/maximum and string length hints where straightforward.

For unsupported constructs (`oneOf`, complex unions, recursive refs, etc.):

- show a notice;
- keep raw JSON available;
- never silently omit values;
- allow form to display supported fields while making raw mode the final source of truth.

Do not claim full JSON Schema validation.

## Result rendering

Summary:

- success / MCP tool error / transport failure;
- duration;
- start/completion timestamp;
- truncation warning.

Known blocks:

### Text

Render in `<pre>` using `textContent`. Add Copy action.

### Image

Only render supported MIME types from bounded base64 data. Do not render script-capable
SVG. Show MIME type and byte size. Provide no automatic external fetch.

### Embedded resource

Show URI, MIME type, and safe text/bounded data preview.

### Resource link

Display metadata and URI. Opening requires explicit action and safe link attributes.

### Unknown

Render bounded formatted JSON as text.

Structured content and raw result appear in collapsible panels.

## Status semantics

Suggested visible labels:

```text
Disconnected
Connecting…
Connected
Disconnecting…
Faulted
```

Do not rely on color alone. Include text/icon shapes and accessible names.

Buttons are disabled only when the action is truly invalid. Provide progress text for
long operations and allow cancellation of a running tool call.

## Error presentation

- field errors beside controls;
- operation error banner with safe message and code;
- request ID available in expandable technical details;
- never show stack traces or secret values;
- retain entered form values after validation failure.

## Accessibility

Minimum requirements:

- semantic landmarks and headings;
- labels associated with every control;
- full keyboard operation;
- visible focus;
- no color-only state;
- sufficient contrast;
- status updates via restrained `aria-live`;
- dialog focus trap and return;
- reduced-motion respect;
- buttons with descriptive names.

## Security implementation rules

- no inline scripts;
- no third-party CDN;
- no `eval`, `Function`, or dynamic module URLs;
- no `innerHTML` with data;
- use DOM creation plus `textContent`;
- no automatic navigation to MCP-provided URIs;
- no Markdown-to-HTML renderer in version 1;
- API key, when used by the bundled UI, is entered for the browser session and held only
  in memory/session storage according to documented tradeoff; it is never echoed by API.

For a local tool, the preferred deployment is same-origin UI plus API behind an
authenticated reverse proxy, avoiding long-term browser storage of a key.

## JavaScript module structure

```text
wwwroot/
├── components/
│   ├── confirm-dialog.js
│   ├── json-editor.js
│   ├── result-viewer.js
│   ├── schema-form.js
│   ├── server-card.js
│   └── status-badge.js
├── pages/
│   ├── dashboard-page.js
│   ├── server-details-page.js
│   ├── server-edit-page.js
│   └── tool-runner-page.js
├── api-client.js
├── app.css
├── app.js
├── dom.js
├── index.html
├── router.js
└── state.js
```

Each module has a narrow purpose and exports explicit functions. Avoid global mutable
state; central state contains only current definitions/runtime snapshots and view state.

## CSS structure

```text
wwwroot/css/
├── tokens.css
├── base.css
├── layout.css
├── components.css
└── utilities.css
```

Use system fonts. Support light/dark via `prefers-color-scheme`; a persistent theme
selector is not required in version 1.

## Acceptance checklist

- all workflows work without a mouse;
- untrusted `<script>` text is shown literally;
- no console errors in supported browsers;
- no network requests to third-party origins;
- connect/invoke progress cannot submit accidental duplicates;
- API failure can be retried without page reload;
- navigation state survives browser back/forward;
- current server/tool is always visible;
- generated form and raw JSON stay synchronized or clearly identify which is authoritative;
- layout remains usable at 360 CSS pixels.
