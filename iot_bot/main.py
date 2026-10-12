import asyncio
import os
import random
import requests
from aiogram import Bot, Dispatcher, F
from aiogram.filters import CommandStart, Command
from aiogram.types import Message, InlineKeyboardMarkup, InlineKeyboardButton, CallbackQuery, BotCommand
from aiogram.exceptions import TelegramBadRequest

BACKEND_API_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1").rstrip('/')
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

app_state = {
    "jwt_token": None,
    "device_key": None,
    "plant_id": None,
    "current_moisture": 85.0,
    "last_alert_id": None,
    "target_chat_id": None,
    "status_message_id": None  # ID повідомлення для автооновлення в реальному часі
}

def init_backend():
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

def get_inline_status_keyboard():
    return InlineKeyboardMarkup(inline_keyboard=[
        [
            InlineKeyboardButton(text="🔄 Оновити", callback_data="refresh_status"),
            InlineKeyboardButton(text="🌱 Полити", callback_data="water_plant")
        ]
    ])

@dp.message(CommandStart())
async def cmd_start(message: Message):
    app_state["target_chat_id"] = message.chat.id
    await message.answer(
        "🪴 Привіт! Я бот системи Smart Planter.\n"
        "Скористайтеся синьою кнопкою «Меню» зліва від поля введення, щоб обрати команду!",
    )

async def send_or_update_pinned_status(message_or_callback: Message | CallbackQuery, is_callback: bool = False):
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
                    f"📊 <b>ПАНЕЛЬ МОНІТОРИНГУ РОСЛИНИ (Live)</b>\n\n"
                    f"🌱 Вазон: <b>Монстера (ID #{app_state['plant_id']})</b>\n"
                    f"💧 Вологість ґрунту: <b>{latest.get('moisture')}%</b>\n"
                    f"🌡 Температура: <b>{latest.get('temperature')}°C</b>\n"
                    f"☀️ Освітленість: <b>{latest.get('light')} lux</b>\n\n"
                    f"<i>🔄 Дані оновлюються автоматично в реальному часі.</i>"
                )

                if is_callback:
                    try:
                        await message_or_callback.message.edit_text(msg, parse_mode="HTML", reply_markup=get_inline_status_keyboard())
                        app_state["status_message_id"] = message_or_callback.message.message_id
                    except TelegramBadRequest:
                        pass
                else:
                    sent_msg = await message_or_callback.answer(msg, parse_mode="HTML", reply_markup=get_inline_status_keyboard())
                    app_state["status_message_id"] = sent_msg.message_id
                    try:
                        await bot.pin_chat_message(chat_id=sent_msg.chat.id, message_id=sent_msg.message_id)
                    except Exception:
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
    app_state["target_chat_id"] = message.chat.id
    await send_or_update_pinned_status(message)

@dp.message(Command("water"))
async def cmd_water(message: Message):
    app_state["target_chat_id"] = message.chat.id
    await trigger_watering(message)

async def trigger_watering(message_or_callback):
    if not app_state["jwt_token"] or not app_state["plant_id"]:
        if isinstance(message_or_callback, CallbackQuery):
            await message_or_callback.answer("⏳ Немає зв'язку з бекендом.", show_alert=True)
        else:
            await message_or_callback.answer("⏳ Немає зв'язку з бекендом.")
        return

    water_url = f"{BACKEND_API_URL}/plants/{app_state['plant_id']}/water"
    headers = {"Authorization": f"Bearer {app_state['jwt_token']}"}

    try:
        resp = requests.post(water_url, headers=headers)
        if resp.status_code == 200:
            app_state["current_moisture"] = 85.0
            success_text = "✅ Команду на полив успішно відправлено. Помпи запускаються, вологість відновлено!"
            if isinstance(message_or_callback, CallbackQuery):
                await message_or_callback.answer("💧 Полив активовано!", show_alert=True)
                await message_or_callback.message.answer(success_text)
            else:
                await message_or_callback.answer(success_text)
        else:
            err_text = f"⚠️ Помилка поливу: {resp.status_code}"
            if isinstance(message_or_callback, CallbackQuery):
                await message_or_callback.answer(err_text, show_alert=True)
            else:
                await message_or_callback.answer(err_text)
    except requests.exceptions.RequestException:
        if isinstance(message_or_callback, CallbackQuery):
            await message_or_callback.answer("❌ Сервер недоступний.", show_alert=True)
        else:
            await message_or_callback.answer("❌ Сервер недоступний.")

@dp.callback_query(F.data == "refresh_status")
async def callback_refresh(callback: CallbackQuery):
    await callback.answer("Дані оновлено!")
    await send_or_update_pinned_status(callback, is_callback=True)

@dp.callback_query(F.data == "water_plant")
async def callback_water(callback: CallbackQuery):
    await trigger_watering(callback)

async def auto_refresh_status_loop():
    """Фонова задача для автоматичного оновлення інформації в панелі моніторингу"""
    while True:
        await asyncio.sleep(12)
        if app_state["jwt_token"] and app_state["plant_id"] and app_state["target_chat_id"] and app_state["status_message_id"]:
            history_url = f"{BACKEND_API_URL}/telemetry/{app_state['plant_id']}/history?limit=1"
            headers = {"Authorization": f"Bearer {app_state['jwt_token']}"}
            try:
                response = requests.get(history_url, headers=headers)
                if response.status_code == 200:
                    data = response.json()
                    if data and len(data) > 0:
                        latest = data[0]
                        msg = (
                            f"📊 <b>ПАНЕЛЬ МОНІТОРИНГУ РОСЛИНИ (Live)</b>\n\n"
                            f"🌱 Вазон: <b>Монстера (ID #{app_state['plant_id']})</b>\n"
                            f"💧 Вологість ґрунту: <b>{latest.get('moisture')}%</b>\n"
                            f"🌡 Температура: <b>{latest.get('temperature')}°C</b>\n"
                            f"☀️ Освітленість: <b>{latest.get('light')} lux</b>\n\n"
                            f"<i>🔄 Дані оновлюються автоматично в реальному часі.</i>"
                        )
                        await bot.edit_message_text(
                            chat_id=app_state["target_chat_id"],
                            message_id=app_state["status_message_id"],
                            text=msg,
                            parse_mode="HTML",
                            reply_markup=get_inline_status_keyboard()
                        )
            except Exception:
                pass

async def check_alerts_loop():
    """Фонова перевірка активних алертів із перевіркою реальної вологісті"""
    while True:
        await asyncio.sleep(15)
        if app_state["jwt_token"] and app_state["target_chat_id"]:
            try:
                headers = {"Authorization": f"Bearer {app_state['jwt_token']}"}

                # Спочатку перевіримо поточну вологість з історії, щоб уникнути хибних тривог
                history_resp = requests.get(f"{BACKEND_API_URL}/telemetry/{app_state['plant_id']}/history?limit=1", headers=headers)
                current_moisture = 50.0
                if history_resp.status_code == 200:
                    h_data = history_resp.json()
                    if h_data:
                        current_moisture = h_data[0].get('moisture', 50.0)

                resp = requests.get(f"{BACKEND_API_URL}/alerts/active", headers=headers)
                if resp.status_code == 200:
                    alerts = resp.json()
                    if alerts:
                        latest_alert = alerts[0]
                        alert_id = latest_alert.get("id")
                        alert_type = latest_alert.get("type")

                        # ВИПРАВЛЕННЯ: Якщо реальна вологість в нормі (наприклад, >= 30%), не надсилаємо алерт про засуху!
                        if alert_type == 0 and current_moisture >= 30.0:
                            continue  # Пропускаємо цей застарілий алерт

                        if alert_id != app_state["last_alert_id"]:
                            app_state["last_alert_id"] = alert_id

                            if alert_type == 0:  # LowMoisture
                                msg = (
                                    f"🌵 <b>Час для поливу!</b>\n\n"
                                    f"🌱 Вазон: <b>{latest_alert.get('plantName')}</b>\n"
                                    f"💧 <i>Земля стала занадто сухою, рослині потрібна волога.</i>\n\n"
                                    f"Давайте подбаємо про неї? ✨"
                                )
                                keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                    [InlineKeyboardButton(text="🌱 Полити рослину", callback_data="water_plant")]
                                ])
                            elif alert_type == 1:  # HighMoisture
                                msg = (
                                    f"💧 <b>Увага: занадто багато вологи!</b>\n\n"
                                    f"🌱 Вазон: <b>{latest_alert.get('plantName')}</b>\n"
                                    f"⚠️ <i>Ґрунт перенасичений водою, корінцям потрібен час просохнути.</i>\n\n"
                                    f"Поки що утримайтеся від поливу 🪴"
                                )
                                keyboard = None
                            else:
                                msg = (
                                    f"⚠️ <b>Попередження від датчика!</b>\n\n"
                                    f"🌱 Вазон: <b>{latest_alert.get('plantName')}</b>\n"
                                    f"💬 <i>{latest_alert.get('message')}</i>"
                                )
                                keyboard = None

                            await bot.send_message(
                                chat_id=app_state["target_chat_id"],
                                text=msg,
                                parse_mode="HTML",
                                reply_markup=keyboard
                            )
            except Exception:
                pass

async def generate_telemetry():
    await asyncio.sleep(5)
    init_backend()

    counter = 0
    while True:
        if app_state["device_key"]:
            # Швидко знижуємо вологість для демонстрації засухи комісії
            app_state["current_moisture"] -= random.uniform(3.0, 5.0)
            if app_state["current_moisture"] < 15.0:
                app_state["current_moisture"] = 15.0

            # Імітуємо зміну дня і ночі (кожні кілька ітерацій світло падатиме до мінімуму)
            counter += 1
            is_night = (counter % 4) >= 2
            light_value = random.uniform(50.0, 100.0) if is_night else random.uniform(400.0, 600.0)
            temp_value = random.uniform(18.0, 20.0) if is_night else random.uniform(22.0, 26.0)

            payload = {
                "moisture": round(app_state["current_moisture"], 1),
                "temperature": round(temp_value, 1),
                "light": round(light_value, 1)
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

    commands = [
        BotCommand(command="start", description="🚀 Запустити бота та відкрити панель"),
        BotCommand(command="status", description="📊 Показати поточний стан рослини"),
        BotCommand(command="water", description="💧 Полити рослину в один клік")
    ]
    await bot.set_my_commands(commands)

    asyncio.create_task(generate_telemetry())
    asyncio.create_task(check_alerts_loop())
    asyncio.create_task(auto_refresh_status_loop())  # Запускаємо фонове автооновлення панелі
    print("Бот із автооновленням та меню команд запущено!")
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
