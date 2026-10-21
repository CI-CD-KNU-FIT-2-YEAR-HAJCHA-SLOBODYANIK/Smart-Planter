import React, { useState } from "react";
import { X, Send, CheckCircle2, AlertCircle, Key, RefreshCw, Smartphone } from "lucide-react";
import { login } from "../../api/authApi";

export function TelegramBotModal({ isOpen, onClose, currentUser, onUserAuthenticated }) {
  const [telegramId, setTelegramId] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);

  if (!isOpen) return null;

  const isConnectedToTg = currentUser?.username?.startsWith("tg_user_");
  const currentTgId = isConnectedToTg ? currentUser.username.replace("tg_user_", "") : null;

  const handleConnectByTgId = async (e) => {
    e.preventDefault();
    if (!telegramId.trim()) return;

    setLoading(true);
    setError(null);
    setSuccess(false);

    const username = `tg_user_${telegramId.trim()}`;
    const password = "bot_secure_password_123";

    try {
      const data = await login(username, password);
      setSuccess(true);
      if (onUserAuthenticated) {
        onUserAuthenticated({
          id: data.id,
          username: data.username,
        });
      }
      setTimeout(() => {
        onClose();
      }, 1500);
    } catch (err) {
      setError(
        err.status === 401
          ? "Користувача з таким Telegram ID не знайдено на бекенді. Спочатку напишіть /start вашому боту в Telegram!"
          : `Помилка входу: ${err.message}`
      );
    } finally {
      setLoading(false);
    }
  };

  const BOT_COMMANDS = [
    { cmd: "/start", desc: "Реєстрація та запуск моніторингу рослин" },
    { cmd: "/status", desc: "Закріплена картка поточного стану вазона (Live)" },
    { cmd: "/water", desc: "Дистанційний запуск помпи поливу" },
    { cmd: "/newplant", desc: "Покрокове додавання нового вазона через бота" },
    { cmd: "/myplants", desc: "Перемикання активного вазона та видалення" },
    { cmd: "/credentials", desc: "Отримання логіна й пароля для веб-панелі" },
  ];

  return (
    <div style={styles.overlay}>
      <div style={styles.modal}>
        <div style={styles.header}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <div style={styles.iconCircle}>
              <Send size={18} color="#0284c7" />
            </div>
            <div>
              <h3 style={{ margin: 0, fontSize: 18, color: "#111827" }}>
                Інтеграція з Telegram-ботом
              </h3>
              <p style={{ margin: 0, fontSize: 12, color: "#6b7280" }}>
                Синхронізація сповіщень, поливу та телеметрії
              </p>
            </div>
          </div>
          <button onClick={onClose} style={styles.closeBtn}>
            <X size={20} />
          </button>
        </div>

        {/* Current status pill */}
        <div
          style={{
            ...styles.statusCard,
            backgroundColor: isConnectedToTg ? "#f0fdf4" : "#f8fafc",
            borderColor: isConnectedToTg ? "#86efac" : "#e2e8f0",
          }}
        >
          {isConnectedToTg ? (
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <CheckCircle2 size={20} color="#16a34a" />
              <div>
                <div style={{ fontWeight: 600, color: "#166534", fontSize: 13 }}>
                  Підключено до Telegram акаунта!
                </div>
                <div style={{ fontSize: 12, color: "#4b5563" }}>
                  Telegram User ID: <b>{currentTgId}</b> ({currentUser.username})
                </div>
              </div>
            </div>
          ) : (
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <Smartphone size={20} color="#64748b" />
              <div>
                <div style={{ fontWeight: 600, color: "#334155", fontSize: 13 }}>
                  Веб-сесія не прив'язана до Telegram-бота
                </div>
                <div style={{ fontSize: 12, color: "#64748b" }}>
                  Введіть свій Telegram ID нижче, щоб синхронізувати графіки з вашим ботом.
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Form to connect via Telegram ID */}
        <form onSubmit={handleConnectByTgId} style={{ marginBottom: 16 }}>
          <label style={styles.label}>
            Швидкий вхід за Telegram User ID:
          </label>
          <div style={{ display: "flex", gap: 8 }}>
            <input
              type="number"
              placeholder="Наприклад: 582194812"
              required
              value={telegramId}
              onChange={(e) => setTelegramId(e.target.value)}
              style={styles.input}
            />
            <button
              type="submit"
              disabled={loading}
              style={styles.btnConnect}
            >
              {loading ? (
                <RefreshCw size={14} style={{ animation: "spin 1s linear infinite" }} />
              ) : (
                <Key size={14} />
              )}
              Підключити
            </button>
          </div>
          <span style={styles.hint}>
            Дізнатися свій ID можна в боті, надіславши команду <code>/credentials</code>.
          </span>
        </form>

        {error && (
          <div style={styles.errorAlert}>
            <AlertCircle size={16} />
            <span>{error}</span>
          </div>
        )}

        {success && (
          <div style={styles.successAlert}>
            <CheckCircle2 size={16} />
            <span>Успішно підключено! Завантажуємо ваші рослини...</span>
          </div>
        )}

        {/* Telegram Commands Table */}
        <div style={{ marginTop: 16 }}>
          <h4 style={{ margin: "0 0 8px 0", fontSize: 13, color: "#374151" }}>
            Доступні команди Telegram-бота:
          </h4>
          <div style={styles.cmdList}>
            {BOT_COMMANDS.map((c) => (
              <div key={c.cmd} style={styles.cmdRow}>
                <code style={styles.cmdCode}>{c.cmd}</code>
                <span style={styles.cmdDesc}>{c.desc}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
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
    paddingBottom: 12,
    borderBottom: "1px solid #e5e7eb",
  },
  iconCircle: {
    width: 36,
    height: 36,
    borderRadius: "50%",
    backgroundColor: "#e0f2fe",
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
  statusCard: {
    padding: "10px 14px",
    borderRadius: 8,
    border: "1px solid #e2e8f0",
    marginBottom: 16,
  },
  label: {
    fontSize: 12,
    fontWeight: 600,
    color: "#374151",
    display: "block",
    marginBottom: 6,
  },
  input: {
    flex: 1,
    padding: "8px 12px",
    borderRadius: 8,
    border: "1px solid #d1d5db",
    fontSize: 14,
    outline: "none",
  },
  btnConnect: {
    display: "flex",
    alignItems: "center",
    gap: 6,
    backgroundColor: "#0284c7",
    color: "#fff",
    border: "none",
    borderRadius: 8,
    padding: "8px 14px",
    fontSize: 13,
    fontWeight: 600,
    cursor: "pointer",
  },
  hint: {
    display: "block",
    fontSize: 11,
    color: "#6b7280",
    marginTop: 4,
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
    marginBottom: 12,
  },
  successAlert: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    padding: "8px 12px",
    borderRadius: 6,
    backgroundColor: "#dcfce7",
    color: "#15803d",
    fontSize: 12,
    marginBottom: 12,
  },
  cmdList: {
    backgroundColor: "#f8fafc",
    borderRadius: 8,
    border: "1px solid #e2e8f0",
    padding: "6px 10px",
  },
  cmdRow: {
    display: "flex",
    alignItems: "center",
    gap: 10,
    padding: "4px 0",
    borderBottom: "1px solid #f1f5f9",
    fontSize: 12,
  },
  cmdCode: {
    fontFamily: "monospace",
    fontWeight: 700,
    color: "#0369a1",
    minWidth: 90,
  },
  cmdDesc: {
    color: "#475569",
  },
};
