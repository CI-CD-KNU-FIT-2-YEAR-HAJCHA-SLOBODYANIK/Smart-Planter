import asyncio
import os
import random
import requests
from aiogram import Bot, Dispatcher
from aiogram.filters import CommandStart
from aiogram.types import Message

BACKEND_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1/Telemetry")
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "ТВІЙ_ТОКЕН")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

@dp.message(CommandStart())
async def cmd_start(message: Message):
    await message.answer("🪴 Привіт! Я бот системи Smart Planter. Моніторинг запущено.")

async def generate_telemetry():
    await asyncio.sleep(5) # Чекаємо 5 секунд після старту, щоб бекенд точно піднявся
    while True:
        try:
            payload = {
                "plantId": 1,
                "moisture": round(random.uniform(30.0, 70.0), 1),
                "temperature": round(random.uniform(18.0, 26.0), 1),
                "light": round(random.uniform(200.0, 800.0), 1)
            }

            response = requests.post(BACKEND_URL, json=payload)
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
