import { useState, useEffect } from "react";
import { Droplets, Thermometer, Sun, AlertTriangle, CheckCircle2, Clock } from "lucide-react";

function getTimeAgo(timestamp, now) {
  if (!timestamp) return "";
  const time = new Date(timestamp).getTime();
  const diffSec = Math.floor((now - time) / 1000);
  if (diffSec < 5) return "тільки що";
  if (diffSec < 60) return `${diffSec} сек тому`;
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)} хв тому`;
  return new Date(timestamp).toLocaleTimeString();
}

export function MetricsGrid({ latestTelemetry, plant }) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 5000);
    return () => clearInterval(timer);
  }, []);

  const timeAgo = getTimeAgo(latestTelemetry?.timestamp, now);

  if (!plant) return null;

  // Status checks
  const getMoistureStatus = () => {
    if (!latestTelemetry) return { label: "Немає даних", color: "#6b7280", state: "idle" };
    const val = latestTelemetry.moisture;
    if (val < plant.minMoisture) {
      return { label: "Потрібен полив!", color: "#dc2626", state: "critical" };
    }
    if (val > plant.maxMoisture) {
      return { label: "Перелив!", color: "#d97706", state: "warning" };
    }
    return { label: "В нормі", color: "#16a34a", state: "ok" };
  };

  const getTempStatus = () => {
    if (!latestTelemetry) return { label: "Немає даних", color: "#6b7280", state: "idle" };
    const val = latestTelemetry.temperature;
    if (val < plant.minTemp) {
      return { label: "Занадто холодно!", color: "#0284c7", state: "critical" };
    }
    if (val > plant.maxTemp) {
      return { label: "Перегрів!", color: "#dc2626", state: "critical" };
    }
    return { label: "В нормі", color: "#16a34a", state: "ok" };
  };

  const getLightStatus = () => {
    if (!latestTelemetry) return { label: "Немає даних", color: "#6b7280", state: "idle" };
    const val = latestTelemetry.light;
    if (val < plant.minLight) {
      return { label: "Недостатньо світла", color: "#d97706", state: "warning" };
    }
    return { label: "Оптимальне", color: "#16a34a", state: "ok" };
  };

  const moistureStatus = getMoistureStatus();
  const tempStatus = getTempStatus();
  const lightStatus = getLightStatus();

  return (
    <div>
      <div style={styles.grid}>
        {/* Moisture Card */}
        <div style={{ ...styles.card, borderColor: moistureStatus.state !== "ok" && moistureStatus.state !== "idle" ? moistureStatus.color : "#e5e7eb" }}>
          <div style={styles.cardTop}>
            <div style={{ ...styles.cardHeader, color: "#0284c7" }}>
              <Droplets size={18} /> Вологість ґрунту
            </div>
            <span
              style={{
                ...styles.badge,
                backgroundColor: `${moistureStatus.color}15`,
                color: moistureStatus.color,
                borderColor: `${moistureStatus.color}40`,
              }}
            >
              {moistureStatus.state === "ok" ? <CheckCircle2 size={12} /> : <AlertTriangle size={12} />}
              {moistureStatus.label}
            </span>
          </div>

          <div style={styles.value}>
            {latestTelemetry ? `${latestTelemetry.moisture.toFixed(1)}%` : "--"}
          </div>

          <div style={styles.limits}>
            Оптимум: <b>{plant.minMoisture}% – {plant.maxMoisture}%</b>
          </div>

          {/* Simple gauge progress bar */}
          <div style={styles.barBg}>
            <div
              style={{
                ...styles.barFill,
                width: `${Math.min(100, Math.max(0, latestTelemetry ? latestTelemetry.moisture : 0))}%`,
                backgroundColor: moistureStatus.color,
              }}
            />
          </div>
        </div>

        {/* Temperature Card */}
        <div style={{ ...styles.card, borderColor: tempStatus.state !== "ok" && tempStatus.state !== "idle" ? tempStatus.color : "#e5e7eb" }}>
          <div style={styles.cardTop}>
            <div style={{ ...styles.cardHeader, color: "#dc2626" }}>
              <Thermometer size={18} /> Температура
            </div>
            <span
              style={{
                ...styles.badge,
                backgroundColor: `${tempStatus.color}15`,
                color: tempStatus.color,
                borderColor: `${tempStatus.color}40`,
              }}
            >
              {tempStatus.state === "ok" ? <CheckCircle2 size={12} /> : <AlertTriangle size={12} />}
              {tempStatus.label}
            </span>
          </div>

          <div style={styles.value}>
            {latestTelemetry ? `${latestTelemetry.temperature.toFixed(1)}°C` : "--"}
          </div>

          <div style={styles.limits}>
            Оптимум: <b>{plant.minTemp}°C – {plant.maxTemp}°C</b>
          </div>

          <div style={styles.barBg}>
            <div
              style={{
                ...styles.barFill,
                width: `${Math.min(100, Math.max(0, latestTelemetry ? ((latestTelemetry.temperature - 10) / 35) * 100 : 0))}%`,
                backgroundColor: tempStatus.color,
              }}
            />
          </div>
        </div>

        {/* Light Card */}
        <div style={{ ...styles.card, borderColor: lightStatus.state !== "ok" && lightStatus.state !== "idle" ? lightStatus.color : "#e5e7eb" }}>
          <div style={styles.cardTop}>
            <div style={{ ...styles.cardHeader, color: "#d97706" }}>
              <Sun size={18} /> Освітленість
            </div>
            <span
              style={{
                ...styles.badge,
                backgroundColor: `${lightStatus.color}15`,
                color: lightStatus.color,
                borderColor: `${lightStatus.color}40`,
              }}
            >
              {lightStatus.state === "ok" ? <CheckCircle2 size={12} /> : <AlertTriangle size={12} />}
              {lightStatus.label}
            </span>
          </div>

          <div style={styles.value}>
            {latestTelemetry ? `${latestTelemetry.light.toFixed(0)} Lux` : "--"}
          </div>

          <div style={styles.limits}>
            Мінімум: <b>{plant.minLight} Lux</b>
          </div>

          <div style={styles.barBg}>
            <div
              style={{
                ...styles.barFill,
                width: `${Math.min(100, Math.max(0, latestTelemetry ? (latestTelemetry.light / (plant.minLight * 2)) * 100 : 0))}%`,
                backgroundColor: lightStatus.color,
              }}
            />
          </div>
        </div>
      </div>

      {latestTelemetry?.timestamp && (
        <div style={styles.timestampWrap}>
          <Clock size={13} color="#6b7280" />
          <span>Останній замір телеметрії: <b>{new Date(latestTelemetry.timestamp).toLocaleString()}</b> ({timeAgo})</span>
        </div>
      )}
    </div>
  );
}

const styles = {
  grid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
    gap: 12,
    marginBottom: 12,
  },
  card: {
    background: "#f9fafb",
    padding: 14,
    borderRadius: 10,
    border: "1px solid #e5e7eb",
    display: "flex",
    flexDirection: "column",
    justifyContent: "space-between",
    transition: "border-color 0.2s ease",
  },
  cardTop: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    gap: 6,
  },
  cardHeader: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    fontSize: 13,
    fontWeight: 600,
  },
  badge: {
    display: "inline-flex",
    alignItems: "center",
    gap: 4,
    fontSize: 11,
    fontWeight: 600,
    padding: "2px 8px",
    borderRadius: 12,
    border: "1px solid transparent",
  },
  value: {
    fontSize: 26,
    fontWeight: 700,
    margin: "10px 0 4px 0",
    color: "#111827",
  },
  limits: {
    fontSize: 12,
    color: "#4b5563",
    marginBottom: 8,
  },
  barBg: {
    width: "100%",
    height: 6,
    backgroundColor: "#e5e7eb",
    borderRadius: 4,
    overflow: "hidden",
  },
  barFill: {
    height: "100%",
    borderRadius: 4,
    transition: "width 0.4s ease, background-color 0.4s ease",
  },
  timestampWrap: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    fontSize: 12,
    color: "#6b7280",
    marginBottom: 16,
    padding: "4px 8px",
    backgroundColor: "#f3f4f6",
    borderRadius: 6,
  },
};
