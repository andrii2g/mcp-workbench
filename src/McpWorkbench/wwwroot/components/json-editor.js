import { el } from "../dom.js";
export function jsonEditor(initial = "{}") {
  const textarea = el("textarea", { class: "code-editor", rows: "12", spellcheck: "false", "aria-label": "Tool arguments as JSON" }); textarea.value = initial;
  const message = el("div", { class: "field-message", "aria-live": "polite" });
  const validate = () => { try { const value = JSON.parse(textarea.value); if (!value || Array.isArray(value) || typeof value !== "object") throw new Error("Root value must be an object."); message.textContent = "Valid JSON object"; message.className = "field-message valid"; return value; } catch (error) { message.textContent = error.message; message.className = "field-message error"; return null; } };
  textarea.addEventListener("input", validate);
  const format = el("button", { type: "button", class: "secondary", text: "Format", onclick: () => { const value = validate(); if (value) textarea.value = JSON.stringify(value, null, 2); } });
  validate(); return { node: el("div", { class: "editor" }, textarea, el("div", { class: "editor-foot" }, message, format)), value: validate, set: value => { textarea.value = JSON.stringify(value, null, 2); validate(); } };
}
