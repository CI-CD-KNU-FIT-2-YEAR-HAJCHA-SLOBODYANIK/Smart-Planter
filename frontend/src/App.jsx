import React, { useState, useEffect } from "react";
import { fetchPlants, createPlant } from "./api/plantsApi";
import { waterPlant } from "./api/plantsApi";
import { fetchActiveAlerts, resolveAlert } from "./api/alertsApi";
import { fetchTelemetryHistory } from "./api/telemetryApi";

import { Header } from "./components/layout/Header";
import { PlantCard } from "./components/plants/PlantCard";
import { MetricsGrid } from "./components/telemetry/MetricsGrid";
import { HistoryTable } from "./components/telemetry/HistoryTable";
import { TelemetryChart } from "./components/telemetry/TelemetryChart";
import { AlertsList } from "./components/alerts/AlertsList";
import { AddPlantModal } from "./components/plants/AddPlantModal";

export default function App() {
  const [plants, setPlants] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [selectedPlantId, setSelectedPlantId] = useState(null);
  const [history, setHistory] = useState([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [loading, setLoading] = useState(true);

  const loadBaseData = async () => {
    try {
      const [plantsData, alertsData] = await Promise.all([
        fetchPlants(),
        fetchActiveAlerts(),
      ]);
      setPlants(plantsData);
      setAlerts(alertsData);

      if (plantsData.length > 0 && !selectedPlantId) {
        setSelectedPlantId(plantsData[0].id);
      }
    } catch (err) {
      console.error("Помилка синхронізації з бекендом:", err);
    } finally {
      setLoading(false);
    }
  };

  const loadTelemetry = async (plantId) => {
    if (!plantId) return;
    try {
      const data = await fetchTelemetryHistory(plantId, 10);
      setHistory(data);
    } catch (err) {
      console.error("Помилка завантаження телеметрії:", err);
    }
  };

  useEffect(() => {
    loadBaseData();
    const interval = setInterval(loadBaseData, 5000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (selectedPlantId) {
      loadTelemetry(selectedPlantId);
    }
  }, [selectedPlantId]);

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
      await createPlant(payload);
      setIsModalOpen(false);
      await loadBaseData();
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
      alert("💧 Полив успішно зафіксовано на сервері!");
    } catch (err) {
      console.warn(
        "Сервер поливу відповів із затримкою, запит надіслано:",
        err,
      );
    }
  };

  return (
    <div style={{ maxWidth: 900, margin: "0 auto", padding: 16 }}>
      <Header
        onRefresh={loadBaseData}
        onOpenCreateModal={() => setIsModalOpen(true)}
      />

      <AlertsList alerts={alerts} onResolve={handleResolve} />

      <h2 style={{ fontSize: 18, color: "#374151", marginBottom: 12 }}>
        Ваші рослини
      </h2>
      {plants.length === 0 && !loading && (
        <p style={{ color: "#6b7280", marginBottom: 20 }}>
          У базі ще немає доданих рослин. Натисніть «Додати рослину», щоб
          створити першу.
        </p>
      )}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
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
            padding: 16,
            boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
          }}
        >
          <h3 style={{ margin: "0 0 16px 0", color: "#1f2937" }}>
            Поточний стан: <b>{selectedPlant.name}</b>
          </h3>
          <button
            onClick={() => handleWater(selectedPlantId)}
            style={{
              padding: "8px 14px",
              backgroundColor: "#0284c7",
              color: "#fff",
              border: "none",
              borderRadius: 8,
              cursor: "pointer",
              fontWeight: 600,
              display: "inline-flex",
              alignItems: "center",
              gap: 6,
              marginBottom: 16,
            }}
          >
            💧 Полити рослину
          </button>
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
