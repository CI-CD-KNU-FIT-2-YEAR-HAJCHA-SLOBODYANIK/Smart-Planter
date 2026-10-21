import { apiFetch } from "./client";

export async function fetchTelemetryHistory(plantId, params = {}) {
  let query = "";
  if (typeof params === "number") {
    query = `?limit=${params}`;
  } else if (params && typeof params === "object") {
    const searchParams = new URLSearchParams();
    if (params.limit) searchParams.append("limit", params.limit);
    if (params.days) searchParams.append("days", params.days);
    if (params.hours) searchParams.append("hours", params.hours);
    if (params.from) searchParams.append("from", params.from);
    if (params.to) searchParams.append("to", params.to);
    const queryString = searchParams.toString();
    if (queryString) {
      query = `?${queryString}`;
    }
  }

  return apiFetch(`/telemetry/${plantId}/history${query}`);
}

export async function postTelemetry(deviceKey, telemetryData) {
  return apiFetch("/telemetry", {
    method: "POST",
    headers: {
      "X-Device-Key": deviceKey,
    },
    body: {
      moisture: Number(telemetryData.moisture),
      temperature: Number(telemetryData.temperature),
      light: Number(telemetryData.light),
      timestamp: telemetryData.timestamp || new Date().toISOString(),
    },
  });
}
