import React, { useState, useEffect, useRef, useCallback } from "react";
import { fetchPlants, createPlant, waterPlant } from "./api/plantsApi";
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

  const loadTelemetry = useCallback(async (plantId) => {
    if (!plantId) return;
    try {
      const data = await fetchTelemetryHistory(plantId, { limit: 30 });
      setHistory(data);
      localStorage.setItem("cached_history", JSON.stringify(data));
    } catch (err) {
      console.error("Помилка завантаження телеметрії:", err);
    }
  }, []);

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
      />

      <AlertsList alerts={alerts} onResolve={handleResolve} />

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
              marginBottom: 16,
            }}
          >
            <h3 style={{ margin: 0, color: "#1f2937", fontSize: 18 }}>
              Поточний стан: <b>{selectedPlant.name}</b>
              {selectedPlant.species && (
                <span style={{ fontSize: 14, color: "#6b7280", fontWeight: 400, marginLeft: 8 }}>
                  ({selectedPlant.species})
                </span>
              )}
            </h3>

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
              }}
            >
              💧 Полити рослину
            </button>
          </div>

          <MetricsGrid latestTelemetry={latestMetric} plant={selectedPlant} />
          <TelemetryChart history={history} />
          <HistoryTable history={history} />
        </section>
      )}

      <AddPlantModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onCreate={handleCreate}
      />
    </div>
  );
}
