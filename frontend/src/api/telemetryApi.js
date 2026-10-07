const BASE_URL = "http://localhost:8000/api/v1/telemetry";

export async function fetchTelemetryHistory(plantId, limit = 20) {
  const response = await fetch(`${BASE_URL}/${plantId}/history?limit=${limit}`);
  if (!response.ok)
    throw new Error("Не вдалося завантажити історію телеметрії");
  return response.json();
}
