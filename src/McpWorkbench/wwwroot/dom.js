export function el(tag, options = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(options)) {
    if (key === "class") node.className = value;
    else if (key === "text") node.textContent = value;
    else if (key.startsWith("on") && typeof value === "function") node.addEventListener(key.slice(2), value);
    else if (value !== null && value !== undefined) node.setAttribute(key, String(value));
  }
  for (const child of children.flat()) if (child !== null && child !== undefined) node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  return node;
}

export function replace(node, content) { node.replaceChildren(content); }
export function formatDate(value) { return value ? new Date(value).toLocaleString() : "Not available"; }
export function toast(message, kind = "info") {
  const region = document.querySelector("#toast-region");
  const item = el("div", { class: `toast ${kind}`, role: "status", text: message });
  region.append(item); setTimeout(() => item.remove(), 4500);
}
