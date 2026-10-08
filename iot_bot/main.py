import asyncio
import os
import random
import requests
from aiogram.exceptions import TelegramBadRequest
from aiogram import Bot, Dispatcher, F
from aiogram.filters import CommandStart, Command
from aiogram.types import Message, InlineKeyboardMarkup, InlineKeyboardButton, CallbackQuery

# Налаштування URL
BACKEND_TELEMETRY_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1/Telemetry")
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "ТВІЙ_ТОКЕН")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

def get_status_keyboard():
    """Створення клавіатури з кнопками оновлення та поливу"""
    keyboard = InlineKeyboardMarkup(inline_keyboard=[
        [
            InlineKeyboardButton(text="🔄 Оновити", callback_data="refresh_status"),
            InlineKeyboardButton(text="💧 Полити", callback_data="water_plant")
        ]
    ])
    return keyboard

@dp.message(CommandStart())
async def cmd_start(message: Message):
    await message.answer(
        "🪴 Привіт! Я бот системи Smart Planter.\n"
        "Напиши /status, щоб перевірити стан вазона.",
        reply_markup=get_status_keyboard()
    )

@dp.message(Command("status"))
async def cmd_status(message: Message):
    await send_plant_status(message)

async def send_plant_status(message_or_callback: Message | CallbackQuery, is_callback: bool = False):
    plant_id = 1
    history_url = f"{BACKEND_TELEMETRY_URL}/{plant_id}/history"

    try:
        response = requests.get(history_url)
        if response.status_code == 200:
            data = response.json()
            if data and len(data) > 0:
                latest = data[-1]
                msg = (
                    f"📊 <b>Поточний стан (Рослина #{plant_id}):</b>\n\n"
                    f"💧 Вологість ґрунту: <b>{latest.get('moisture')}%</b>\n"
                    f"🌡 Температура: <b>{latest.get('temperature')}°C</b>\n"
                    f"☀️ Освітленість: <b>{latest.get('light')} lux</b>"
                )

                try:
                    if is_callback:
                        await message_or_callback.message.edit_text(msg, parse_mode="HTML", reply_markup=get_status_keyboard())
                    else:
                        await message_or_callback.answer(msg, parse_mode="HTML", reply_markup=get_status_keyboard())
                except TelegramBadRequest:
                    # Якщо текст і кнопки абсолютно такі ж самі, Telegram кине помилку — просто ігноруємо її
                    pass
            else:
                text = "🤷‍♂️ Дані про цю рослину ще не надходили."
                if is_callback:
                    try:
                        await message_or_callback.message.edit_text(text)
                    except TelegramBadRequest:
                        pass
                else:
                    await message_or_callback.answer(text)
        else:
            text = f"⚠️ Помилка бекенда: {response.status_code}"
            if is_callback:
                try:
                    await message_or_callback.message.edit_text(text)
                except TelegramBadRequest:
                    pass
            else:
                await message_or_callback.answer(text)

    except requests.exceptions.RequestException:
        text = "❌ Не вдалося з'єднатися з сервером бази даних."
        if is_callback:
            try:
                await message_or_callback.message.edit_text(text)
            except TelegramBadRequest:
                pass
        else:
            await message_or_callback.answer(text)

@dp.callback_query(F.data == "refresh_status")
async def callback_refresh(callback: CallbackQuery):
    await callback.answer("Дані оновлено!")
    await send_plant_status(callback, is_callback=True)

@dp.callback_query(F.data == "water_plant")
async def callback_water(callback: CallbackQuery):
    # Симулюємо команду поливу (можна буде підключити реальний POST-запит до бекенда)
    await callback.answer("💧 Полив активовано! Ґрунт зволожується...", show_alert=True)

    # Можемо надіслати підтвердження в чат
    await callback.message.answer("✅ Команду на полив успішно відправлено на помпу!")

async def generate_telemetry():
    await asyncio.sleep(5)
    current_moisture = 85.0

    while True:
        try:
            current_moisture -= random.uniform(0.5, 1.5)
            if current_moisture < 5.0:
                current_moisture = 5.0

            payload = {
                "plantId": 1,
                "moisture": round(current_moisture, 1),
                "temperature": round(random.uniform(21.0, 23.0), 1),
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
