import { useState, useEffect, useRef, useCallback } from "react";
import { fetchPlants, createPlant, updatePlant, deletePlant, waterPlant } from "./api/plantsApi";
import { fetchActiveAlerts, resolveAlert } from "./api/alertsApi";
import { fetchTelemetryHistory } from "./api/telemetryApi";
import { getStoredUser } from "./api/client";
import { startSignalR, stopSignalR } from "./services/signalrService";

import { Header } from "./components/layout/Header";
import { PlantCard } from "./components/plants/PlantCard";
import { MetricsGrid } from "./components/telemetry/MetricsGrid";
import { HistoryTable } from "./components/telemetry/HistoryTable";
import { TelemetryChart } from "./components/telemetry/TelemetryChart";
import { AlertsList } from "./components/alerts/AlertsList";
import { AddPlantModal } from "./components/plants/AddPlantModal";
import { EditPlantModal } from "./components/plants/EditPlantModal";
import { TelegramBotModal } from "./components/telegram/TelegramBotModal";
import { AuthModal } from "./components/auth/AuthModal";
import { IoTSimulatorModal } from "./components/simulator/IoTSimulatorModal";
import { OfflineBanner } from "./components/layout/OfflineBanner";

export default function App() {
  const [plants, setPlants] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [selectedPlantId, setSelectedPlantId] = useState(null);
  const [history, setHistory] = useState(() => {
    const cached = localStorage.getItem("cached_history");
    return cached ? JSON.parse(cached) : [];
  });
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingPlant, setEditingPlant] = useState(null);
  const [isTelegramModalOpen, setIsTelegramModalOpen] = useState(false);
  const [isAuthModalOpen, setIsAuthModalOpen] = useState(false);
  const [isSimulatorModalOpen, setIsSimulatorModalOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [connectionStatus, setConnectionStatus] = useState("disconnected");
  const [currentUser, setCurrentUser] = useState(getStoredUser());

  const selectedPlantIdRef = useRef(selectedPlantId);
  useEffect(() => {
    selectedPlantIdRef.current = selectedPlantId;
  }, [selectedPlantId]);

  const loadBaseData = useCallback(async () => {
    try {
      const [plantsData, alertsData] = await Promise.all([
        fetchPlants(),
        fetchActiveAlerts(),
      ]);
      setPlants(plantsData);
      localStorage.setItem("cached_plants", JSON.stringify(plantsData));
      setAlerts(alertsData);

      if (plantsData.length > 0 && !selectedPlantIdRef.current) {
        setSelectedPlantId(plantsData[0].id);
      }
    } catch (err) {
      console.error("Помилка синхронізації з бекендом:", err);
    } finally {
      setLoading(false);
    }
  }, []);

  const [timeframe, setTimeframe] = useState("30");
  const [timeframeParams, setTimeframeParams] = useState({ limit: 30 });

  const loadTelemetry = useCallback(async (plantId, params = null) => {
    if (!plantId) return;
    try {
      const activeParams = params || timeframeParams;
      const data = await fetchTelemetryHistory(plantId, activeParams);
      setHistory(data);
      localStorage.setItem("cached_history", JSON.stringify(data));
    } catch (err) {
      console.error("Помилка завантаження телеметрії:", err);
    }
  }, [timeframeParams]);

  const handleTimeframeChange = (tfId, params) => {
    setTimeframe(tfId);
    setTimeframeParams(params);
    if (selectedPlantId) {
      loadTelemetry(selectedPlantId, params);
    }
  };

  // Initialize Real-time SignalR WebSocket Stream
  const initRealtime = useCallback(() => {
    startSignalR({
      onTelemetry: (telemetry) => {
        if (telemetry.plantId === selectedPlantIdRef.current) {
          setHistory((prev) => {
            const updated = [telemetry, ...prev.filter((p) => p.id !== telemetry.id)].slice(0, 50);
            localStorage.setItem("cached_history", JSON.stringify(updated));
            return updated;
          });
        }
      },
      onAlert: (newAlert) => {
        setAlerts((prev) => [newAlert, ...prev.filter((a) => a.id !== newAlert.id)]);
      },
      onWatering: (wateringEvent) => {
        if (wateringEvent.plantId === selectedPlantIdRef.current) {
          setHistory((prev) => {
            if (!prev.length) return prev;
            const updated = [{ ...prev[0], moisture: 85.0 }, ...prev.slice(1)];
            localStorage.setItem("cached_history", JSON.stringify(updated));
            return updated;
          });
        }
        // Also refresh alerts because watering auto-resolves LowMoisture
        fetchActiveAlerts().then(setAlerts).catch(() => {});
      },
      onStatusChange: (status) => {
        setConnectionStatus(status);
      },
    });
  }, []);

  useEffect(() => {
    loadBaseData();
    initRealtime();

    // Fallback polling for robust multi-client sync
    const interval = setInterval(() => {
      loadBaseData();
      if (selectedPlantIdRef.current) {
        loadTelemetry(selectedPlantIdRef.current);
      }
    }, 5000);

    return () => {
      clearInterval(interval);
      stopSignalR();
    };
  }, [loadBaseData, loadTelemetry, initRealtime]);

  useEffect(() => {
    if (selectedPlantId) {
      loadTelemetry(selectedPlantId);
    }
  }, [selectedPlantId, loadTelemetry]);

  const handleResolve = async (id) => {
    try {
      await resolveAlert(id);
      setAlerts((prev) => prev.filter((a) => a.id !== id));
    } catch (err) {
      alert(`Не вдалося закрити інцидент: ${err.message}`);
    }
  };

  const handleCreate = async (payload) => {
    try {
      const created = await createPlant(payload);
      setIsModalOpen(false);
      await loadBaseData();
      if (created?.id) {
        setSelectedPlantId(created.id);
      }
    } catch (err) {
      alert(`Помилка створення рослини: ${err.message}`);
    }
  };

  const handleUpdatePlant = async (plantId, payload) => {
    try {
      await updatePlant(plantId, payload);
      setEditingPlant(null);
      await loadBaseData();
    } catch (err) {
      alert(`Помилка оновлення рослини: ${err.message}`);
    }
  };

  const handleDeletePlant = async (plant) => {
    if (!window.confirm(`Ви дійсно бажаєте видалити вазон «${plant.name}»? Уся історія телеметрії та сповіщень буде видалена.`)) {
      return;
    }
    try {
      await deletePlant(plant.id);
      setPlants((prev) => prev.filter((p) => p.id !== plant.id));
      if (selectedPlantId === plant.id) {
        const remaining = plants.filter((p) => p.id !== plant.id);
        setSelectedPlantId(remaining.length > 0 ? remaining[0].id : null);
      }
    } catch (err) {
      alert(`Помилка видалення рослини: ${err.message}`);
    }
  };

  const handleUserAuthenticated = (user) => {
    setCurrentUser(user);
    loadBaseData();
    initRealtime();
  };

  const handleUserLoggedOut = () => {
    setCurrentUser(null);
    setPlants([]);
    setAlerts([]);
    setHistory([]);
    setSelectedPlantId(null);
  };

  const selectedPlant = plants.find((p) => p.id === selectedPlantId);
  const latestMetric = history[0];

  const handleWater = async (plantId) => {
    try {
      setHistory((prev) => {
        if (!prev.length) return prev;
        return [{ ...prev[0], moisture: 85.0 }, ...prev.slice(1)];
      });
      await waterPlant(plantId);
      // Refresh active alerts as backend resolves LowMoisture alert on water
      const updatedAlerts = await fetchActiveAlerts();
      setAlerts(updatedAlerts);
    } catch (err) {
      console.warn("Помилка поливу:", err);
      alert(`Помилка поливу: ${err.message}`);
    }
  };

  const handleWaterAndResolve = async (plantId, alertId) => {
    try {
      await handleWater(plantId);
      await handleResolve(alertId);
    } catch (err) {
      alert(`Помилка: ${err.message}`);
    }
  };

  return (
    <div style={{ maxWidth: 960, margin: "0 auto", padding: 16 }}>
      <OfflineBanner />

      <Header
        onRefresh={() => {
          loadBaseData();
          if (selectedPlantId) loadTelemetry(selectedPlantId);
        }}
        onOpenCreateModal={() => setIsModalOpen(true)}
        connectionStatus={connectionStatus}
        onReconnect={initRealtime}
        isPollingActive={true}
        currentUser={currentUser}
        onOpenTelegramModal={() => setIsTelegramModalOpen(true)}
        onOpenAuthModal={() => setIsAuthModalOpen(true)}
        onOpenSimulatorModal={() => setIsSimulatorModalOpen(true)}
      />

      <AlertsList
        alerts={alerts}
        onResolve={handleResolve}
        onWaterAndResolve={handleWaterAndResolve}
        selectedPlantId={selectedPlantId}
      />

      <h2 style={{ fontSize: 18, color: "#374151", marginBottom: 12 }}>
        Ваші рослини
      </h2>
      {plants.length === 0 && !loading && (
        <p style={{ color: "#6b7280", marginBottom: 20 }}>
          У базі ще немає доданих рослин. Натисніть «Додати рослину», щоб створити першу.
        </p>
      )}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))",
          gap: 12,
          marginBottom: 24,
        }}
      >
        {plants.map((plant) => (
          <PlantCard
            key={plant.id}
            plant={plant}
            isSelected={selectedPlantId === plant.id}
            onSelect={setSelectedPlantId}
            onEdit={setEditingPlant}
            onDelete={handleDeletePlant}
          />
        ))}
      </div>

      {selectedPlant && (
        <section
          style={{
            background: "#fff",
            border: "1px solid #e5e7eb",
            borderRadius: 12,
            padding: 18,
            boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
          }}
        >
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: 10,
              marginBottom: 12,
            }}
          >
            <div>
              <h3 style={{ margin: 0, color: "#1f2937", fontSize: 18 }}>
                Поточний стан: <b>{selectedPlant.name}</b>
                {selectedPlant.species && (
                  <span style={{ fontSize: 14, color: "#6b7280", fontWeight: 400, marginLeft: 8 }}>
                    ({selectedPlant.species})
                  </span>
                )}
              </h3>
            </div>

            <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
              <button
                onClick={() => setEditingPlant(selectedPlant)}
                style={{
                  padding: "8px 14px",
                  backgroundColor: "#f3f4f6",
                  color: "#374151",
                  border: "1px solid #d1d5db",
                  borderRadius: 8,
                  cursor: "pointer",
                  fontWeight: 600,
                  fontSize: 13,
                }}
              >
                ✏️ Налаштувати
              </button>

              <button
                onClick={() => handleWater(selectedPlantId)}
                style={{
                  padding: "8px 16px",
                  backgroundColor: "#0284c7",
                  color: "#fff",
                  border: "none",
                  borderRadius: 8,
                  cursor: "pointer",
                  fontWeight: 600,
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 6,
                  boxShadow: "0 1px 2px rgba(2, 132, 199, 0.3)",
                  fontSize: 13,
                }}
              >
                💧 Полити рослину
              </button>
            </div>
          </div>

          {/* IoT Device Key snippet banner */}
          {selectedPlant.apiKey && (
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: 8,
                padding: "8px 12px",
                marginBottom: 16,
                backgroundColor: "#f0fdf4",
                border: "1px solid #bbf7d0",
                borderRadius: 8,
                fontSize: 12,
                color: "#166534",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
                <span>🔑 <b>X-Device-Key IoT сенсора:</b></span>
                <code
                  style={{
                    backgroundColor: "#ffffff",
                    padding: "2px 8px",
                    borderRadius: 4,
                    border: "1px solid #86efac",
                    fontFamily: "monospace",
                    fontSize: 12,
                  }}
                >
                  {selectedPlant.apiKey}
                </code>
              </div>
              <button
                onClick={() => {
                  navigator.clipboard.writeText(selectedPlant.apiKey);
                  alert("✅ Ключ пристрою X-Device-Key скопійовано до буфера обміну!");
                }}
                style={{
                  background: "#166534",
                  color: "#fff",
                  border: "none",
                  borderRadius: 6,
                  padding: "4px 10px",
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: "pointer",
                }}
              >
                Скопіювати ключ
              </button>
            </div>
          )}

          <MetricsGrid latestTelemetry={latestMetric} plant={selectedPlant} />
          <TelemetryChart
            history={history}
            onTimeframeChange={handleTimeframeChange}
            currentTimeframe={timeframe}
          />
          <HistoryTable history={history} plant={selectedPlant} />
        </section>
      )}

      <AddPlantModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onCreate={handleCreate}
      />

      <EditPlantModal
        isOpen={!!editingPlant}
        plant={editingPlant}
        onClose={() => setEditingPlant(null)}
        onUpdate={handleUpdatePlant}
      />

      <TelegramBotModal
        isOpen={isTelegramModalOpen}
        onClose={() => setIsTelegramModalOpen(false)}
        currentUser={currentUser}
        onUserAuthenticated={handleUserAuthenticated}
      />

      <AuthModal
        isOpen={isAuthModalOpen}
        onClose={() => setIsAuthModalOpen(false)}
        currentUser={currentUser}
        onUserAuthenticated={handleUserAuthenticated}
        onUserLoggedOut={handleUserLoggedOut}
      />

      <IoTSimulatorModal
        isOpen={isSimulatorModalOpen}
        onClose={() => setIsSimulatorModalOpen(false)}
        plants={plants}
        selectedPlantId={selectedPlantId}
        onTelemetrySent={() => {
          loadBaseData();
          if (selectedPlantId) {
            loadTelemetry(selectedPlantId);
          }
        }}
      />
    </div>
  );
}
