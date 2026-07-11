import { el, formatDate } from "../dom.js";
function jsonPanel(title, value) { const details = el("details", {}, el("summary", { text: title }), el("pre", { class: "result-json", text: JSON.stringify(value, null, 2) })); return details; }
export function resultViewer(result) {
  const root = el("section", { class: "result", "aria-labelledby": "result-title" }, el("div", { class: "section-head" }, el("h2", { id: "result-title", text: result.isError ? "MCP tool error" : "Result" }), el("span", { class: result.isError ? "result-error" : "result-success", text: result.isError ? "Tool error" : "Success" })), el("p", { class: "muted", text: `${result.durationMilliseconds} ms · ${formatDate(result.completedAtUtc)}` }));
  if (result.wasTruncated) root.append(el("p", { class: "notice", text: "The result was truncated to the configured limit." }));
  for (const block of result.content || []) {
    const item = el("article", { class: "result-block" }, el("div", { class: "block-label", text: block.kind || "content" }));
    if (block.text !== undefined && block.text !== null) item.append(el("pre", { text: block.text }));
    else if (block.dataBase64 && block.mimeType?.startsWith("image/") && block.mimeType !== "image/svg+xml") item.append(el("img", { src: `data:${block.mimeType};base64,${block.dataBase64}`, alt: `Tool image result (${block.mimeType})` }));
    else item.append(el("pre", { text: JSON.stringify(block.raw || block, null, 2) })); root.append(item);
  }
  if (result.structuredContent) root.append(jsonPanel("Structured content", result.structuredContent)); root.append(jsonPanel("Raw MCP result", result.raw)); return root;
}
