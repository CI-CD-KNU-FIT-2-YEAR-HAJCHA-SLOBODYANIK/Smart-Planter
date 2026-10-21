import React, { useState } from "react";
import {
  AlertTriangle,
  CheckCircle,
  Droplets,
  ThermometerSnowflake,
  Flame,
  SunDim,
  Filter,
} from "lucide-react";

export function AlertsList({ alerts = [], onResolve, onWaterAndResolve, selectedPlantId }) {
  const [filterCurrentOnly, setFilterCurrentOnly] = useState(false);

  if (!alerts || alerts.length === 0) return null;

  const displayedAlerts = filterCurrentOnly && selectedPlantId
    ? alerts.filter((a) => a.plantId === selectedPlantId)
    : alerts;

  const getAlertMeta = (type) => {
    switch (type) {
      case 0:
        return {
          label: "Посуха / Низька вологість",
          icon: <Droplets size={16} color="#0284c7" />,
          color: "#0284c7",
          bgColor: "#e0f2fe",
          canWater: true,
        };
      case 1:
        return {
          label: "Перелив ґрунту",
          icon: <Droplets size={16} color="#d97706" />,
          color: "#d97706",
          bgColor: "#fef3c7",
          canWater: false,
        };
      case 2:
        return {
          label: "Критично холодно",
          icon: <ThermometerSnowflake size={16} color="#0284c7" />,
          color: "#0284c7",
          bgColor: "#e0f2fe",
          canWater: false,
        };
      case 3:
        return {
          label: "Перегрів / Спека",
          icon: <Flame size={16} color="#dc2626" />,
          color: "#dc2626",
          bgColor: "#fee2e2",
          canWater: false,
        };
      case 4:
        return {
          label: "Недостатньо світла",
          icon: <SunDim size={16} color="#d97706" />,
          color: "#d97706",
          bgColor: "#fef3c7",
          canWater: false,
        };
      default:
        return {
          label: "Попередження",
          icon: <AlertTriangle size={16} color="#dc2626" />,
          color: "#dc2626",
          bgColor: "#fee2e2",
          canWater: false,
        };
    }
  };

  return (
    <div style={styles.container}>
      <div style={styles.headerRow}>
        <h3 style={styles.header}>
          <AlertTriangle size={18} color="#b91c1c" />
          <span>Активні аварії та сповіщення ({alerts.length})</span>
        </h3>

        {selectedPlantId && alerts.length > 1 && (
          <button
            onClick={() => setFilterCurrentOnly((prev) => !prev)}
            style={{
              ...styles.filterBtn,
              backgroundColor: filterCurrentOnly ? "#b91c1c" : "#ffffff",
              color: filterCurrentOnly ? "#ffffff" : "#b91c1c",
            }}
          >
            <Filter size={12} />
            {filterCurrentOnly ? "Показати всі" : "Тільки обраний вазон"}
          </button>
        )}
      </div>

      {displayedAlerts.length === 0 ? (
        <div style={styles.noAlertsNotice}>
          Для обраного вазона активних аварійних сповіщень немає.
        </div>
      ) : (
        <div style={styles.list}>
          {displayedAlerts.map((alert) => {
            const meta = getAlertMeta(alert.type);

            return (
              <div key={alert.id} style={styles.card}>
                <div style={styles.contentWrap}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
                    <div
                      style={{
                        padding: "4px 8px",
                        borderRadius: 6,
                        backgroundColor: meta.bgColor,
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 5,
                        fontSize: 11,
                        fontWeight: 700,
                        color: meta.color,
                      }}
                    >
                      {meta.icon}
                      <span>{meta.label}</span>
                    </div>

                    <span style={{ fontSize: 13, fontWeight: 700, color: "#111827" }}>
                      {alert.plantName || `Рослина #${alert.plantId}`}
                    </span>
                  </div>

                  <div style={styles.msgText}>{alert.message}</div>

                  <div style={styles.time}>
                    Зафіксовано: {new Date(alert.createdAt).toLocaleString()}
                  </div>
                </div>

                <div style={styles.actionsWrap}>
                  {meta.canWater && onWaterAndResolve && (
                    <button
                      onClick={() => onWaterAndResolve(alert.plantId, alert.id)}
                      style={styles.btnWater}
                      title="Автоматично запустити полив і закрити цю тривогу"
                    >
                      💧 Полити та закрити
                    </button>
                  )}

                  <button
                    onClick={() => onResolve(alert.id)}
                    style={styles.btnResolve}
                    title="Позначити аварію як вирішену"
                  >
                    <CheckCircle size={14} /> Закрити
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

const styles = {
  container: {
    background: "#fff5f5",
    border: "1px solid #fecaca",
    borderRadius: 12,
    padding: 14,
    marginBottom: 20,
    boxShadow: "0 1px 3px rgba(220, 38, 38, 0.08)",
  },
  headerRow: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 10,
    flexWrap: "wrap",
    gap: 8,
  },
  header: {
    margin: 0,
    display: "flex",
    alignItems: "center",
    gap: 8,
    color: "#991b1b",
    fontSize: 15,
    fontWeight: 700,
  },
  filterBtn: {
    display: "flex",
    alignItems: "center",
    gap: 5,
    padding: "4px 10px",
    borderRadius: 6,
    border: "1px solid #f87171",
    fontSize: 12,
    fontWeight: 600,
    cursor: "pointer",
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
    flexWrap: "wrap",
    gap: 12,
    background: "#ffffff",
    padding: "10px 14px",
    borderRadius: 8,
    border: "1px solid #fee2e2",
    boxShadow: "0 1px 2px rgba(0,0,0,0.03)",
  },
  contentWrap: {
    flex: 1,
    minWidth: 240,
  },
  msgText: {
    fontSize: 13,
    color: "#374151",
    marginBottom: 4,
  },
  time: {
    fontSize: 11,
    color: "#9ca3af",
  },
  actionsWrap: {
    display: "flex",
    alignItems: "center",
    gap: 8,
  },
  btnWater: {
    display: "flex",
    alignItems: "center",
    gap: 4,
    background: "#0284c7",
    color: "#fff",
    border: "none",
    padding: "6px 12px",
    borderRadius: 6,
    cursor: "pointer",
    fontSize: 12,
    fontWeight: 600,
    boxShadow: "0 1px 2px rgba(2, 132, 199, 0.2)",
  },
  btnResolve: {
    display: "flex",
    alignItems: "center",
    gap: 4,
    background: "#16a34a",
    color: "#fff",
    border: "none",
    padding: "6px 12px",
    borderRadius: 6,
    cursor: "pointer",
    fontSize: 12,
    fontWeight: 600,
  },
  noAlertsNotice: {
    padding: "8px 12px",
    color: "#6b7280",
    fontSize: 12,
    backgroundColor: "#ffffff",
    borderRadius: 6,
    border: "1px dashed #fca5a5",
  },
};
