import { useState } from "react";
import { X, Cpu, Send, CheckCircle2, AlertCircle, Sparkles, RefreshCw } from "lucide-react";
import { postTelemetry } from "../../api/telemetryApi";

function IoTSimulatorContent({ onClose, plants = [], selectedPlantId, onTelemetrySent }) {
  const [targetPlantId, setTargetPlantId] = useState(selectedPlantId || (plants[0]?.id ?? ""));
  const [customKey, setCustomKey] = useState("");
  const [moisture, setMoisture] = useState(45);
  const [temperature, setTemperature] = useState(23);
  const [light, setLight] = useState(450);
  const [sending, setSending] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  const currentPlant = plants.find((p) => p.id === Number(targetPlantId));
  const activeDeviceKey = currentPlant?.apiKey || customKey;

  const SCENARIOS = [
    {
      name: "🏜️ Посуха",
      desc: "Викликає алерт LowMoisture",
      moisture: 18,
      temperature: 28,
      light: 650,
    },
    {
      name: "🌊 Перелив",
      desc: "Викликає алерт HighMoisture",
      moisture: 92,
      temperature: 21,
      light: 400,
    },
    {
      name: "❄️ Заморозки",
      desc: "Викликає алерт LowTemperature",
      moisture: 45,
      temperature: 12,
      light: 250,
    },
    {
      name: "🔥 Спека",
      desc: "Викликає алерт HighTemperature",
      moisture: 32,
      temperature: 36,
      light: 950,
    },
    {
      name: "⛅ Темрява",
      desc: "Викликає алерт LowLight",
      moisture: 50,
      temperature: 20,
      light: 60,
    },
    {
      name: "🌿 Норма",
      desc: "Оптимальні показники",
      moisture: 55,
      temperature: 23,
      light: 500,
    },
  ];

  const applyScenario = (sc) => {
    setMoisture(sc.moisture);
    setTemperature(sc.temperature);
    setLight(sc.light);
    setError(null);
    setResult(null);
  };

  const handleSend = async (e) => {
    e.preventDefault();
    if (!activeDeviceKey) {
      setError("Вкажіть API-ключ пристрою (X-Device-Key)!");
      return;
    }

    setSending(true);
    setError(null);
    setResult(null);

    try {
      const response = await postTelemetry(activeDeviceKey, {
        moisture,
        temperature,
        light,
        timestamp: new Date().toISOString(),
      });

      setResult({
        message: "Пакет телеметрії успішно прийнято бекендом!",
        data: response,
      });

      if (onTelemetrySent) {
        onTelemetrySent(response);
      }
    } catch (err) {
      setError(`Помилка відправки: ${err.message}`);
    } finally {
      setSending(false);
    }
  };

  return (
    <div style={styles.overlay}>
      <div style={styles.modal}>
        <div style={styles.header}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <div style={styles.iconCircle}>
              <Cpu size={18} color="#7c3aed" />
            </div>
            <div>
              <h3 style={{ margin: 0, fontSize: 18, color: "#111827" }}>
                Емулятор IoT датчиків
              </h3>
              <p style={{ margin: 0, fontSize: 12, color: "#6b7280" }}>
                Генерація пакетів вимірювань (POST /api/v1/telemetry)
              </p>
            </div>
          </div>
          <button onClick={onClose} style={styles.closeBtn}>
            <X size={20} />
          </button>
        </div>

        {/* Plant selection */}
        <div style={{ marginBottom: 14 }}>
          <label style={styles.label}>Цільовий вазон:</label>
          {plants.length > 0 ? (
            <select
              value={targetPlantId}
              onChange={(e) => setTargetPlantId(e.target.value)}
              style={styles.select}
            >
              {plants.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} ({p.species || "без сорту"}) — Key: {p.apiKey?.substring(0, 8)}...
                </option>
              ))}
            </select>
          ) : (
            <div>
              <input
                placeholder="Введіть X-Device-Key вручну"
                value={customKey}
                onChange={(e) => setCustomKey(e.target.value)}
                style={styles.input}
              />
            </div>
          )}
        </div>

        {/* Quick Scenario Buttons */}
        <div style={{ marginBottom: 16 }}>
          <label style={styles.label}>
            <Sparkles size={13} style={{ verticalAlign: "middle", marginRight: 4 }} />
            Швидкі тестові сценарії для перевірки тривог:
          </label>
          <div style={styles.scenarioGrid}>
            {SCENARIOS.map((sc) => (
              <button
                key={sc.name}
                type="button"
                onClick={() => applyScenario(sc)}
                style={styles.scenarioBtn}
                title={sc.desc}
              >
                <b>{sc.name}</b>
                <span style={styles.scenarioDesc}>{sc.desc}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Sliders Form */}
        <form onSubmit={handleSend} style={styles.form}>
          <div>
            <div style={styles.sliderHeader}>
              <span style={{ fontSize: 12, fontWeight: 600, color: "#0284c7" }}>
                💧 Вологість ґрунту: <b>{moisture}%</b>
              </span>
              {currentPlant && (
                <span style={{ fontSize: 11, color: "#6b7280" }}>
                  (Норма: {currentPlant.minMoisture}–{currentPlant.maxMoisture}%)
                </span>
              )}
            </div>
            <input
              type="range"
              min="5"
              max="100"
              step="0.5"
              value={moisture}
              onChange={(e) => setMoisture(Number(e.target.value))}
              style={{ width: "100%", accentColor: "#0284c7" }}
            />
          </div>

          <div>
            <div style={styles.sliderHeader}>
              <span style={{ fontSize: 12, fontWeight: 600, color: "#dc2626" }}>
                🌡 Температура: <b>{temperature}°C</b>
              </span>
              {currentPlant && (
                <span style={{ fontSize: 11, color: "#6b7280" }}>
                  (Норма: {currentPlant.minTemp}–{currentPlant.maxTemp}°C)
                </span>
              )}
            </div>
            <input
              type="range"
              min="0"
              max="45"
              step="0.5"
              value={temperature}
              onChange={(e) => setTemperature(Number(e.target.value))}
              style={{ width: "100%", accentColor: "#dc2626" }}
            />
          </div>

          <div>
            <div style={styles.sliderHeader}>
              <span style={{ fontSize: 12, fontWeight: 600, color: "#d97706" }}>
                ☀️ Освітленість: <b>{light} Lux</b>
              </span>
              {currentPlant && (
                <span style={{ fontSize: 11, color: "#6b7280" }}>
                  (Мін: {currentPlant.minLight} Lux)
                </span>
              )}
            </div>
            <input
              type="range"
              min="10"
              max="1200"
              step="10"
              value={light}
              onChange={(e) => setLight(Number(e.target.value))}
              style={{ width: "100%", accentColor: "#d97706" }}
            />
          </div>

          {error && (
            <div style={styles.errorAlert}>
              <AlertCircle size={16} />
              <span>{error}</span>
            </div>
          )}

          {result && (
            <div style={styles.successAlert}>
              <CheckCircle2 size={16} />
              <div>
                <b>{result.message}</b>
                <div style={{ fontSize: 11, marginTop: 2 }}>
                  Замір збережено ID #{result.data?.id}. Дані передано у SignalR та Telegram!
                </div>
              </div>
            </div>
          )}

          <button
            type="submit"
            disabled={sending}
            style={styles.btnSubmit}
          >
            {sending ? (
              <RefreshCw size={16} style={{ animation: "spin 1s linear infinite" }} />
            ) : (
              <Send size={16} />
            )}
            Відправити на бекенд (POST /telemetry)
          </button>
        </form>
      </div>
    </div>
  );
}

export function IoTSimulatorModal({ isOpen, onClose, plants = [], selectedPlantId, onTelemetrySent }) {
  if (!isOpen) return null;
  return (
    <IoTSimulatorContent
      key={selectedPlantId || "default"}
      onClose={onClose}
      plants={plants}
      selectedPlantId={selectedPlantId}
      onTelemetrySent={onTelemetrySent}
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
    maxWidth: 480,
    boxShadow: "0 10px 25px -5px rgba(0, 0, 0, 0.1)",
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 16,
    paddingBottom: 10,
    borderBottom: "1px solid #e5e7eb",
  },
  iconCircle: {
    width: 36,
    height: 36,
    borderRadius: "50%",
    backgroundColor: "#f5f3ff",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
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
    fontWeight: 600,
    color: "#374151",
    display: "block",
    marginBottom: 6,
  },
  select: {
    width: "100%",
    padding: "8px 12px",
    borderRadius: 8,
    border: "1px solid #d1d5db",
    fontSize: 13,
    backgroundColor: "#fff",
    outline: "none",
  },
  input: {
    width: "100%",
    padding: "8px 12px",
    borderRadius: 8,
    border: "1px solid #d1d5db",
    fontSize: 13,
    outline: "none",
    boxSizing: "border-box",
  },
  scenarioGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(3, 1fr)",
    gap: 6,
  },
  scenarioBtn: {
    padding: "6px 8px",
    borderRadius: 6,
    border: "1px solid #e2e8f0",
    backgroundColor: "#f8fafc",
    cursor: "pointer",
    textAlign: "left",
    fontSize: 11,
    display: "flex",
    flexDirection: "column",
    gap: 2,
    transition: "all 0.15s ease",
  },
  scenarioDesc: {
    fontSize: 10,
    color: "#64748b",
  },
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 12,
  },
  sliderHeader: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 4,
  },
  btnSubmit: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    backgroundColor: "#7c3aed",
    color: "#fff",
    border: "none",
    borderRadius: 8,
    padding: "10px",
    fontSize: 14,
    fontWeight: 600,
    cursor: "pointer",
    marginTop: 6,
    boxShadow: "0 1px 3px rgba(124, 58, 237, 0.3)",
  },
  errorAlert: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    padding: "8px 12px",
    borderRadius: 6,
    backgroundColor: "#fee2e2",
    color: "#b91c1c",
    fontSize: 12,
  },
  successAlert: {
    display: "flex",
    alignItems: "flex-start",
    gap: 8,
    padding: "8px 12px",
    borderRadius: 6,
    backgroundColor: "#dcfce7",
    color: "#15803d",
    fontSize: 12,
  },
};
