import React, { useState } from "react";
import { X } from "lucide-react";

export function AddPlantModal({ isOpen, onClose, onCreate }) {
  const [formData, setFormData] = useState({
    name: "",
    species: "",
    minMoisture: 30,
    maxMoisture: 70,
    minTemp: 18,
    maxTemp: 28,
    minLight: 300,
  });

  if (!isOpen) return null;

  const handleSubmit = (e) => {
    e.preventDefault();
    onCreate({
      name: formData.name,
      species: formData.species,
      minMoisture: Number(formData.minMoisture),
      maxMoisture: Number(formData.maxMoisture),
      minTemp: Number(formData.minTemp),
      maxTemp: Number(formData.maxTemp),
      minLight: Number(formData.minLight),
    });
  };

  return (
    <div style={styles.overlay}>
      <div style={styles.modal}>
        <div style={styles.modalHeader}>
          <h3 style={{ margin: 0 }}>Нова рослина</h3>
          <button onClick={onClose} style={styles.closeBtn}>
            <X size={20} />
          </button>
        </div>
        <form onSubmit={handleSubmit} style={styles.form}>
          <input
            placeholder="Назва рослини (напр. Монстера)"
            required
            style={styles.input}
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
          />
          <input
            placeholder="Вид / сорт"
            style={styles.input}
            value={formData.species}
            onChange={(e) =>
              setFormData({ ...formData, species: e.target.value })
            }
          />
          <div style={styles.row}>
            <input
              type="number"
              placeholder="Min Вологість %"
              style={styles.input}
              value={formData.minMoisture}
              onChange={(e) =>
                setFormData({ ...formData, minMoisture: e.target.value })
              }
            />
            <input
              type="number"
              placeholder="Max Вологість %"
              style={styles.input}
              value={formData.maxMoisture}
              onChange={(e) =>
                setFormData({ ...formData, maxMoisture: e.target.value })
              }
            />
            <input
              type="number"
              placeholder="Min Темп °C"
              style={styles.input}
              value={formData.minTemp}
              onChange={(e) =>
                setFormData({ ...formData, minTemp: e.target.value })
              }
            />
            <input
              type="number"
              placeholder="Max Темп °C"
              style={styles.input}
              value={formData.maxTemp}
              onChange={(e) =>
                setFormData({ ...formData, maxTemp: e.target.value })
              }
            />
          </div>
          <input
            type="number"
            placeholder="Мінімальне світло (Lux)"
            style={styles.input}
            value={formData.minLight}
            onChange={(e) =>
              setFormData({ ...formData, minLight: e.target.value })
            }
          />

          <button type="submit" style={styles.submitBtn}>
            Зберегти в базу
          </button>
        </form>
      </div>
    </div>
  );
}

const styles = {
  overlay: {
    position: "fixed",
    inset: 0,
    background: "rgba(0,0,0,0.5)",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    zIndex: 100,
  },
  modal: {
    background: "#fff",
    padding: 20,
    borderRadius: 12,
    width: "100%",
    maxWidth: 400,
  },
  modalHeader: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  closeBtn: {
    border: "none",
    background: "none",
    cursor: "pointer",
  },
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 10,
  },
  row: {
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: 8,
  },
  input: {
    padding: "8px 10px",
    borderRadius: 6,
    border: "1px solid #d1d5db",
    fontSize: 14,
  },
  submitBtn: {
    background: "#166534",
    color: "#fff",
    border: "none",
    padding: "10px",
    borderRadius: 8,
    cursor: "pointer",
    fontWeight: 600,
    marginTop: 6,
  },
};
