const BASE_URL = "http://localhost:5000/api/v1/alerts";

export async function fetchActiveAlerts() {
  const response = await fetch(`${BASE_URL}/active`);
  if (!response.ok) throw new Error("Не вдалося завантажити активні алерти");
  return response.json();
}

export async function resolveAlert(alertId) {
  const response = await fetch(`${BASE_URL}/${alertId}/resolve`, {
    method: "PUT",
  });
  if (!response.ok) throw new Error("Не вдалося закрити інцидент");
  return true;
}
