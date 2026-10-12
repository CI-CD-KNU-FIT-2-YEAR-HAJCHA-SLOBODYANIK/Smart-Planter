import asyncio
import os
import random
import requests
from aiogram import Bot, Dispatcher
from aiogram.filters import CommandStart
from aiogram.types import Message

BACKEND_API_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1").rstrip('/')
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

app_state = {
    "device_key": None
}

def init_backend():
    """Спрощена авторизація для отримання ключа пристрою (Спринт 1)"""
    auth_data = {"username": "telegram_bot_user", "password": "bot_secure_password_123"}
    try:
        # Логін або реєстрація
        resp = requests.post(f"{BACKEND_API_URL}/users/login", json=auth_data)
        if resp.status_code == 401:
            resp = requests.post(f"{BACKEND_API_URL}/users/register", json=auth_data)

        if resp.status_code in [200, 201]:
            data = resp.json()
            jwt_token = data.get("token")
            plants = data.get("plants") or []

            # Якщо рослини ще немає — створюємо
            if not plants:
                headers = {"Authorization": f"Bearer {jwt_token}"}
                plant_data = {
                    "name": "Монстера (Спринт 1)",
                    "species": "Monstera",
                    "minMoisture": 30.0, "maxMoisture": 70.0,
                    "minTemp": 18.0, "maxTemp": 28.0,
                    "minLight": 300.0
                }
                p_resp = requests.post(f"{BACKEND_API_URL}/plants", json=plant_data, headers=headers)
                if p_resp.status_code == 201:
                    app_state["device_key"] = p_resp.json().get("apiKey")
            else:
                app_state["device_key"] = plants[0].get("apiKey")

            print(f"✅ Бекенд ініціалізовано. Device Key: {app_state['device_key']}")
    except Exception as e:
        print(f"❌ Помилка підключення до бекенда: {e}")

# --- БЛОК TELEGRAM БОТА ---
@dp.message(CommandStart())
async def cmd_start(message: Message):
    await message.answer("🪴 Привіт! Я бот системи Smart Planter (Спринт 1: Каркас).")

# --- БЛОК ГЕНЕРАТОРА ТЕЛЕМЕТРІЇ ---
async def generate_telemetry():
    await asyncio.sleep(5) # Чекаємо запуску бази даних
    init_backend()

    current_moisture = 85.0
    while True:
        if app_state["device_key"]:
            current_moisture -= random.uniform(0.5, 1.5)
            if current_moisture < 5.0: current_moisture = 5.0

            payload = {
                "moisture": round(current_moisture, 1),
                "temperature": round(random.uniform(21.0, 23.0), 1),
                "light": round(random.uniform(400.0, 500.0), 1)
            }
            headers = {"X-Device-Key": app_state["device_key"]}
            try:
                resp = requests.post(f"{BACKEND_API_URL}/telemetry", json=payload, headers=headers)
                print(f"Телеметрія: {payload} | Статус: {resp.status_code}")
            except Exception:
                pass
        await asyncio.sleep(10)

async def main():
    if not BOT_TOKEN:
        print("ПОМИЛКА: TELEGRAM_BOT_TOKEN не знайдено!")
        return

    # Запускаємо генератор у фоні
    asyncio.create_task(generate_telemetry())

    print("Бот запускається...")
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
