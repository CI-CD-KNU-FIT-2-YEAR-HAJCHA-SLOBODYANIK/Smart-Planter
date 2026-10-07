import React from "react";

export function PlantCard({ plant, isSelected, onSelect }) {
  return (
    <div
      onClick={() => onSelect(plant.id)}
      style={{
        ...styles.card,
        border: isSelected ? "2px solid #166534" : "1px solid #e5e7eb",
        backgroundColor: isSelected ? "#f0fdf4" : "#ffffff",
      }}
    >
      <h3 style={styles.name}>{plant.name}</h3>
      <span style={styles.species}>{plant.species || "Вид не вказано"}</span>

      <div style={styles.thresholds}>
        <span>
          💧 {plant.minMoisture}–{plant.maxMoisture}%
        </span>
        <span>
          🌡 {plant.minTemp}–{plant.maxTemp}°C
        </span>
        <span>☀️ min {plant.minLight} lx</span>
      </div>
    </div>
  );
}

const styles = {
  card: {
    padding: 14,
    borderRadius: 10,
    cursor: "pointer",
    transition: "all 0.2s ease",
    boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
  },
  name: {
    margin: "0 0 4px 0",
    fontSize: 16,
    color: "#111827",
  },
  species: {
    fontSize: 13,
    color: "#6b7280",
  },
  thresholds: {
    display: "flex",
    flexWrap: "wrap",
    gap: 8,
    marginTop: 10,
    fontSize: 12,
    color: "#374151",
  },
};
