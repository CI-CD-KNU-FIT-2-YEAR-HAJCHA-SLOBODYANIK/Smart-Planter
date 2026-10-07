const BASE_URL = "http://localhost:8000/api/v1/plants";

export async function fetchPlants() {
  const response = await fetch(BASE_URL);
  if (!response.ok) throw new Error("Не вдалося завантажити список рослин");
  return response.json();
}

export async function createPlant(plantData) {
  const response = await fetch(BASE_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(plantData),
  });
  if (!response.ok) throw new Error("Не вдалося створити рослину");
  return response.json();
}

export async function waterPlant(plantId) {
  const response = await fetch(`${BASE_URL}/api/v1/plants/${plantId}/water`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
  });
  if (!response.ok) {
    throw new Error(`Помилка поливу: ${response.status}`);
  }
  return response.json().catch(() => ({ success: true }));
}
