import { el } from "../dom.js";
export function schemaForm(schema, initial = {}, onChange = () => {}) {
  const root = el("div", { class: "schema-form" }); const fields = new Map(); const properties = schema?.properties || {}; const required = new Set(schema?.required || []);
  for (const [name, spec] of Object.entries(properties)) {
    const id = `schema-${crypto.randomUUID()}`; let input;
    if (Array.isArray(spec.enum)) { input = el("select", { id }); for (const option of spec.enum) input.append(el("option", { value: option, text: option })); }
    else if (spec.type === "boolean") input = el("input", { id, type: "checkbox" });
    else if (spec.type === "object") input = el("textarea", { id, rows: "4", "aria-describedby": `${id}-hint` });
    else input = el("input", { id, type: ["number", "integer"].includes(spec.type) ? "number" : "text", min: spec.minimum, max: spec.maximum, required: required.has(name) ? "" : null });
    if (initial[name] !== undefined) spec.type === "boolean" ? input.checked = initial[name] : input.value = Array.isArray(initial[name]) ? initial[name].join(", ") : initial[name];
    fields.set(name, { input, spec }); input.addEventListener("input", () => onChange(read()));
    root.append(el("div", { class: "field" }, el("label", { for: id, text: spec.title || name }), input, spec.description ? el("small", { text: spec.description }) : null));
  }
  if (!Object.keys(properties).length) root.append(el("p", { class: "muted", text: "This tool has no declared input fields. Raw JSON remains authoritative." }));
  if (schema?.oneOf || schema?.anyOf || schema?.$ref) root.prepend(el("p", { class: "notice", text: "This schema contains advanced constructs. Review raw JSON before running." }));
  function read() { const value = {}; for (const [name, { input, spec }] of fields) { if (spec.type === "boolean") value[name] = input.checked; else if (input.value !== "") { if (["number", "integer"].includes(spec.type)) value[name] = Number(input.value); else if (spec.type === "object") { try { value[name] = JSON.parse(input.value); } catch { value[name] = {}; } } else if (spec.type === "array") value[name] = input.value.split(",").map(x => { const item = x.trim(); return ["number", "integer"].includes(spec.items?.type) ? Number(item) : spec.items?.type === "boolean" ? item === "true" : item; }); else value[name] = input.value; } } return value; }
  function set(value) { for (const [name, { input, spec }] of fields) { const current = value?.[name]; if (spec.type === "boolean") input.checked = Boolean(current); else input.value = current === undefined ? "" : Array.isArray(current) ? current.join(", ") : typeof current === "object" ? JSON.stringify(current) : current; } }
  return { node: root, value: read, set };
}
