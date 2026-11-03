import { useState } from "react";
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

  const PRESETS = {
    monstera: {
      name: "Монстера",
      species: "Monstera Deliciosa",
      minMoisture: 40,
      maxMoisture: 70,
      minTemp: 18,
      maxTemp: 28,
      minLight: 400,
    },
    succulent: {
      name: "Ехеверія",
      species: "Сукулент",
      minMoisture: 15,
      maxMoisture: 40,
      minTemp: 15,
      maxTemp: 30,
      minLight: 800,
    },
    ficus: {
      name: "Фікус Бенджаміна",
      species: "Ficus",
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
          <h3 style={{ margin: 0 }}>Нова рослина</h3>
          <button onClick={onClose} style={styles.closeBtn}>
            <X size={20} />
          </button>
        </div>

        <div style={{ marginBottom: 14 }}>
          <label style={styles.label}>Швидкий шаблон:</label>
          <div style={{ display: "flex", gap: 6 }}>
            {Object.entries(PRESETS).map(([key, p]) => (
              <button
                key={key}
                type="button"
                onClick={() => setFormData({ ...p })}
                style={{
                  padding: "4px 8px",
                  fontSize: 12,
                  borderRadius: 6,
                  border: "1px solid #d1d5db",
                  background: "#f9fafb",
                  cursor: "pointer",
                }}
              >
                {p.name}
              </button>
            ))}
          </div>
        </div>

        <form onSubmit={handleSubmit} style={styles.form}>
          <div>
            <label style={styles.label}>Назва рослини</label>
            <input
              placeholder="напр. Монстера"
              required
              style={styles.input}
              value={formData.name}
              onChange={(e) =>
                setFormData({ ...formData, name: e.target.value })
              }
            />
          </div>

          <div>
            <label style={styles.label}>Вид / сорт</label>
            <input
              placeholder="напр. Deliciosa"
              style={styles.input}
              value={formData.species}
              onChange={(e) =>
                setFormData({ ...formData, species: e.target.value })
              }
            />
          </div>

          {/* Вологість з окремими підписами Мін/Макс */}
          <div>
            <label style={styles.label}>Вологість ґрунту (%)</label>
            <div style={styles.row}>
              <div>
                <span style={styles.subLabel}>Мінімум (%)</span>
                <input
                  type="number"
                  placeholder="30"
                  style={styles.input}
                  value={formData.minMoisture}
                  onChange={(e) =>
                    setFormData({ ...formData, minMoisture: e.target.value })
                  }
                />
              </div>
              <div>
                <span style={styles.subLabel}>Максимум (%)</span>
                <input
                  type="number"
                  placeholder="70"
                  style={styles.input}
                  value={formData.maxMoisture}
                  onChange={(e) =>
                    setFormData({ ...formData, maxMoisture: e.target.value })
                  }
                />
              </div>
            </div>
          </div>

          {/* Температура з окремими підписами Мін/Макс */}
          <div>
            <label style={styles.label}>Температура (°C)</label>
            <div style={styles.row}>
              <div>
                <span style={styles.subLabel}>Мінімум (°C)</span>
                <input
                  type="number"
                  placeholder="18"
                  style={styles.input}
                  value={formData.minTemp}
                  onChange={(e) =>
                    setFormData({ ...formData, minTemp: e.target.value })
                  }
                />
              </div>
              <div>
                <span style={styles.subLabel}>Максимум (°C)</span>
                <input
                  type="number"
                  placeholder="28"
                  style={styles.input}
                  value={formData.maxTemp}
                  onChange={(e) =>
                    setFormData({ ...formData, maxTemp: e.target.value })
                  }
                />
              </div>
            </div>
          </div>

          <div>
            <label style={styles.label}>Мінімальне освітлення (Lux)</label>
            <input
              type="number"
              placeholder="300"
              style={styles.input}
              value={formData.minLight}
              onChange={(e) =>
                setFormData({ ...formData, minLight: e.target.value })
              }
            />
          </div>

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
    padding: 16,
    boxSizing: "border-box",
  },
  modal: {
    background: "#fff",
    padding: 20,
    borderRadius: 12,
    width: "100%",
    maxWidth: 400,
    boxSizing: "border-box",
    maxHeight: "90vh",
    overflowY: "auto",
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
  label: {
    fontSize: 13,
    color: "#1f2937",
    fontWeight: 600,
    display: "block",
    marginBottom: 4,
  },
  subLabel: {
    fontSize: 11,
    color: "#6b7280",
    fontWeight: 500,
    display: "block",
    marginBottom: 2,
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
    width: "100%",
    boxSizing: "border-box",
  },
  input: {
    width: "100%",
    padding: "8px 10px",
    borderRadius: 6,
    border: "1px solid #d1d5db",
    fontSize: 14,
    boxSizing: "border-box",
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
