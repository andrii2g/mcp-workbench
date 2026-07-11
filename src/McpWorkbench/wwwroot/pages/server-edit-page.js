import { el } from "../dom.js";

function field(label, input, hint) {
  const id = `field-${crypto.randomUUID()}`;
  input.id = id;
  return el("div", { class: "field" }, el("label", { for: id, text: label }), input, hint ? el("small", { text: hint }) : null);
}

function rowsEditor(title, values, keyLabel, valueLabel, placeholders = {}) {
  const root = el("div", { class: "rows" });
  const list = el("div");
  const isSensitive = name => /authorization|api[-_]?key|token|password|secret|credential|cookie/i.test(name);
  const add = (key = "", value = "") => {
    const redacted = value === "[REDACTED]";
    const managedReference = value.startsWith("${SECRET:") ? value : null;
    const keyInput = el("input", { value: key, placeholder: placeholders.key || "", "aria-label": keyLabel });
    const valueInput = el("input", { value: redacted || managedReference ? "" : value, placeholder: managedReference ? "Stored securely" : redacted ? "Enter a replacement secret" : placeholders.value || "", "aria-label": valueLabel, "data-redacted": redacted ? "true" : null, "data-secret-reference": managedReference });
    const secretInput = el("input", { type: "checkbox", "aria-label": `${keyLabel} is secret` });
    secretInput.checked = redacted || Boolean(managedReference) || isSensitive(key);
    const updateSecretState = () => { valueInput.type = secretInput.checked ? "password" : "text"; };
    keyInput.addEventListener("input", () => { if (!keyInput.dataset.secretEdited) { secretInput.checked = isSensitive(keyInput.value); updateSecretState(); } });
    secretInput.addEventListener("change", () => { keyInput.dataset.secretEdited = "true"; updateSecretState(); });
    updateSecretState();
    const row = el("div", { class: "row secret-row" }, keyInput, valueInput, el("label", { class: "secret-toggle" }, secretInput, " Secret"), el("button", { type: "button", class: "icon-button", text: "x", title: "Remove row", "aria-label": "Remove row", onclick: () => row.remove() }));
    list.append(row);
  };
  for (const [key, value] of values) add(key, value);
  root.append(el("div", { class: "row-heading" }, el("strong", { text: title }), el("button", { type: "button", class: "secondary", text: "Add row", onclick: () => add() })), list);
  return {
    node: root,
    missingSecrets: () => [...list.querySelectorAll('input[data-redacted="true"]')].some(input => !input.value.trim()),
    hasAuthorization: () => [...list.querySelectorAll(".row")].some(row => row.querySelector('input:not([type="checkbox"])').value.trim().toLowerCase() === "authorization"),
    extract: () => {
      const entries = {};
      const secrets = {};
      for (const row of list.querySelectorAll(".row")) {
        const [keyInput, valueInput] = row.querySelectorAll('input:not([type="checkbox"])');
        const key = keyInput.value.trim();
        const value = valueInput.value.trim();
        if (!key) continue;
        if (!value && valueInput.dataset.secretReference) {
          entries[key] = valueInput.dataset.secretReference;
          continue;
        }
        if (row.querySelector('input[type="checkbox"]').checked && value && !value.includes("${ENV:") && !value.includes("${SECRET:")) {
          const id = crypto.randomUUID();
          entries[key] = "${SECRET:" + id + "}";
          secrets[id] = value;
        } else entries[key] = value;
      }
      return { entries, secrets };
    }
  };
}

function authorizationEditor(value) {
  const root = el("div", { class: "authorization-editor" });
  const kind = el("select", {},
    el("option", { value: "none", text: "None" }),
    el("option", { value: "bearer", text: "Bearer token" }),
    el("option", { value: "basic", text: "Basic authentication" }),
    el("option", { value: "customScheme", text: "Custom scheme" }),
    el("option", { value: "customRaw", text: "Custom raw value" }));
  kind.value = value?.kind || "none";
  const username = el("input", { value: value?.username || "", autocomplete: "username" });
  const scheme = el("input", { value: value?.scheme || "", placeholder: "Token" });
  const managedReference = value?.credential?.startsWith("${SECRET:") ? value.credential : null;
  const redacted = value?.credential === "[REDACTED]";
  const credential = el("input", {
    type: "password",
    value: managedReference || redacted ? "" : value?.credential || "",
    placeholder: managedReference ? "Stored securely" : redacted ? "Enter a replacement credential" : "",
    autocomplete: "new-password",
    "data-secret-reference": managedReference,
    "data-redacted": redacted ? "true" : null
  });
  const usernameField = field("Username", username);
  const schemeField = field("Scheme", scheme);
  const credentialField = field("Credential", credential);
  const fields = el("div", { class: "authorization-fields form-grid" }, usernameField, schemeField, credentialField);
  const update = () => {
    usernameField.hidden = kind.value !== "basic";
    schemeField.hidden = kind.value !== "customScheme";
    credentialField.hidden = kind.value === "none";
  };
  kind.addEventListener("change", update);
  update();
  root.append(field("Authorization", kind), fields);
  return {
    node: root,
    missingCredential: () => kind.value !== "none" && !credential.value.trim() && !credential.dataset.secretReference,
    extract: () => {
      if (kind.value === "none") return { authorization: null, secrets: {} };
      let credentialValue = credential.value.trim();
      const secrets = {};
      if (!credentialValue && credential.dataset.secretReference) credentialValue = credential.dataset.secretReference;
      else if (credentialValue && !credentialValue.includes("${ENV:") && !credentialValue.includes("${SECRET:")) {
        const id = crypto.randomUUID();
        secrets[id] = credentialValue;
        credentialValue = "${SECRET:" + id + "}";
      }
      return {
        authorization: {
          kind: kind.value,
          username: kind.value === "basic" ? username.value : null,
          scheme: kind.value === "customScheme" ? scheme.value : null,
          credential: credentialValue
        },
        secrets
      };
    }
  };
}

export function serverEditPage(server, onSave) {
  const isEdit = Boolean(server);
  const form = el("form", { class: "server-form compact-form" });
  const name = el("input", { required: "", value: server?.name || "" });
  const description = el("textarea", { rows: "2" });
  description.value = server?.description || "";
  const enabled = el("input", { type: "checkbox" });
  enabled.checked = server?.enabled ?? true;
  const transport = el("select", {}, el("option", { value: "stdio", text: "Stdio process" }), el("option", { value: "http", text: "HTTP endpoint" }));
  transport.value = server?.transport || "http";
  const timeout = el("input", { type: "number", min: "1", max: "300", value: server?.operationTimeoutSeconds || 30 });
  const command = el("input", { value: server?.stdio?.command || "" });
  const args = el("textarea", { rows: "3", placeholder: "One argument per line" });
  args.value = (server?.stdio?.arguments || []).join("\n");
  const workdir = el("input", { value: server?.stdio?.workingDirectory || "" });
  const shutdown = el("input", { type: "number", min: "1", value: server?.stdio?.shutdownTimeoutSeconds || 5 });
  const env = rowsEditor("Environment", Object.entries(server?.stdio?.environment || {}), "Environment name", "Environment value", { key: "MCP_ACCESS_TOKEN", value: "${ENV:MCP_ACCESS_TOKEN}" });
  const endpoint = el("input", { type: "url", value: server?.http?.endpoint || "" });
  const mode = el("select", {}, ...["auto", "streamableHttp", "legacySse"].map(value => el("option", { value, text: value })));
  mode.value = server?.http?.mode || "auto";
  const authorization = authorizationEditor(server?.http?.authorization);
  const headers = rowsEditor("Additional headers", Object.entries(server?.http?.headers || {}), "Header name", "Header value", { key: "X-API-Key", value: "Header value" });
  const stdio = el("fieldset", {}, el("legend", { text: "Stdio settings" }), el("div", { class: "form-grid" }, field("Command", command), field("Working directory", workdir)), field("Arguments", args, "One argument per line, in order."), env.node, field("Shutdown timeout (seconds)", shutdown));
  const http = el("fieldset", {}, el("legend", { text: "HTTP settings" }), el("div", { class: "form-grid" }, field("Endpoint", endpoint), field("Mode", mode)), authorization.node, headers.node);
  const updateTransport = () => { stdio.hidden = transport.value !== "stdio"; http.hidden = transport.value !== "http"; };
  transport.addEventListener("change", updateTransport);
  updateTransport();
  const error = el("div", { class: "error-banner", hidden: "", role: "alert" });
  const enabledId = `field-${crypto.randomUUID()}`;
  enabled.id = enabledId;
  form.append(
    el("div", { class: "page-head" }, el("div", {}, el("p", { class: "eyebrow", text: isEdit ? "Configuration" : "Registration" }), el("h1", { text: isEdit ? `Edit ${server.name}` : "Add MCP server" }), el("p", { class: "subtitle", text: isEdit ? "Saving disconnects an active server before replacement." : "Register a local process or remote endpoint." }))),
    error,
    el("div", { class: "form-grid" }, field("Name", name), field("Description", description)),
    el("div", { class: "form-grid form-grid-controls" }, field("Transport", transport), field("Operation timeout (seconds)", timeout), el("div", { class: "field inline" }, enabled, el("label", { for: enabledId, text: "Enabled" }))),
    stdio,
    http,
    el("div", { class: "actions" }, el("button", { class: "primary", type: "submit", text: "Save" }), el("button", { class: "secondary", type: "submit", name: "connect", value: "true", text: "Save and connect" }), el("a", { class: "secondary", href: server ? `#/servers/${server.id}` : "#/servers", text: "Cancel" })));
  form.addEventListener("submit", async event => {
    event.preventDefault();
    const submitter = event.submitter;
    if ((transport.value === "stdio" && env.missingSecrets()) || (transport.value === "http" && headers.missingSecrets())) {
      error.hidden = false;
      error.textContent = "Re-enter every redacted secret value before saving.";
      return;
    }
    if (transport.value === "http" && authorization.missingCredential()) {
      error.hidden = false;
      error.textContent = "Enter an authorization credential.";
      return;
    }
    if (transport.value === "http" && headers.hasAuthorization()) {
      error.hidden = false;
      error.textContent = "Configure Authorization in the dedicated selector, not Additional headers.";
      return;
    }
    const selectedRows = transport.value === "stdio" ? env.extract() : headers.extract();
    const selectedAuthorization = transport.value === "http" ? authorization.extract() : { authorization: null, secrets: {} };
    const body = {
      name: name.value, description: description.value || null, enabled: enabled.checked, transport: transport.value,
      stdio: transport.value === "stdio" ? { command: command.value, arguments: args.value.split(/\r?\n/).filter(Boolean), workingDirectory: workdir.value || null, environment: selectedRows.entries, shutdownTimeoutSeconds: Number(shutdown.value) } : null,
      http: transport.value === "http" ? { endpoint: endpoint.value, mode: mode.value, headers: selectedRows.entries, authorization: selectedAuthorization.authorization } : null,
      operationTimeoutSeconds: Number(timeout.value),
      secrets: { ...selectedRows.secrets, ...selectedAuthorization.secrets }
    };
    try {
      [...form.querySelectorAll("button")].forEach(button => button.disabled = true);
      await onSave(body, submitter?.value === "true");
    } catch (exception) {
      error.hidden = false;
      error.textContent = `${exception.message} (${exception.code || "error"})`;
      [...form.querySelectorAll("button")].forEach(button => button.disabled = false);
    }
  });
  return form;
}
