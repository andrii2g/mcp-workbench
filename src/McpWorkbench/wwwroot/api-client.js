const base = "/api/v1";
let apiKeyProvider = null;
export class ApiError extends Error { constructor(payload, status) { super(payload?.error?.message || `Request failed (${status})`); this.code = payload?.error?.code || "request_failed"; this.details = payload?.error?.details || []; this.requestId = payload?.meta?.requestId; this.status = status; } }
export function setApiKeyProvider(provider) { apiKeyProvider = provider; }
async function request(path, options = {}, retry = true) {
  const apiKey = sessionStorage.getItem("mcp-workbench-api-key");
  const response = await fetch(`${base}${path}`, { ...options, headers: { "Content-Type": "application/json", ...(apiKey ? { "X-Mcp-Workbench-Key": apiKey } : {}), ...options.headers } });
  if (response.status === 401 && retry && apiKeyProvider) {
    sessionStorage.removeItem("mcp-workbench-api-key");
    const supplied = await apiKeyProvider();
    if (supplied) { sessionStorage.setItem("mcp-workbench-api-key", supplied); return request(path, options, false); }
  }
  if (response.status === 204) return null;
  const payload = await response.json().catch(() => null);
  if (!response.ok) throw new ApiError(payload, response.status);
  return payload.data;
}
export const api = {
  health: () => fetch("/health/ready").then(r => r.ok).catch(() => false),
  servers: () => request("/servers?includeRuntime=true"),
  server: id => request(`/servers/${id}`),
  create: body => request("/servers", { method: "POST", body: JSON.stringify(body) }),
  update: (id, body) => request(`/servers/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  remove: id => request(`/servers/${id}`, { method: "DELETE" }),
  connect: (id, forceReconnect = false) => request(`/servers/${id}/connect`, { method: "POST", body: JSON.stringify({ forceReconnect }) }),
  disconnect: id => request(`/servers/${id}/disconnect`, { method: "POST" }),
  ping: id => request(`/servers/${id}/ping`, { method: "POST" }),
  tools: (id, refresh = false) => request(`/servers/${id}/tools${refresh ? "?refresh=true" : ""}`),
  invoke: (id, tool, body, signal) => request(`/servers/${id}/tools/${encodeURIComponent(tool)}/invoke`, { method: "POST", body: JSON.stringify(body), signal })
};
