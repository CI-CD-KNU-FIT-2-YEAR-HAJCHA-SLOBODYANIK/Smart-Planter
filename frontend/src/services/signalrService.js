import * as signalR from "@microsoft/signalr";
import { getApiBaseUrl, getAuthToken } from "../api/client";

let hubConnection = null;
let currentStatus = "disconnected";

export function getSignalRHubUrl() {
  const apiUrl = getApiBaseUrl();
  // Strip trailing /api/v1 or similar to get base domain
  const rootUrl = apiUrl.replace(/\/api\/v1\/?$/, "");
  return `${rootUrl}/hubs/telemetry`;
}

export function getSignalRStatus() {
  return currentStatus;
}

export async function startSignalR({
  onTelemetry,
  onAlert,
  onWatering,
  onStatusChange,
}) {
  const hubUrl = getSignalRHubUrl();

  // If connection already exists, stop it cleanly
  if (hubConnection) {
    try {
      await hubConnection.stop();
    } catch {
      // ignore
    }
    hubConnection = null;
  }

  const updateStatus = (status) => {
    currentStatus = status;
    if (onStatusChange) {
      onStatusChange(status);
    }
  };

  updateStatus("connecting");

  const connectionBuilder = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => getAuthToken() || "",
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning);

  hubConnection = connectionBuilder.build();

  hubConnection.on("ReceiveTelemetry", (data) => {
    if (onTelemetry) {
      onTelemetry(data);
    }
  });

  hubConnection.on("ReceiveAlert", (data) => {
    if (onAlert) {
      onAlert(data);
    }
  });

  hubConnection.on("ReceiveWatering", (data) => {
    if (onWatering) {
      onWatering(data);
    }
  });

  hubConnection.onreconnecting((err) => {
    console.warn("SignalR reconnecting:", err);
    updateStatus("reconnecting");
  });

  hubConnection.onreconnected(() => {
    console.info("SignalR reconnected.");
    updateStatus("connected");
  });

  hubConnection.onclose(() => {
    updateStatus("disconnected");
  });

  try {
    await hubConnection.start();
    updateStatus("connected");
    return hubConnection;
  } catch (err) {
    console.warn("SignalR connection failed (will rely on polling fallback):", err?.message || err);
    updateStatus("disconnected");
    return null;
  }
}

export async function stopSignalR() {
  if (hubConnection) {
    try {
      await hubConnection.stop();
    } catch {
      // ignore
    }
    hubConnection = null;
    currentStatus = "disconnected";
  }
}
