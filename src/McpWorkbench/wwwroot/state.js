export const state = { servers: [], search: "", currentAbort: null };
export function setServers(servers) { state.servers = servers; window.dispatchEvent(new CustomEvent("servers-changed")); }
export function serverById(id) { return state.servers.find(item => item.id === id); }
