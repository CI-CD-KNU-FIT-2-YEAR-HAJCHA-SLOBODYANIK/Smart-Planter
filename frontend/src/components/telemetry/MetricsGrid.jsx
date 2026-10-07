import React from "react";
import { Droplets, Thermometer, Sun } from "lucide-react";

export function MetricsGrid({ latestTelemetry, plant }) {
  return (
    <div style={styles.grid}>
      <div style={styles.card}>
        <div style={{ ...styles.cardHeader, color: "#0284c7" }}>
          <Droplets size={18} /> Вологість ґрунту
        </div>
        <div style={styles.value}>
          {latestTelemetry ? `${latestTelemetry.moisture.toFixed(1)}%` : "--"}
        </div>
        <div style={styles.limits}>
          Норма: {plant.minMoisture}–{plant.maxMoisture}%
        </div>
      </div>

      <div style={styles.card}>
        <div style={{ ...styles.cardHeader, color: "#dc2626" }}>
          <Thermometer size={18} /> Температура
        </div>
        <div style={styles.value}>
          {latestTelemetry
            ? `${latestTelemetry.temperature.toFixed(1)}°C`
            : "--"}
        </div>
        <div style={styles.limits}>
          Норма: {plant.minTemp}–{plant.maxTemp}°C
        </div>
      </div>

      <div style={styles.card}>
        <div style={{ ...styles.cardHeader, color: "#d97706" }}>
          <Sun size={18} /> Освітленість
        </div>
        <div style={styles.value}>
          {latestTelemetry ? `${latestTelemetry.light.toFixed(0)} Lux` : "--"}
        </div>
        <div style={styles.limits}>Мінімум: {plant.minLight} Lux</div>
      </div>
    </div>
  );
}

const styles = {
  grid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
    gap: 12,
    marginBottom: 16,
  },
  card: {
    background: "#f9fafb",
    padding: 14,
    borderRadius: 8,
    border: "1px solid #f3f4f6",
  },
  cardHeader: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    fontSize: 13,
    fontWeight: 600,
  },
  value: {
    fontSize: 24,
    fontWeight: 700,
    margin: "6px 0",
    color: "#111827",
  },
  limits: {
    fontSize: 12,
    color: "#6b7280",
  },
};
