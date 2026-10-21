import React, { useState } from "react";
import { Key, Copy, Check, Edit2, Trash2 } from "lucide-react";

export function PlantCard({ plant, isSelected, onSelect, onEdit, onDelete }) {
  const [copied, setCopied] = useState(false);

  const handleCopyKey = (e) => {
    e.stopPropagation();
    if (plant?.apiKey) {
      navigator.clipboard.writeText(plant.apiKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleEditClick = (e) => {
    e.stopPropagation();
    if (onEdit) onEdit(plant);
  };

  const handleDeleteClick = (e) => {
    e.stopPropagation();
    if (onDelete) onDelete(plant);
  };

  return (
    <div
      onClick={() => onSelect(plant.id)}
      style={{
        ...styles.card,
        border: isSelected ? "2px solid #166534" : "1px solid #e5e7eb",
        backgroundColor: isSelected ? "#f0fdf4" : "#ffffff",
      }}
    >
      <div style={styles.cardHeader}>
        <div>
          <h3 style={styles.name}>{plant.name}</h3>
          <span style={styles.species}>{plant.species || "Вид не вказано"}</span>
        </div>

        <div style={styles.headerActions}>
          {onEdit && (
            <button
              onClick={handleEditClick}
              style={styles.iconBtn}
              title="Редагувати параметри вазона"
            >
              <Edit2 size={14} color="#4b5563" />
            </button>
          )}
          {onDelete && (
            <button
              onClick={handleDeleteClick}
              style={{ ...styles.iconBtn, color: "#dc2626" }}
              title="Видалити рослину"
            >
              <Trash2 size={14} />
            </button>
          )}
        </div>
      </div>

      <div style={styles.thresholds}>
        <span style={styles.thresholdItem}>
          💧 {plant.minMoisture}–{plant.maxMoisture}%
        </span>
        <span style={styles.thresholdItem}>
          🌡 {plant.minTemp}–{plant.maxTemp}°C
        </span>
        <span style={styles.thresholdItem}>
          ☀️ min {plant.minLight} lx
        </span>
      </div>

      {plant.apiKey && (
        <div
          onClick={handleCopyKey}
          style={styles.apiKeyWrap}
          title="Натисніть, щоб скопіювати X-Device-Key для IoT сенсора"
        >
          <div style={{ display: "flex", alignItems: "center", gap: 5, overflow: "hidden" }}>
            <Key size={12} color="#0284c7" />
            <span style={styles.apiKeyLabel}>Key:</span>
            <code style={styles.apiKeyCode}>
              {plant.apiKey.substring(0, 8)}...{plant.apiKey.slice(-4)}
            </code>
          </div>

          <button style={styles.copyBtn}>
            {copied ? <Check size={12} color="#16a34a" /> : <Copy size={12} color="#6b7280" />}
            <span style={{ fontSize: 11, color: copied ? "#16a34a" : "#6b7280" }}>
              {copied ? "Є!" : "Копія"}
            </span>
          </button>
        </div>
      )}
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
    display: "flex",
    flexDirection: "column",
    justifyContent: "space-between",
  },
  cardHeader: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "flex-start",
    gap: 8,
  },
  headerActions: {
    display: "flex",
    alignItems: "center",
    gap: 4,
  },
  iconBtn: {
    background: "none",
    border: "none",
    padding: "4px 6px",
    borderRadius: 6,
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
  },
  name: {
    margin: "0 0 2px 0",
    fontSize: 16,
    color: "#111827",
  },
  species: {
    fontSize: 12,
    color: "#6b7280",
  },
  thresholds: {
    display: "flex",
    flexWrap: "wrap",
    gap: 6,
    marginTop: 10,
    marginBottom: 10,
  },
  thresholdItem: {
    fontSize: 11,
    color: "#374151",
    backgroundColor: "#f3f4f6",
    padding: "2px 6px",
    borderRadius: 4,
  },
  apiKeyWrap: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    padding: "4px 8px",
    backgroundColor: "#f8fafc",
    border: "1px dashed #cbd5e1",
    borderRadius: 6,
    fontSize: 11,
    marginTop: "auto",
  },
  apiKeyLabel: {
    color: "#64748b",
    fontWeight: 600,
  },
  apiKeyCode: {
    color: "#0f172a",
    fontFamily: "monospace",
  },
  copyBtn: {
    background: "none",
    border: "none",
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
    gap: 3,
    padding: 0,
  },
};
