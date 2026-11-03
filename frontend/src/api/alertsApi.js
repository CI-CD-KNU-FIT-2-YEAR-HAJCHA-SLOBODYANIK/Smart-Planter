import { apiFetch } from "./client";

export async function fetchActiveAlerts(plantId = null) {
  const query = plantId ? `?plantId=${plantId}` : "";
  return apiFetch(`/alerts/active${query}`);
}

export async function resolveAlert(alertId) {
  return apiFetch(`/alerts/${alertId}/resolve`, {
    method: "PUT",
  });
}
