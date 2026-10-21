const DEFAULT_API_URL = "http://localhost:8000/api/v1";

export function getApiBaseUrl() {
  return localStorage.getItem("smart_planter_api_url") || DEFAULT_API_URL;
}

export function setApiBaseUrl(url) {
  if (url) {
    localStorage.setItem("smart_planter_api_url", url.replace(/\/+$/, ""));
  } else {
    localStorage.removeItem("smart_planter_api_url");
  }
}

export function getAuthToken() {
  return localStorage.getItem("smart_planter_token") || null;
}

export function setAuthToken(token) {
  if (token) {
    localStorage.setItem("smart_planter_token", token);
  } else {
    localStorage.removeItem("smart_planter_token");
  }
}

export function getStoredUser() {
  try {
    const raw = localStorage.getItem("smart_planter_user");
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function setStoredUser(user) {
  if (user) {
    localStorage.setItem("smart_planter_user", JSON.stringify(user));
  } else {
    localStorage.removeItem("smart_planter_user");
  }
}

export function clearAuth() {
  localStorage.removeItem("smart_planter_token");
  localStorage.removeItem("smart_planter_user");
}

export async function apiFetch(endpoint, options = {}) {
  const baseUrl = getApiBaseUrl();
  const cleanEndpoint = endpoint.startsWith("/") ? endpoint : `/${endpoint}`;
  const url = `${baseUrl}${cleanEndpoint}`;

  const headers = {
    Accept: "application/json",
    ...options.headers,
  };

  const token = getAuthToken();
  if (token && !headers["Authorization"]) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  if (options.body && typeof options.body === "object" && !(options.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(options.body);
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get("content-type") || "";
  let data = null;
  if (contentType.includes("application/json")) {
    try {
      data = await response.json();
    } catch {
      data = null;
    }
  } else {
    data = await response.text();
  }

  if (!response.ok) {
    const errorMsg =
      (data && typeof data === "object" && (data.message || data.title || JSON.stringify(data))) ||
      (typeof data === "string" && data) ||
      `HTTP Error ${response.status} (${response.statusText})`;
    const error = new Error(errorMsg);
    error.status = response.status;
    error.data = data;
    throw error;
  }

  return data;
}
