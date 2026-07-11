export function route() {
  const parts = (location.hash || "#/servers").slice(2).split("/").map(decodeURIComponent);
  if (parts[0] !== "servers") return { page: "dashboard" };
  if (parts[1] === "new") return { page: "edit", id: null };
  if (!parts[1]) return { page: "dashboard" };
  if (parts[2] === "edit") return { page: "edit", id: parts[1] };
  if (parts[2] === "tools" && parts[3]) return { page: "tool", id: parts[1], tool: parts[3] };
  return { page: "details", id: parts[1] };
}
export function startRouter(render) { window.addEventListener("hashchange", render); render(); }
