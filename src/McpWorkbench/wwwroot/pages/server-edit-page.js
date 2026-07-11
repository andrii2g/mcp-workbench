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
  const headers = rowsEditor("Headers", Object.entries(server?.http?.headers || {}), "Header name", "Header value", { key: "Authorization", value: "Bearer ${ENV:MCP_ACCESS_TOKEN}" });
  const stdio = el("fieldset", {}, el("legend", { text: "Stdio settings" }), el("div", { class: "form-grid" }, field("Command", command), field("Working directory", workdir)), field("Arguments", args, "One argument per line, in order."), env.node, field("Shutdown timeout (seconds)", shutdown));
  const http = el("fieldset", {}, el("legend", { text: "HTTP settings" }), el("div", { class: "form-grid" }, field("Endpoint", endpoint), field("Mode", mode)), headers.node);
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
    el("p", { class: "secret-help", text: "Secret values are encrypted in the local vault. ${ENV:NAME} references remain supported." }),
    el("div", { class: "actions" }, el("button", { class: "primary", type: "submit", text: "Save" }), el("button", { class: "secondary", type: "submit", name: "connect", value: "true", text: "Save and connect" }), el("a", { class: "secondary", href: server ? `#/servers/${server.id}` : "#/servers", text: "Cancel" })));
  form.addEventListener("submit", async event => {
    event.preventDefault();
    const submitter = event.submitter;
    if ((transport.value === "stdio" && env.missingSecrets()) || (transport.value === "http" && headers.missingSecrets())) {
      error.hidden = false;
      error.textContent = "Re-enter every redacted secret value before saving.";
      return;
    }
    const selectedRows = transport.value === "stdio" ? env.extract() : headers.extract();
    const body = {
      name: name.value, description: description.value || null, enabled: enabled.checked, transport: transport.value,
      stdio: transport.value === "stdio" ? { command: command.value, arguments: args.value.split(/\r?\n/).filter(Boolean), workingDirectory: workdir.value || null, environment: selectedRows.entries, shutdownTimeoutSeconds: Number(shutdown.value) } : null,
      http: transport.value === "http" ? { endpoint: endpoint.value, mode: mode.value, headers: selectedRows.entries } : null,
      operationTimeoutSeconds: Number(timeout.value),
      secrets: selectedRows.secrets
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
