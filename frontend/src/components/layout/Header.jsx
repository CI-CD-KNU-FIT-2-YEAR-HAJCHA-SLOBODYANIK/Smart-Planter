import {
  Sprout,
  Plus,
  RefreshCw,
  Send,
  UserCheck,
  LogIn,
  Cpu,
} from "lucide-react";
import { ConnectionStatus } from "./ConnectionStatus";

export function Header({
  onRefresh,
  onOpenCreateModal,
  connectionStatus,
  onReconnect,
  isPollingActive,
  currentUser,
  onOpenAuthModal,
  onOpenTelegramModal,
  onOpenSimulatorModal,
}) {
  const isTgUser = currentUser?.username?.startsWith("tg_user_");

  return (
    <header style={styles.header}>
      <div style={styles.brand}>
        <div style={styles.logoWrap}>
          <Sprout size={26} color="#166534" />
        </div>
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <h1 style={styles.title}>Smart Planter</h1>
            <ConnectionStatus
              status={connectionStatus}
              onReconnect={onReconnect}
              isPollingActive={isPollingActive}
            />
          </div>
          <p style={styles.subtitle}>
            IoT телеметрія, контроль поливу та інтеграція з Telegram
          </p>
        </div>
      </div>

      <div style={styles.actions}>
        {onOpenSimulatorModal && (
          <button
            onClick={onOpenSimulatorModal}
            style={styles.btnSimulator}
            title="Емулятор IoT датчиків (тестування передачі даних)"
          >
            <Cpu size={15} /> Емулятор IoT
          </button>
        )}

        {onOpenTelegramModal && (
          <button
            onClick={onOpenTelegramModal}
            style={{
              ...styles.btnTg,
              backgroundColor: isTgUser ? "#0284c7" : "#0284c718",
              color: isTgUser ? "#ffffff" : "#0369a1",
              border: `1px solid ${isTgUser ? "#0284c7" : "#bae6fd"}`,
            }}
            title="Інтеграція та статус Telegram-бота"
          >
            <Send size={14} />
            <span>{isTgUser ? "Telegram акаунт" : "Telegram бот"}</span>
          </button>
        )}

        {onOpenAuthModal && (
          <button
            onClick={onOpenAuthModal}
            style={styles.btnAuth}
            title="Керування обліковим записом"
          >
            {currentUser ? (
              <>
                <UserCheck size={14} color="#166534" />
                <span
                  style={{
                    maxWidth: 110,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                >
                  {currentUser.username}
                </span>
              </>
            ) : (
              <>
                <LogIn size={14} /> Увійти
              </>
            )}
          </button>
        )}

        {currentUser && (
          <button onClick={onOpenCreateModal} style={styles.btnPrimary}>
            <Plus size={16} /> Додати рослину
          </button>
        )}

        <button
          onClick={onRefresh}
          style={styles.btnSecondary}
          title="Оновити дані вручну"
        >
          <RefreshCw size={16} />
        </button>
      </div>
    </header>
  );
}

const styles = {
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 12,
    marginBottom: 20,
    paddingBottom: 16,
    borderBottom: "1px solid #e5e7eb",
  },
  brand: {
    display: "flex",
    alignItems: "center",
    gap: 12,
  },
  logoWrap: {
    width: 44,
    height: 44,
    borderRadius: 12,
    backgroundColor: "#dcfce7",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
  },
  title: {
    margin: 0,
    fontSize: 22,
    fontWeight: 700,
    color: "#166534",
  },
  subtitle: {
    margin: "2px 0 0 0",
    color: "#6b7280",
    fontSize: 13,
  },
  actions: {
    display: "flex",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 8,
  },
  btnPrimary: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    background: "#166534",
    color: "#fff",
    border: "none",
    padding: "8px 14px",
    borderRadius: 8,
    cursor: "pointer",
    fontWeight: 600,
    fontSize: 13,
    transition: "background 0.15s ease",
  },
  btnSecondary: {
    background: "#ffffff",
    border: "1px solid #d1d5db",
    padding: "8px 10px",
    borderRadius: 8,
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
    color: "#4b5563",
  },
  btnTg: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    padding: "7px 12px",
    borderRadius: 8,
    cursor: "pointer",
    fontWeight: 600,
    fontSize: 12,
    transition: "all 0.15s ease",
  },
  btnSimulator: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    padding: "7px 12px",
    borderRadius: 8,
    cursor: "pointer",
    fontWeight: 600,
    fontSize: 12,
    backgroundColor: "#f5f3ff",
    color: "#6d28d9",
    border: "1px solid #ddd6fe",
  },
  btnAuth: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    padding: "7px 12px",
    borderRadius: 8,
    cursor: "pointer",
    fontWeight: 600,
    fontSize: 12,
    backgroundColor: "#ffffff",
    color: "#374151",
    border: "1px solid #d1d5db",
  },
};
