import asyncio
import os
import random
import requests
from aiogram import Bot, Dispatcher, F
from aiogram.filters import CommandStart, Command
from aiogram.types import Message, InlineKeyboardMarkup, InlineKeyboardButton, CallbackQuery
from aiogram.exceptions import TelegramBadRequest

BACKEND_API_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1").rstrip('/')
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

app_state = {
    "jwt_token": None,
    "device_key": None,
    "plant_id": None,
    "current_moisture": 85.0
}

def init_backend():
    """Авторизація та отримання ключів/рослин згідно з документацією бекенда"""
    auth_data = {"username": "telegram_bot_user", "password": "bot_secure_password_123"}
    try:
        print("Спроба авторизації на бекенді...")
        resp = requests.post(f"{BACKEND_API_URL}/users/login", json=auth_data)

        if resp.status_code == 401:
            print("Користувача не знайдено. Реєструємо нового...")
            resp = requests.post(f"{BACKEND_API_URL}/users/register", json=auth_data)

        if resp.status_code in [200, 201]:
            data = resp.json()
            app_state["jwt_token"] = data.get("token")
            plants = data.get("plants") or []

            if not plants:
                print("Створюємо першу рослину...")
                headers = {"Authorization": f"Bearer {app_state['jwt_token']}"}
                plant_data = {
                    "name": "Монстера (Бот)",
                    "species": "Monstera deliciosa",
                    "minMoisture": 30.0, "maxMoisture": 70.0,
                    "minTemp": 18.0, "maxTemp": 28.0,
                    "minLight": 300.0
                }
                p_resp = requests.post(f"{BACKEND_API_URL}/plants", json=plant_data, headers=headers)
                if p_resp.status_code == 201:
                    created_plant = p_resp.json()
                    app_state["device_key"] = created_plant.get("apiKey")
                    app_state["plant_id"] = created_plant.get("id")
            else:
                app_state["device_key"] = plants[0].get("apiKey")
                app_state["plant_id"] = plants[0].get("id")

            print(f"✅ Успішно! Plant ID: {app_state['plant_id']}")
        else:
            print(f"❌ Помилка авторизації: {resp.status_code} - {resp.text}")
    except requests.exceptions.RequestException as e:
        print(f"❌ Не вдалося з'єднатися з бекендом: {e}")

def get_status_keyboard():
    return InlineKeyboardMarkup(inline_keyboard=[
        [
            InlineKeyboardButton(text="🔄 Оновити", callback_data="refresh_status"),
            InlineKeyboardButton(text="💧 Полити", callback_data="water_plant")
        ]
    ])

@dp.message(CommandStart())
async def cmd_start(message: Message):
    await message.answer(
        "🪴 Привіт! Я бот системи Smart Planter (Спринт 2).\n"
        "Напиши /status, щоб перевірити стан вазона.",
        reply_markup=get_status_keyboard()
    )

async def send_plant_status(message_or_callback: Message | CallbackQuery, is_callback: bool = False):
    if not app_state["jwt_token"] or not app_state["plant_id"]:
        text = "⏳ Бот ще не підключився до бекенда. Зачекайте пару секунд."
        if is_callback: await message_or_callback.message.edit_text(text)
        else: await message_or_callback.answer(text)
        return

    history_url = f"{BACKEND_API_URL}/telemetry/{app_state['plant_id']}/history?limit=1"
    headers = {"Authorization": f"Bearer {app_state['jwt_token']}"}

    try:
        response = requests.get(history_url, headers=headers)
        if response.status_code == 200:
            data = response.json()
            if data and len(data) > 0:
                latest = data[0]
                msg = (
                    f"📊 <b>Поточний стан (Рослина #{app_state['plant_id']}):</b>\n\n"
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
                    pass
            else:
                text = "🤷‍♂️ Дані про цю рослину ще не надходили."
                if is_callback: await message_or_callback.message.edit_text(text)
                else: await message_or_callback.answer(text)
        else:
            text = f"⚠️ Помилка бекенда: {response.status_code}"
            if is_callback: await message_or_callback.message.edit_text(text)
            else: await message_or_callback.answer(text)
    except requests.exceptions.RequestException:
        text = "❌ Не вдалося з'єднатися з сервером."
        if is_callback: await message_or_callback.message.edit_text(text)
        else: await message_or_callback.answer(text)

@dp.message(Command("status"))
async def cmd_status(message: Message):
    await send_plant_status(message)

@dp.callback_query(F.data == "refresh_status")
async def callback_refresh(callback: CallbackQuery):
    await callback.answer("Дані оновлено!")
    await send_plant_status(callback, is_callback=True)

@dp.callback_query(F.data == "water_plant")
async def callback_water(callback: CallbackQuery):
    if not app_state["jwt_token"] or not app_state["plant_id"]:
        await callback.answer("⏳ Немає зв'язку з бекендом.", show_alert=True)
        return

    water_url = f"{BACKEND_API_URL}/plants/{app_state['plant_id']}/water"
    headers = {"Authorization": f"Bearer {app_state['jwt_token']}"}

    try:
        resp = requests.post(water_url, headers=headers)
        if resp.status_code == 200:
            app_state["current_moisture"] = 85.0
            await callback.answer("💧 Полив активовано!", show_alert=True)
            await callback.message.answer("✅ Команду на полив успішно зафіксовано на бекенді!")
        else:
            await callback.answer(f"⚠️ Помилка поливу: {resp.status_code}", show_alert=True)
    except requests.exceptions.RequestException:
        await callback.answer("❌ Сервер недоступний.", show_alert=True)

async def generate_telemetry():
    await asyncio.sleep(5)  # Чекаємо підняття бекенда
    init_backend()

    while True:
        if app_state["device_key"]:
            app_state["current_moisture"] -= random.uniform(0.5, 1.5)
            if app_state["current_moisture"] < 5.0:
                app_state["current_moisture"] = 5.0

            payload = {
                "moisture": round(app_state["current_moisture"], 1),
                "temperature": round(random.uniform(21.0, 23.0), 1),
                "light": round(random.uniform(400.0, 500.0), 1)
            }
            headers = {"X-Device-Key": app_state["device_key"]}
            try:
                requests.post(f"{BACKEND_API_URL}/telemetry", json=payload, headers=headers)
            except Exception:
                pass
        await asyncio.sleep(10)

async def main():
    if not BOT_TOKEN:
        print("ПОМИЛКА: TELEGRAM_BOT_TOKEN не задано!")
        return

    asyncio.create_task(generate_telemetry())
    print("Бот запускається...")
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
