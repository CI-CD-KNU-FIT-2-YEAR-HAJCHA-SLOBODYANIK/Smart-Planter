import { useState } from "react";
import { X, LogIn, UserPlus, LogOut, KeyRound, Server, CheckCircle2, AlertCircle } from "lucide-react";
import { login, register, logout, changePassword } from "../../api/authApi";
import { getApiBaseUrl, setApiBaseUrl } from "../../api/client";

export function AuthModal({ isOpen, onClose, currentUser, onUserAuthenticated, onUserLoggedOut }) {
  const [mode, setMode] = useState("login"); // 'login' | 'register' | 'profile'
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [apiUrl, setApiUrl] = useState(getApiBaseUrl());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  if (!isOpen) return null;

  const handleAuthSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      let data;
      if (mode === "login") {
        data = await login(username, password);
        setSuccess("Успішний вхід!");
      } else {
        data = await register(username, password);
        setSuccess("Реєстрація успішна!");
      }

      if (onUserAuthenticated) {
        onUserAuthenticated({
          id: data.id,
          username: data.username,
        });
      }

      setTimeout(() => {
        onClose();
      }, 1000);
    } catch (err) {
      setError(err.message || "Помилка автентифікації");
    } finally {
      setLoading(false);
    }
  };

  const handleChangePassword = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      await changePassword(oldPassword, newPassword);
      setSuccess("Пароль успішно змінено!");
      setOldPassword("");
      setNewPassword("");
    } catch (err) {
      setError(err.message || "Не вдалося змінити пароль");
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    logout();
    if (onUserLoggedOut) {
      onUserLoggedOut();
    }
    onClose();
  };

  const handleSaveApiUrl = (e) => {
    e.preventDefault();
    setApiBaseUrl(apiUrl);
    setSuccess("Адресу API оновлено!");
    setTimeout(() => setSuccess(null), 2000);
  };

  return (
    <div style={styles.overlay}>
      <div style={styles.modal}>
        <div style={styles.header}>
          <h3 style={{ margin: 0, fontSize: 18, color: "#111827" }}>
            {currentUser ? "Обліковий запис" : mode === "login" ? "Вхід у систему" : "Реєстрація"}
          </h3>
          <button onClick={onClose} style={styles.closeBtn}>
            <X size={20} />
          </button>
        </div>

        {error && (
          <div style={styles.errorAlert}>
            <AlertCircle size={16} />
            <span>{error}</span>
          </div>
        )}

        {success && (
          <div style={styles.successAlert}>
            <CheckCircle2 size={16} />
            <span>{success}</span>
          </div>
        )}

        {currentUser ? (
          <div>
            <div style={styles.profileBox}>
              <div style={{ fontSize: 13, color: "#6b7280" }}>Поточний користувач:</div>
              <div style={{ fontSize: 18, fontWeight: 700, color: "#166534" }}>
                {currentUser.username}
              </div>
              <div style={{ fontSize: 11, color: "#9ca3af", marginTop: 2 }}>
                ID: {currentUser.id}
              </div>
            </div>

            {/* Change Password Form */}
            <form onSubmit={handleChangePassword} style={{ marginBottom: 16 }}>
              <label style={styles.label}>
                <KeyRound size={13} style={{ verticalAlign: "middle", marginRight: 4 }} />
                Зміна пароля:
              </label>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <input
                  type="password"
                  placeholder="Старий пароль"
                  required
                  value={oldPassword}
                  onChange={(e) => setOldPassword(e.target.value)}
                  style={styles.input}
                />
                <input
                  type="password"
                  placeholder="Новий пароль"
                  required
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  style={styles.input}
                />
                <button type="submit" disabled={loading} style={styles.btnSecondaryAction}>
                  Змінити пароль
                </button>
              </div>
            </form>

            {/* API Endpoint Config */}
            <form onSubmit={handleSaveApiUrl} style={{ marginBottom: 16 }}>
              <label style={styles.label}>
                <Server size={13} style={{ verticalAlign: "middle", marginRight: 4 }} />
                Адреса бекенду (API Endpoint):
              </label>
              <div style={{ display: "flex", gap: 6 }}>
                <input
                  value={apiUrl}
                  onChange={(e) => setApiUrl(e.target.value)}
                  style={styles.input}
                />
                <button type="submit" style={styles.btnSecondaryAction}>
                  Зберегти
                </button>
              </div>
            </form>

            <button onClick={handleLogout} style={styles.btnLogout}>
              <LogOut size={16} /> Вийти з облікового запису
            </button>
          </div>
        ) : (
          <div>
            <div style={styles.tabGroup}>
              <button
                type="button"
                onClick={() => { setMode("login"); setError(null); }}
                style={{
                  ...styles.tabBtn,
                  borderBottom: mode === "login" ? "2px solid #166534" : "none",
                  fontWeight: mode === "login" ? 700 : 400,
                  color: mode === "login" ? "#166534" : "#6b7280",
                }}
              >
                Вхід
              </button>
              <button
                type="button"
                onClick={() => { setMode("register"); setError(null); }}
                style={{
                  ...styles.tabBtn,
                  borderBottom: mode === "register" ? "2px solid #166534" : "none",
                  fontWeight: mode === "register" ? 700 : 400,
                  color: mode === "register" ? "#166534" : "#6b7280",
                }}
              >
                Реєстрація
              </button>
            </div>

            <form onSubmit={handleAuthSubmit} style={styles.form}>
              <div>
                <label style={styles.label}>Ім'я користувача (Username):</label>
                <input
                  required
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="Наприклад: gardener_1"
                  style={styles.input}
                />
              </div>

              <div>
                <label style={styles.label}>Пароль (Password):</label>
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Ваш пароль"
                  style={styles.input}
                />
              </div>

              <button type="submit" disabled={loading} style={styles.btnSubmit}>
                {mode === "login" ? <LogIn size={16} /> : <UserPlus size={16} />}
                {mode === "login" ? "Увійти" : "Зареєструватися"}
              </button>
            </form>

            {/* Quick config for Backend URL */}
            <div style={{ marginTop: 16, paddingTop: 12, borderTop: "1px solid #f3f4f6" }}>
              <label style={styles.label}>Сервер бекенду:</label>
              <div style={{ display: "flex", gap: 6 }}>
                <input
                  value={apiUrl}
                  onChange={(e) => setApiUrl(e.target.value)}
                  style={{ ...styles.input, fontSize: 12 }}
                />
                <button
                  type="button"
                  onClick={() => setApiBaseUrl(apiUrl)}
                  style={{ ...styles.btnSecondaryAction, fontSize: 11 }}
                >
                  OK
                </button>
              </div>
            </div>
          </div>
        )}
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
    maxWidth: 420,
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
  closeBtn: {
    background: "none",
    border: "none",
    cursor: "pointer",
    color: "#6b7280",
    display: "flex",
    alignItems: "center",
  },
  tabGroup: {
    display: "flex",
    borderBottom: "1px solid #e5e7eb",
    marginBottom: 16,
  },
  tabBtn: {
    flex: 1,
    padding: "8px 0",
    background: "none",
    border: "none",
    cursor: "pointer",
    fontSize: 14,
  },
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 12,
  },
  label: {
    fontSize: 12,
    fontWeight: 600,
    color: "#4b5563",
    display: "block",
    marginBottom: 4,
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
  btnSubmit: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 6,
    backgroundColor: "#166534",
    color: "#fff",
    border: "none",
    borderRadius: 8,
    padding: "10px",
    fontSize: 14,
    fontWeight: 600,
    cursor: "pointer",
    marginTop: 6,
  },
  profileBox: {
    padding: "12px 14px",
    backgroundColor: "#f0fdf4",
    border: "1px solid #bbf7d0",
    borderRadius: 8,
    marginBottom: 16,
  },
  btnSecondaryAction: {
    padding: "6px 12px",
    borderRadius: 6,
    border: "1px solid #d1d5db",
    backgroundColor: "#f9fafb",
    color: "#374151",
    fontSize: 12,
    fontWeight: 500,
    cursor: "pointer",
    whiteSpace: "nowrap",
  },
  btnLogout: {
    width: "100%",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 6,
    padding: "9px",
    borderRadius: 8,
    border: "1px solid #fecaca",
    backgroundColor: "#fef2f2",
    color: "#dc2626",
    fontSize: 13,
    fontWeight: 600,
    cursor: "pointer",
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
};
