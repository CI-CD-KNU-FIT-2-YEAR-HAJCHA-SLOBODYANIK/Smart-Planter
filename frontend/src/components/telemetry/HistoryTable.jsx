import React, { useState } from "react";
import { Activity, AlertTriangle, ArrowUpDown } from "lucide-react";

export function HistoryTable({ history = [], plant }) {
  const [maxDisplay, setMaxDisplay] = useState(15);

  if (!history || history.length === 0) {
    return (
      <div style={styles.empty}>
        <Activity size={18} color="#9ca3af" />
        <span>Дані історії телеметрії поки що відсутні.</span>
      </div>
    );
  }

  const displayedHistory = history.slice(0, maxDisplay);

  return (
    <div style={{ marginTop: 24 }}>
      <div style={styles.header}>
        <h4 style={styles.title}>
          <Activity size={16} color="#166534" /> Останні пакети вимірювань ({history.length})
        </h4>

        {history.length > 15 && (
          <div style={styles.limitBtns}>
            <button
              onClick={() => setMaxDisplay(15)}
              style={{
                ...styles.limitBtn,
                fontWeight: maxDisplay === 15 ? 700 : 400,
                color: maxDisplay === 15 ? "#166534" : "#6b7280",
              }}
            >
              15 записів
            </button>
            <button
              onClick={() => setMaxDisplay(50)}
              style={{
                ...styles.limitBtn,
                fontWeight: maxDisplay === 50 ? 700 : 400,
                color: maxDisplay === 50 ? "#166534" : "#6b7280",
              }}
            >
              50 записів
            </button>
            {history.length > 50 && (
              <button
                onClick={() => setMaxDisplay(history.length)}
                style={{
                  ...styles.limitBtn,
                  fontWeight: maxDisplay === history.length ? 700 : 400,
                  color: maxDisplay === history.length ? "#166534" : "#6b7280",
                }}
              >
                Усі ({history.length})
              </button>
            )}
          </div>
        )}
      </div>

      <div style={styles.tableWrap}>
        <table style={styles.table}>
          <thead>
            <tr style={styles.thRow}>
              <th style={styles.th}>Час заміру</th>
              <th style={styles.th}>Вологість ґрунту</th>
              <th style={styles.th}>Температура</th>
              <th style={styles.th}>Освітленість</th>
              <th style={styles.th}>Статус</th>
            </tr>
          </thead>
          <tbody>
            {displayedHistory.map((entry) => {
              const isMoistureAlert = plant && (entry.moisture < plant.minMoisture || entry.moisture > plant.maxMoisture);
              const isTempAlert = plant && (entry.temperature < plant.minTemp || entry.temperature > plant.maxTemp);
              const isLightAlert = plant && (entry.light < plant.minLight);
              const hasAnyAlert = isMoistureAlert || isTempAlert || isLightAlert;

              return (
                <tr
                  key={entry.id || entry.timestamp}
                  style={{
                    ...styles.tr,
                    backgroundColor: hasAnyAlert ? "#fffbeb" : "#ffffff",
                  }}
                >
                  <td style={styles.td}>
                    <div style={{ fontWeight: 500 }}>
                      {new Date(entry.timestamp).toLocaleTimeString()}
                    </div>
                    <div style={{ fontSize: 11, color: "#9ca3af" }}>
                      {new Date(entry.timestamp).toLocaleDateString()}
                    </div>
                  </td>

                  <td style={styles.td}>
                    <span
                      style={{
                        color: isMoistureAlert ? "#dc2626" : "#0284c7",
                        fontWeight: isMoistureAlert ? 700 : 500,
                      }}
                    >
                      {entry.moisture.toFixed(1)}%
                    </span>
                    {isMoistureAlert && (
                      <span style={styles.pillAlert} title="Вихід за межі норми">
                        {entry.moisture < plant.minMoisture ? "сухо" : "перелив"}
                      </span>
                    )}
                  </td>

                  <td style={styles.td}>
                    <span
                      style={{
                        color: isTempAlert ? "#dc2626" : "#374151",
                        fontWeight: isTempAlert ? 700 : 500,
                      }}
                    >
                      {entry.temperature.toFixed(1)}°C
                    </span>
                    {isTempAlert && (
                      <span style={styles.pillAlert} title="Вихід за межі норми">
                        {entry.temperature < plant.minTemp ? "холодно" : "жарко"}
                      </span>
                    )}
                  </td>

                  <td style={styles.td}>
                    <span
                      style={{
                        color: isLightAlert ? "#d97706" : "#374151",
                        fontWeight: isLightAlert ? 700 : 500,
                      }}
                    >
                      {entry.light.toFixed(0)} Lux
                    </span>
                    {isLightAlert && (
                      <span style={styles.pillAlert} title="Нижче норми">
                        мало світла
                      </span>
                    )}
                  </td>

                  <td style={styles.td}>
                    {hasAnyAlert ? (
                      <span style={styles.badgeWarn}>
                        <AlertTriangle size={12} /> Відхилення
                      </span>
                    ) : (
                      <span style={styles.badgeOk}>В нормі</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const styles = {
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 8,
  },
  title: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    margin: 0,
    fontSize: 14,
    color: "#374151",
  },
  limitBtns: {
    display: "flex",
    gap: 8,
  },
  limitBtn: {
    background: "none",
    border: "none",
    fontSize: 12,
    cursor: "pointer",
    padding: "2px 4px",
  },
  tableWrap: {
    overflowX: "auto",
    border: "1px solid #e5e7eb",
    borderRadius: 8,
    backgroundColor: "#ffffff",
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
    padding: "8px 12px",
    color: "#4b5563",
    fontWeight: 600,
    fontSize: 12,
  },
  tr: {
    borderBottom: "1px solid #f3f4f6",
    transition: "background-color 0.1s ease",
  },
  td: {
    padding: "8px 12px",
    color: "#1f2937",
    verticalAlign: "middle",
  },
  pillAlert: {
    marginLeft: 6,
    fontSize: 10,
    fontWeight: 600,
    padding: "1px 5px",
    borderRadius: 4,
    backgroundColor: "#fee2e2",
    color: "#dc2626",
  },
  badgeOk: {
    display: "inline-block",
    fontSize: 11,
    padding: "2px 8px",
    borderRadius: 12,
    backgroundColor: "#dcfce7",
    color: "#16a34a",
    fontWeight: 600,
  },
  badgeWarn: {
    display: "inline-flex",
    alignItems: "center",
    gap: 4,
    fontSize: 11,
    padding: "2px 8px",
    borderRadius: 12,
    backgroundColor: "#fef3c7",
    color: "#b45309",
    fontWeight: 600,
  },
  empty: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    padding: 16,
    color: "#6b7280",
    fontSize: 13,
    backgroundColor: "#f9fafb",
    borderRadius: 8,
  },
};
