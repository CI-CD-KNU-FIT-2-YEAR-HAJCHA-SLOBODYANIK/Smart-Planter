import { useState } from "react";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  CartesianGrid,
} from "recharts";
import { SlidersHorizontal, Clock } from "lucide-react";

export function TelemetryChart({ history = [], onTimeframeChange, currentTimeframe = "30" }) {
  const [metricFilter, setMetricFilter] = useState("all"); // 'all' | 'moisture' | 'temperature' | 'light'

  const chartData = [...history].reverse().map((item) => ({
    time: new Date(item.timestamp).toLocaleTimeString([], {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }),
    fullDate: new Date(item.timestamp).toLocaleString(),
    moisture: item.moisture,
    temperature: item.temperature,
    light: item.light,
  }));

  const TIMEFRAMES = [
    { id: "10", label: "10 точок", params: { limit: 10 } },
    { id: "30", label: "30 точок", params: { limit: 30 } },
    { id: "50", label: "50 точок", params: { limit: 50 } },
    { id: "1h", label: "1 год", params: { hours: 1, limit: 100 } },
    { id: "6h", label: "6 год", params: { hours: 6, limit: 100 } },
    { id: "24h", label: "24 год", params: { hours: 24, limit: 100 } },
  ];

  return (
    <div style={{ marginTop: 24 }}>
      <div style={styles.header}>
        <div>
          <h4 style={styles.title}>Динаміка мікроклімату</h4>
          <span style={styles.subtitle}>
            {chartData.length} точок замірів у поточному періоді
          </span>
        </div>

        <div style={styles.controlsRow}>
          {/* Timeframe Presets */}
          {onTimeframeChange && (
            <div style={styles.btnGroup}>
              <span style={{ fontSize: 11, color: "#6b7280", display: "flex", alignItems: "center", gap: 3 }}>
                <Clock size={12} /> Період:
              </span>
              {TIMEFRAMES.map((tf) => (
                <button
                  key={tf.id}
                  onClick={() => onTimeframeChange(tf.id, tf.params)}
                  style={{
                    ...styles.filterBtn,
                    backgroundColor: currentTimeframe === tf.id ? "#166534" : "#f3f4f6",
                    color: currentTimeframe === tf.id ? "#ffffff" : "#4b5563",
                    fontWeight: currentTimeframe === tf.id ? 600 : 400,
                  }}
                >
                  {tf.label}
                </button>
              ))}
            </div>
          )}

          {/* Metric Selector */}
          <div style={styles.btnGroup}>
            <span style={{ fontSize: 11, color: "#6b7280", display: "flex", alignItems: "center", gap: 3 }}>
              <SlidersHorizontal size={12} /> Показники:
            </span>
            <button
              onClick={() => setMetricFilter("all")}
              style={{
                ...styles.filterBtn,
                backgroundColor: metricFilter === "all" ? "#1f2937" : "#f3f4f6",
                color: metricFilter === "all" ? "#ffffff" : "#4b5563",
              }}
            >
              Усі
            </button>
            <button
              onClick={() => setMetricFilter("moisture")}
              style={{
                ...styles.filterBtn,
                backgroundColor: metricFilter === "moisture" ? "#0284c7" : "#f3f4f6",
                color: metricFilter === "moisture" ? "#ffffff" : "#0284c7",
              }}
            >
              💧 Вологість
            </button>
            <button
              onClick={() => setMetricFilter("temperature")}
              style={{
                ...styles.filterBtn,
                backgroundColor: metricFilter === "temperature" ? "#dc2626" : "#f3f4f6",
                color: metricFilter === "temperature" ? "#ffffff" : "#dc2626",
              }}
            >
              🌡 Темп.
            </button>
            <button
              onClick={() => setMetricFilter("light")}
              style={{
                ...styles.filterBtn,
                backgroundColor: metricFilter === "light" ? "#d97706" : "#f3f4f6",
                color: metricFilter === "light" ? "#ffffff" : "#d97706",
              }}
            >
              ☀️ Світло
            </button>
          </div>
        </div>
      </div>

      {chartData.length === 0 ? (
        <div style={styles.emptyState}>
          Немає даних телеметрії для обраного періоду.
        </div>
      ) : (
        <div style={{ width: "100%", height: 280 }}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart
              data={chartData}
              margin={{ top: 10, right: metricFilter === "all" ? 25 : 10, left: -10, bottom: 5 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis dataKey="time" tick={{ fontSize: 11 }} />

              {/* Left Y-axis for Moisture and Temperature (0 - 100 range) */}
              {(metricFilter === "all" || metricFilter === "moisture" || metricFilter === "temperature") && (
                <YAxis
                  yAxisId="left"
                  tick={{ fontSize: 11 }}
                  domain={metricFilter === "temperature" ? ["auto", "auto"] : [0, 100]}
                  unit={metricFilter === "temperature" ? "°C" : "%"}
                />
              )}

              {/* Right Y-axis for Light (0 - 1000+ Lux range) */}
              {(metricFilter === "all" || metricFilter === "light") && (
                <YAxis
                  yAxisId={metricFilter === "all" ? "right" : "left"}
                  orientation={metricFilter === "all" ? "right" : "left"}
                  tick={{ fontSize: 11 }}
                  unit=" lx"
                  domain={[0, "auto"]}
                />
              )}

              <Tooltip
                contentStyle={{
                  borderRadius: 8,
                  fontSize: 12,
                  border: "1px solid #e5e7eb",
                  boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
                }}
                labelFormatter={(time, items) => {
                  const entry = items?.[0]?.payload;
                  return entry ? entry.fullDate : time;
                }}
              />

              <Legend wrapperStyle={{ fontSize: 12, paddingTop: 8 }} />

              {(metricFilter === "all" || metricFilter === "moisture") && (
                <Line
                  yAxisId="left"
                  type="monotone"
                  dataKey="moisture"
                  name="Вологість (%)"
                  stroke="#0284c7"
                  strokeWidth={2}
                  dot={{ r: 2 }}
                  activeDot={{ r: 5 }}
                />
              )}

              {(metricFilter === "all" || metricFilter === "temperature") && (
                <Line
                  yAxisId="left"
                  type="monotone"
                  dataKey="temperature"
                  name="Температура (°C)"
                  stroke="#dc2626"
                  strokeWidth={2}
                  dot={{ r: 2 }}
                  activeDot={{ r: 5 }}
                />
              )}

              {(metricFilter === "all" || metricFilter === "light") && (
                <Line
                  yAxisId={metricFilter === "all" ? "right" : "left"}
                  type="monotone"
                  dataKey="light"
                  name="Освітленість (Lux)"
                  stroke="#d97706"
                  strokeWidth={1.75}
                  dot={{ r: 2 }}
                  activeDot={{ r: 5 }}
                />
              )}
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}

const styles = {
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "flex-start",
    flexWrap: "wrap",
    gap: 12,
    marginBottom: 14,
  },
  title: {
    margin: 0,
    color: "#1f2937",
    fontSize: 15,
    fontWeight: 700,
  },
  subtitle: {
    fontSize: 12,
    color: "#6b7280",
  },
  controlsRow: {
    display: "flex",
    flexWrap: "wrap",
    alignItems: "center",
    gap: 12,
  },
  btnGroup: {
    display: "flex",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 4,
  },
  filterBtn: {
    padding: "3px 8px",
    fontSize: 11,
    borderRadius: 6,
    border: "1px solid #e5e7eb",
    cursor: "pointer",
    transition: "all 0.15s ease",
  },
  emptyState: {
    padding: 32,
    textAlign: "center",
    color: "#6b7280",
    backgroundColor: "#f9fafb",
    borderRadius: 8,
    border: "1px dashed #d1d5db",
  },
};
