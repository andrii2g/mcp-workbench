import { el } from "../dom.js";

function schemaType(spec) {
  const types = Array.isArray(spec?.type) ? spec.type : [spec?.type];
  return types.find(type => type && type !== "null") || "string";
}

export function schemaForm(schema, initial = {}, onChange = () => {}) {
  const root = el("div", { class: "schema-form" });
  const fields = new Map();
  const properties = schema?.properties || {};
  const required = new Set(schema?.required || []);
  for (const [name, spec] of Object.entries(properties)) {
    const id = `schema-${crypto.randomUUID()}`;
    const type = schemaType(spec);
    let input;
    if (Array.isArray(spec.enum)) {
      input = el("select", { id });
      for (const option of spec.enum) input.append(el("option", { value: option, text: option }));
    } else if (type === "boolean") input = el("input", { id, type: "checkbox" });
    else if (type === "object") input = el("textarea", { id, rows: "3", "aria-describedby": `${id}-hint` });
    else input = el("input", { id, type: ["number", "integer"].includes(type) ? "number" : "text", min: spec.minimum, max: spec.maximum, required: required.has(name) ? "" : null });
    if (initial[name] !== undefined) type === "boolean" ? input.checked = initial[name] : input.value = Array.isArray(initial[name]) ? initial[name].join(", ") : initial[name];
    fields.set(name, { input, spec, type });
    input.addEventListener("input", () => onChange(read()));
    root.append(el("div", { class: "field" }, el("label", { for: id, text: spec.title || name }), input, spec.description ? el("small", { text: spec.description }) : null));
  }
  if (!Object.keys(properties).length) root.append(el("p", { class: "muted", text: "This tool has no declared input fields. Raw JSON remains authoritative." }));
  if (schema?.oneOf || schema?.anyOf || schema?.$ref) root.prepend(el("p", { class: "notice", text: "This schema contains advanced constructs. Review raw JSON before running." }));
  function read() {
    const value = {};
    for (const [name, { input, spec, type }] of fields) {
      if (type === "boolean") value[name] = input.checked;
      else if (input.value !== "") {
        if (["number", "integer"].includes(type)) value[name] = Number(input.value);
        else if (type === "object") { try { value[name] = JSON.parse(input.value); } catch { value[name] = {}; } }
        else if (type === "array") {
          const itemType = schemaType(spec.items);
          value[name] = input.value.split(",").map(entry => { const item = entry.trim(); return ["number", "integer"].includes(itemType) ? Number(item) : itemType === "boolean" ? item === "true" : item; });
        } else value[name] = input.value;
      }
    }
    return value;
  }
  function set(value) {
    for (const [name, { input, type }] of fields) {
      const current = value?.[name];
      if (type === "boolean") input.checked = Boolean(current);
      else input.value = current === undefined ? "" : Array.isArray(current) ? current.join(", ") : typeof current === "object" ? JSON.stringify(current) : current;
    }
  }
  return { node: root, value: read, set };
}
