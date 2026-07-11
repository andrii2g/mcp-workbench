import { el } from "../dom.js";
export function statusBadge(status = "disconnected") { return el("span", { class: `status status-${status}`, text: status[0].toUpperCase() + status.slice(1), "aria-label": `Status: ${status}` }); }
