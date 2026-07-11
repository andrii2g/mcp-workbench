import { el } from "../dom.js";
export function schemaForm(schema, initial = {}, onChange = () => {}) {
  const root = el("div", { class: "schema-form" }); const fields = new Map(); const properties = schema?.properties || {}; const required = new Set(schema?.required || []);
  for (const [name, spec] of Object.entries(properties)) {
    const id = `schema-${crypto.randomUUID()}`; let input;
    if (Array.isArray(spec.enum)) { input = el("select", { id }); for (const option of spec.enum) input.append(el("option", { value: option, text: option })); }
    else if (spec.type === "boolean") input = el("input", { id, type: "checkbox" });
    else input = el("input", { id, type: ["number", "integer"].includes(spec.type) ? "number" : "text", min: spec.minimum, max: spec.maximum, required: required.has(name) ? "" : null });
    if (initial[name] !== undefined) spec.type === "boolean" ? input.checked = initial[name] : input.value = Array.isArray(initial[name]) ? initial[name].join(", ") : initial[name];
    fields.set(name, { input, spec }); input.addEventListener("input", () => onChange(read()));
    root.append(el("div", { class: "field" }, el("label", { for: id, text: spec.title || name }), input, spec.description ? el("small", { text: spec.description }) : null));
  }
  if (!Object.keys(properties).length) root.append(el("p", { class: "muted", text: "This tool has no declared input fields. Raw JSON remains authoritative." }));
  if (schema?.oneOf || schema?.anyOf || schema?.$ref) root.prepend(el("p", { class: "notice", text: "This schema contains advanced constructs. Review raw JSON before running." }));
  function read() { const value = {}; for (const [name, { input, spec }] of fields) { if (spec.type === "boolean") value[name] = input.checked; else if (input.value !== "") value[name] = ["number", "integer"].includes(spec.type) ? Number(input.value) : spec.type === "array" ? input.value.split(",").map(x => x.trim()) : input.value; } return value; }
  return { node: root, value: read };
}
