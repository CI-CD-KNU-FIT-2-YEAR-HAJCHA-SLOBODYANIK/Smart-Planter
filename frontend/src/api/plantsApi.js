import { apiFetch } from "./client";

export async function fetchPlants() {
  return apiFetch("/plants");
}

export async function createPlant(plantData) {
  return apiFetch("/plants", {
    method: "POST",
    body: plantData,
  });
}

export async function updatePlant(plantId, plantData) {
  return apiFetch(`/plants/${plantId}`, {
    method: "PUT",
    body: plantData,
  });
}

export async function deletePlant(plantId) {
  return apiFetch(`/plants/${plantId}`, {
    method: "DELETE",
  });
}

export async function waterPlant(plantId) {
  return apiFetch(`/plants/${plantId}/water`, {
    method: "POST",
  });
}
