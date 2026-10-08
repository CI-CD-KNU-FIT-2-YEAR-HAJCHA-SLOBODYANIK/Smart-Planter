import asyncio
import os
import random
import requests
from aiogram import Bot, Dispatcher
from aiogram.filters import CommandStart, Command
from aiogram.types import Message

# Налаштування URL
BACKEND_TELEMETRY_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1/Telemetry")
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "ТВІЙ_ТОКЕН")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

# Обробка команди /start
@dp.message(CommandStart())
async def cmd_start(message: Message):
    await message.answer(
        "🪴 Привіт! Я бот системи Smart Planter.\n"
        "Напиши /status, щоб дізнатися останні показники вазону."
    )

# Обробка команди /status
@dp.message(Command("status"))
async def cmd_status(message: Message):
    plant_id = 1
    # Формуємо URL для отримання історії згідно зі Swagger-документацією
    history_url = f"{BACKEND_TELEMETRY_URL}/{plant_id}/history"

    try:
        response = requests.get(history_url)
        if response.status_code == 200:
            data = response.json()

            # Перевіряємо, чи є хоча б один запис
            if data and len(data) > 0:
                # Беремо останній запис з масиву (найсвіжіший)
                latest = data[-1]

                msg = (
                    f"📊 <b>Поточний стан (Рослина #{plant_id}):</b>\n\n"
                    f"💧 Вологість ґрунту: <b>{latest.get('moisture')}%</b>\n"
                    f"🌡 Температура: <b>{latest.get('temperature')}°C</b>\n"
                    f"☀️ Освітленість: <b>{latest.get('light')} lux</b>"
                )
                await message.answer(msg, parse_mode="HTML")
            else:
                await message.answer("🤷‍♂️ Дані про цю рослину ще не надходили.")
        else:
            await message.answer(f"⚠️ Помилка бекенда: {response.status_code}")

    except requests.exceptions.RequestException as e:
        await message.answer("❌ Не вдалося з'єднатися з сервером бази даних.")

# Реалістичний генератор телеметрії
async def generate_telemetry():
    await asyncio.sleep(5) # Чекаємо, поки підніметься бекенд

    current_moisture = 85.0 # Початкова вологість (ґрунт полито)

    while True:
        try:
            # Симулюємо поступове висихання ґрунту (віднімаємо від 0.5 до 1.5% кожні 10 сек)
            current_moisture -= random.uniform(0.5, 1.5)
            if current_moisture < 5.0:
                current_moisture = 5.0 # Запобіжник, щоб вологість не пішла в мінус

            payload = {
                "plantId": 1,
                "moisture": round(current_moisture, 1),
                "temperature": round(random.uniform(21.0, 23.0), 1), # Температура в кімнаті стабільніша
                "light": round(random.uniform(400.0, 500.0), 1)
            }

            response = requests.post(BACKEND_TELEMETRY_URL, json=payload)
            print(f"Відправлено: {payload} | Статус: {response.status_code}")

        except requests.exceptions.RequestException as e:
            print(f"Помилка з'єднання: {e}")

        await asyncio.sleep(10)

async def main():
    asyncio.create_task(generate_telemetry())
    print("Бот запускається...")
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
