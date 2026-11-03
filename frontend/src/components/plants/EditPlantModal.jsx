import { useState } from "react";
import { X, Save, Sliders } from "lucide-react";

function EditPlantForm({ plant, onClose, onUpdate }) {
  const [formData, setFormData] = useState({
    name: plant.name || "",
    species: plant.species || "",
    minMoisture: plant.minMoisture ?? 30,
    maxMoisture: plant.maxMoisture ?? 70,
    minTemp: plant.minTemp ?? 18,
    maxTemp: plant.maxTemp ?? 28,
    minLight: plant.minLight ?? 300,
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    onUpdate(plant.id, {
      name: formData.name,
      species: formData.species,
      minMoisture: Number(formData.minMoisture),
      maxMoisture: Number(formData.maxMoisture),
      minTemp: Number(formData.minTemp),
      maxTemp: Number(formData.maxTemp),
      minLight: Number(formData.minLight),
    });
  };

  const PRESETS = {
    monstera: {
      minMoisture: 40,
      maxMoisture: 70,
      minTemp: 18,
      maxTemp: 28,
      minLight: 400,
    },
    succulent: {
      minMoisture: 15,
      maxMoisture: 40,
      minTemp: 15,
      maxTemp: 30,
      minLight: 800,
    },
    ficus: {
      minMoisture: 30,
      maxMoisture: 60,
      minTemp: 18,
      maxTemp: 26,
      minLight: 500,
    },
  };

  return (
    <div style={styles.overlay}>
      <div style={styles.modal}>
        <div style={styles.modalHeader}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <Sliders size={20} color="#166534" />
            <h3 style={{ margin: 0, fontSize: 18, color: "#111827" }}>
              Налаштування: {plant.name}
            </h3>
          </div>
          <button onClick={onClose} style={styles.closeBtn}>
            <X size={20} />
          </button>
        </div>

        <div style={{ marginBottom: 14 }}>
          <label style={styles.label}>Швидкі пороги за видом рослини:</label>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <button
              type="button"
              onClick={() => setFormData((prev) => ({ ...prev, ...PRESETS.monstera }))}
              style={styles.presetBtn}
            >
              🌿 Монстера
            </button>
            <button
              type="button"
              onClick={() => setFormData((prev) => ({ ...prev, ...PRESETS.succulent }))}
              style={styles.presetBtn}
            >
              🌵 Сукулент
            </button>
            <button
              type="button"
              onClick={() => setFormData((prev) => ({ ...prev, ...PRESETS.ficus }))}
              style={styles.presetBtn}
            >
              🪴 Фікус
            </button>
          </div>
        </div>

        <form onSubmit={handleSubmit} style={styles.form}>
          <div>
            <label style={styles.label}>Назва вазона:</label>
            <input
              required
              style={styles.input}
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </div>

          <div>
            <label style={styles.label}>Біологічний вид:</label>
            <input
              style={styles.input}
              value={formData.species}
              onChange={(e) => setFormData({ ...formData, species: e.target.value })}
            />
          </div>

          <div style={styles.row}>
            <div style={{ flex: 1 }}>
              <label style={styles.label}>Мін. вологість (%)</label>
              <input
                type="number"
                step="0.5"
                required
                style={styles.input}
                value={formData.minMoisture}
                onChange={(e) => setFormData({ ...formData, minMoisture: e.target.value })}
              />
            </div>
            <div style={{ flex: 1 }}>
              <label style={styles.label}>Макс. вологість (%)</label>
              <input
                type="number"
                step="0.5"
                required
                style={styles.input}
                value={formData.maxMoisture}
                onChange={(e) => setFormData({ ...formData, maxMoisture: e.target.value })}
              />
            </div>
          </div>

          <div style={styles.row}>
            <div style={{ flex: 1 }}>
              <label style={styles.label}>Мін. темп (°C)</label>
              <input
                type="number"
                step="0.5"
                required
                style={styles.input}
                value={formData.minTemp}
                onChange={(e) => setFormData({ ...formData, minTemp: e.target.value })}
              />
            </div>
            <div style={{ flex: 1 }}>
              <label style={styles.label}>Макс. темп (°C)</label>
              <input
                type="number"
                step="0.5"
                required
                style={styles.input}
                value={formData.maxTemp}
                onChange={(e) => setFormData({ ...formData, maxTemp: e.target.value })}
              />
            </div>
          </div>

          <div>
            <label style={styles.label}>Мін. освітленість (Lux):</label>
            <input
              type="number"
              required
              style={styles.input}
              value={formData.minLight}
              onChange={(e) => setFormData({ ...formData, minLight: e.target.value })}
            />
          </div>

          <div style={styles.actions}>
            <button type="button" onClick={onClose} style={styles.cancelBtn}>
              Скасувати
            </button>
            <button type="submit" style={styles.submitBtn}>
              <Save size={16} /> Зберегти зміни
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function EditPlantModal({ isOpen, plant, onClose, onUpdate }) {
  if (!isOpen || !plant) return null;
  return (
    <EditPlantForm
      key={plant.id}
      plant={plant}
      onClose={onClose}
      onUpdate={onUpdate}
    />
  );
}

const styles = {
  overlay: {
    position: "fixed",
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: "rgba(0, 0, 0, 0.4)",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    zIndex: 1000,
    padding: 16,
  },
  modal: {
    background: "#fff",
    borderRadius: 12,
    padding: 20,
    width: "100%",
    maxWidth: 440,
    boxShadow: "0 10px 25px -5px rgba(0, 0, 0, 0.1)",
  },
  modalHeader: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 16,
    paddingBottom: 12,
    borderBottom: "1px solid #e5e7eb",
  },
  closeBtn: {
    background: "none",
    border: "none",
    cursor: "pointer",
    color: "#6b7280",
    display: "flex",
    alignItems: "center",
  },
  label: {
    fontSize: 12,
    color: "#4b5563",
    display: "block",
    marginBottom: 4,
    fontWeight: 500,
  },
  presetBtn: {
    padding: "4px 8px",
    fontSize: 12,
    borderRadius: 6,
    border: "1px solid #d1d5db",
    background: "#f9fafb",
    cursor: "pointer",
  },
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 12,
  },
  input: {
    width: "100%",
    padding: "8px 12px",
    borderRadius: 8,
    border: "1px solid #d1d5db",
    fontSize: 14,
    outline: "none",
  },
  row: {
    display: "flex",
    gap: 10,
  },
  actions: {
    display: "flex",
    justifyContent: "flex-end",
    gap: 8,
    marginTop: 8,
  },
  cancelBtn: {
    padding: "8px 14px",
    borderRadius: 8,
    border: "1px solid #d1d5db",
    background: "#fff",
    color: "#374151",
    cursor: "pointer",
    fontSize: 13,
    fontWeight: 500,
  },
  submitBtn: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    padding: "8px 16px",
    borderRadius: 8,
    border: "none",
    background: "#166534",
    color: "#fff",
    cursor: "pointer",
    fontSize: 13,
    fontWeight: 600,
  },
};
