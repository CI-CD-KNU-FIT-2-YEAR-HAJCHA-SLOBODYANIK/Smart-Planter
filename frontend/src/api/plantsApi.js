const BASE_URL = "http://localhost:5000/api/v1/plants";

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
