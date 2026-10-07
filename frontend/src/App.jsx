import React, { useState } from "react";
import { Header } from "./components/layout/Header";
import { PlantCard } from "./components/plants/PlantCard";
import { MetricsGrid } from "./components/telemetry/MetricsGrid";
import { HistoryTable } from "./components/telemetry/HistoryTable";
import { AlertsList } from "./components/alerts/AlertsList";
import { AddPlantModal } from "./components/plants/AddPlantModal";

export default function App() {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [mockAlerts, setMockAlerts] = useState([
    {
      id: 1,
      plantId: 1,
      plantName: "Фікус Бенджаміна",
      message: "Низька вологість: 25.0% (мінімум: 30.0%)",
      createdAt: new Date().toISOString(),
    },
  ]);

  const mockPlant = {
    id: 1,
    name: "Фікус Бенджаміна",
    species: "Ficus",
    minMoisture: 30,
    maxMoisture: 60,
    minTemp: 18,
    maxTemp: 26,
    minLight: 400,
  };

  const mockTelemetry = {
    moisture: 45.2,
    temperature: 22.4,
    light: 520,
  };

  const mockHistory = [
    {
      id: 1,
      timestamp: new Date().toISOString(),
      moisture: 45.2,
      temperature: 22.4,
      light: 520,
    },
    {
      id: 2,
      timestamp: new Date(Date.now() - 60000).toISOString(),
      moisture: 44.8,
      temperature: 22.1,
      light: 510,
    },
  ];

  const handleResolve = (id) => {
    alert(`Інцидент #${id} закрито!`);
    setMockAlerts((prev) => prev.filter((a) => a.id !== id));
  };

  const handleCreate = (data) => {
    alert(`Рослину додано: ${data.name} (вид: ${data.species})`);
    console.log("Дані нової рослини:", data);
    setIsModalOpen(false);
  };

  return (
    <div style={{ maxWidth: 900, margin: "0 auto", padding: 16 }}>
      <Header
        onRefresh={() => alert("Оновлення даних...")}
        onOpenCreateModal={() => setIsModalOpen(true)}
      />

      <AlertsList alerts={mockAlerts} onResolve={handleResolve} />

      <div style={{ marginBottom: 20 }}>
        <PlantCard plant={mockPlant} isSelected={true} onSelect={() => {}} />
      </div>

      <section
        style={{
          background: "#fff",
          border: "1px solid #e5e7eb",
          borderRadius: 12,
          padding: 16,
        }}
      >
        <MetricsGrid latestTelemetry={mockTelemetry} plant={mockPlant} />
        <HistoryTable history={mockHistory} />
      </section>

      <AddPlantModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onCreate={handleCreate}
      />
    </div>
  );
}
