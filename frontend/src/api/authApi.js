import { apiFetch, setAuthToken, setStoredUser, clearAuth } from "./client";

export async function login(username, password) {
  const data = await apiFetch("/users/login", {
    method: "POST",
    body: { username, password },
  });
  if (data?.token) {
    setAuthToken(data.token);
    setStoredUser({
      id: data.id,
      username: data.username,
      createdAt: data.createdAt,
    });
  }
  return data;
}

export async function register(username, password) {
  const data = await apiFetch("/users/register", {
    method: "POST",
    body: { username, password },
  });
  if (data?.token) {
    setAuthToken(data.token);
    setStoredUser({
      id: data.id,
      username: data.username,
      createdAt: data.createdAt,
    });
  }
  return data;
}

export async function getMe() {
  const data = await apiFetch("/users/me");
  if (data) {
    setStoredUser({
      id: data.id,
      username: data.username,
      createdAt: data.createdAt,
    });
  }
  return data;
}

export async function changePassword(oldPassword, newPassword) {
  return apiFetch("/users/change-password", {
    method: "PUT",
    body: { oldPassword, newPassword },
  });
}

export async function deleteAccount() {
  const res = await apiFetch("/users/me", {
    method: "DELETE",
  });
  clearAuth();
  return res;
}

export function logout() {
  clearAuth();
}
