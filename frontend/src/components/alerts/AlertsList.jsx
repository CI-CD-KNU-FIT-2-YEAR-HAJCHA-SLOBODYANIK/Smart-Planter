import React from "react";
import { AlertTriangle, CheckCircle } from "lucide-react";

export function AlertsList({ alerts, onResolve }) {
  if (!alerts || alerts.length === 0) return null;

  return (
    <div style={styles.container}>
      <h3 style={styles.header}>
        <AlertTriangle size={18} /> Активні тривоги ({alerts.length})
      </h3>
      <div style={styles.list}>
        {alerts.map((alert) => (
          <div key={alert.id} style={styles.card}>
            <div>
              <strong>{alert.plantName || `Рослина #${alert.plantId}`}:</strong>{" "}
              {alert.message}
              <div style={styles.time}>
                {new Date(alert.createdAt).toLocaleTimeString()}
              </div>
            </div>
            <button
              onClick={() => onResolve(alert.id)}
              style={styles.btnResolve}
            >
              <CheckCircle size={14} /> Вирішено
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

const styles = {
  container: {
    background: "#fef2f2",
    border: "1px solid #fecaca",
    borderRadius: 10,
    padding: 12,
    marginBottom: 20,
  },
  header: {
    margin: "0 0 8px 0",
    display: "flex",
    alignItems: "center",
    gap: 6,
    color: "#991b1b",
    fontSize: 15,
  },
  list: {
    display: "flex",
    flexDirection: "column",
    gap: 8,
  },
  card: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    background: "#fff",
    padding: "8px 12px",
    borderRadius: 6,
    border: "1px solid #fee2e2",
  },
  time: {
    fontSize: 12,
    color: "#6b7280",
  },
  btnResolve: {
    display: "flex",
    alignItems: "center",
    gap: 4,
    background: "#22c55e",
    color: "#fff",
    border: "none",
    padding: "6px 10px",
    borderRadius: 6,
    cursor: "pointer",
    fontSize: 13,
    fontWeight: 600,
  },
};
