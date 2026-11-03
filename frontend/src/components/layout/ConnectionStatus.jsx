import { Radio, RefreshCw } from "lucide-react";

export function ConnectionStatus({ status, onReconnect, isPollingActive }) {
  let badgeColor = "#6b7280";
  let bgColor = "#f3f4f6";
  let label = "Відключено";
  let isLive = false;

  if (status === "connected") {
    badgeColor = "#16a34a";
    bgColor = "#dcfce7";
    label = "Live (SignalR)";
    isLive = true;
  } else if (status === "connecting" || status === "reconnecting") {
    badgeColor = "#d97706";
    bgColor = "#fef3c7";
    label = status === "connecting" ? "Підключення..." : "Перепідключення...";
  } else if (isPollingActive) {
    badgeColor = "#0284c7";
    bgColor = "#e0f2fe";
    label = "Опитування (5с)";
  }

  return (
    <div
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 6,
        padding: "4px 10px",
        borderRadius: 20,
        backgroundColor: bgColor,
        color: badgeColor,
        fontSize: 12,
        fontWeight: 600,
        border: `1px solid ${badgeColor}33`,
      }}
      title={isLive ? "Отримання даних у реальному часі через WebSocket/SignalR" : "Регулярне опитування бекенду"}
    >
      <Radio
        size={14}
        style={{
          animation: isLive ? "pulse 1.5s infinite" : "none",
        }}
      />
      <span>{label}</span>
      {status === "disconnected" && onReconnect && (
        <button
          onClick={onReconnect}
          style={{
            background: "none",
            border: "none",
            color: badgeColor,
            cursor: "pointer",
            display: "inline-flex",
            alignItems: "center",
            padding: 0,
            marginLeft: 4,
          }}
          title="Спробувати підключитись знову"
        >
          <RefreshCw size={12} />
        </button>
      )}
    </div>
  );
}
