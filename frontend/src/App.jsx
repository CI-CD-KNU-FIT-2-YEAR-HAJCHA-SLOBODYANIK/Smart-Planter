import React from "react";
import { Header } from "./components/layout/Header";
import { PlantCard } from "./components/plants/PlantCard";
import { MetricsGrid } from "./components/telemetry/MetricsGrid";
import { HistoryTable } from "./components/telemetry/HistoryTable";

export default function App() {
  const mockPlant = {
    id: 1,
    name: "Фикус Бенджамина",
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

  return (
    <div style={{ maxWidth: 900, margin: "0 auto", padding: 16 }}>
      <Header onRefresh={() => {}} onOpenCreateModal={() => {}} />
      <PlantCard plant={mockPlant} isSelected={true} onSelect={() => {}} />

      <div style={{ marginTop: 20 }}>
        <MetricsGrid latestTelemetry={mockTelemetry} plant={mockPlant} />
        <HistoryTable history={mockHistory} />
      </div>
    </div>
  );
}
