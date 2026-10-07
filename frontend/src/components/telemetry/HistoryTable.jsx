import React from "react";
import { Activity } from "lucide-react";

export function HistoryTable({ history }) {
  if (!history || history.length === 0) {
    return (
      <p style={{ color: "#6b7280", fontSize: 13 }}>
        Дані телеметрії поки що відсутні.
      </p>
    );
  }

  return (
    <div>
      <h4 style={styles.title}>
        <Activity size={16} /> Останні пакети вимірювань
      </h4>
      <div style={styles.tableWrap}>
        <table style={styles.table}>
          <thead>
            <tr style={styles.thRow}>
              <th style={styles.th}>Час</th>
              <th style={styles.th}>Вологість</th>
              <th style={styles.th}>Температура</th>
              <th style={styles.th}>Освітленість</th>
            </tr>
          </thead>
          <tbody>
            {history.map((entry) => (
              <tr key={entry.id} style={styles.tr}>
                <td style={styles.td}>
                  {new Date(entry.timestamp).toLocaleTimeString()}
                </td>
                <td style={styles.td}>{entry.moisture.toFixed(1)}%</td>
                <td style={styles.td}>{entry.temperature.toFixed(1)}°C</td>
                <td style={styles.td}>{entry.light.toFixed(0)} Lux</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const styles = {
  title: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    margin: "16px 0 8px 0",
    fontSize: 14,
    color: "#374151",
  },
  tableWrap: {
    overflowX: "auto",
    border: "1px solid #e5e7eb",
    borderRadius: 8,
  },
  table: {
    width: "100%",
    borderCollapse: "collapse",
    fontSize: 13,
  },
  thRow: {
    background: "#f9fafb",
    textAlign: "left",
    borderBottom: "1px solid #e5e7eb",
  },
  th: {
    padding: "8px 10px",
    color: "#4b5563",
    fontWeight: 600,
  },
  tr: {
    borderBottom: "1px solid #f3f4f6",
  },
  td: {
    padding: "8px 10px",
    color: "#1f2937",
  },
};
