import React from "react";
import { Sprout, Plus, RefreshCw } from "lucide-react";

export function Header({ onRefresh, onOpenCreateModal }) {
  return (
    <header style={styles.header}>
      <div style={styles.brand}>
        <Sprout size={28} color="#166534" />
        <div>
          <h1 style={styles.title}>Smart Planter</h1>
          <p style={styles.subtitle}>Система моніторингу мікроклімату</p>
        </div>
      </div>
      <div style={styles.actions}>
        <button onClick={onOpenCreateModal} style={styles.btnPrimary}>
          <Plus size={16} /> Додати рослину
        </button>
        <button
          onClick={onRefresh}
          style={styles.btnSecondary}
          title="Оновити дані"
        >
          <RefreshCw size={16} />
        </button>
      </div>
    </header>
  );
}

const styles = {
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 20,
  },
  brand: {
    display: "flex",
    alignItems: "center",
    gap: 10,
  },
  title: {
    margin: 0,
    fontSize: 22,
    fontWeight: 700,
    color: "#166534",
  },
  subtitle: {
    margin: "2px 0 0 0",
    color: "#6b7280",
    fontSize: 13,
  },
  actions: {
    display: "flex",
    gap: 8,
  },
  btnPrimary: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    background: "#166534",
    color: "#fff",
    border: "none",
    padding: "8px 14px",
    borderRadius: 8,
    cursor: "pointer",
    fontWeight: 600,
  },
  btnSecondary: {
    background: "#e5e7eb",
    border: "none",
    padding: "8px 12px",
    borderRadius: 8,
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
  },
};
