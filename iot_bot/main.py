import asyncio
import os
import random
import requests
from aiogram import Bot, Dispatcher, F
from aiogram.filters import CommandStart, Command
from aiogram.types import Message, InlineKeyboardMarkup, InlineKeyboardButton, CallbackQuery, BotCommand
from aiogram.fsm.state import State, StatesGroup
from aiogram.fsm.context import FSMContext
from aiogram.exceptions import TelegramBadRequest

BACKEND_API_URL = os.getenv("BACKEND_API_URL", "http://backend:8080/api/v1").rstrip('/')
BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")

bot = Bot(token=BOT_TOKEN)
dp = Dispatcher()

# Сховище станів для кожного користувача окремо за його Telegram ID
users_state = {}

def get_user_state(user_id: int):
    if user_id not in users_state:
        users_state[user_id] = {
            "jwt_token": None,
            "device_key": None,
            "plant_id": None,
            "current_moisture": 85.0,
            "last_alert_id": None,
            "target_chat_id": None,
            "status_message_id": None,
            "min_moisture": 30.0,
            "min_light": 300.0
        }
    return users_state[user_id]

# FSM стани для покрокового додавання рослини
class PlantCreationStates(StatesGroup):
    waiting_for_name = State()
    waiting_for_species = State()
    waiting_for_min_moisture = State()
    waiting_for_min_light = State()

def init_backend_for_user(telegram_id: int):
    """Авторизація або реєстрація унікального користувача на основі його Telegram ID"""
    user_state = get_user_state(telegram_id)
    username = f"tg_user_{telegram_id}"
    auth_data = {"username": username, "password": "bot_secure_password_123"}
    try:
        print(f"Спроба авторизації користувача {username} на бекенді...")
        resp = requests.post(f"{BACKEND_API_URL}/users/login", json=auth_data)

        if resp.status_code == 401:
            print(f"Користувача {username} не знайдено. Реєструємо нового...")
            resp = requests.post(f"{BACKEND_API_URL}/users/register", json=auth_data)

        if resp.status_code in [200, 201]:
            data = resp.json()
            user_state["jwt_token"] = data.get("token")
            print(f"✅ Успішна авторизація для {username}.")
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
async def cmd_start(message: Message, state: FSMContext):
    user_id = message.from_user.id
    user_state = get_user_state(user_id)
    user_state["target_chat_id"] = message.chat.id

    # Ініціалізуємо унікального користувача за його Telegram ID
    init_backend_for_user(user_id)

    # Перевіримо, чи є у цього користувача вже створена рослина на бекенді
    if user_state["jwt_token"]:
        headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
        try:
            resp = requests.get(f"{BACKEND_API_URL}/plants", headers=headers)
            if resp.status_code == 200:
                plants = resp.json()
                if plants:
                    user_state["device_key"] = plants[0].get("apiKey")
                    user_state["plant_id"] = plants[0].get("id")
                    user_state["min_moisture"] = plants[0].get("minMoisture", 30.0)
                    user_state["min_light"] = plants[0].get("minLight", 300.0)
        except Exception:
            pass

    if user_state["plant_id"]:
        await message.answer(
            "🪴 Привіт! Ваша рослина вже налаштована.\n"
            "Скористайтеся меню команд або командою /status для перегляду стану.\n"
            "Якщо хочете додати нову рослину (замість поточної) — скористайтеся /newplant."
        )
        await send_or_update_pinned_status(message)
    else:
        # Якщо рослини немає — запускаємо покрокове додавання
        await state.set_state(PlantCreationStates.waiting_for_name)
        await message.answer(
            "🪴 Вітаємо у системі **Smart Planter**!\n\n"
            "У вас ще немає доданих рослин. Давайте додамо вашу першу рослину.\n"
            "Введіть **назву** рослини (наприклад: <i>Моя улюблена орхідея</i>):",
            parse_mode="HTML"
        )

# --- Додавання нової рослини замість поточної ---
@dp.message(Command("newplant"))
async def cmd_new_plant(message: Message, state: FSMContext):
    user_id = message.from_user.id
    user_state = get_user_state(user_id)
    user_state["target_chat_id"] = message.chat.id

    # Скидаємо старий ідентифікатор рослини в сесії користувача
    user_state["plant_id"] = None
    user_state["device_key"] = None

    await state.set_state(PlantCreationStates.waiting_for_name)
    await message.answer(
        "🪴 Давайте налаштуємо **нову рослину**!\n\n"
        "Введіть **назву** нової рослини (наприклад: <i>Фікус Бенджаміна</i>):",
        parse_mode="HTML"
    )

# --- FSM Додавання рослини ---
@dp.message(PlantCreationStates.waiting_for_name)
async def process_plant_name(message: Message, state: FSMContext):
    await state.update_data(plant_name=message.text)
    await state.set_state(PlantCreationStates.waiting_for_species)
    await message.answer("🌿 Тепер введіть **вид рослини** (наприклад: <i>Phalaenopsis</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_species)
async def process_plant_species(message: Message, state: FSMContext):
    await state.update_data(plant_species=message.text)
    await state.set_state(PlantCreationStates.waiting_for_min_moisture)
    await message.answer("💧 Введіть **мінімальний рівень вологості (%)**, нижче якого потрібно надсилати сповіщення про засуху (наприклад: <i>35</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_min_moisture)
async def process_min_moisture(message: Message, state: FSMContext):
    try:
        min_m = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Будь ласка, введіть числове значення для вологості (наприклад, 30 або 35.5):")
        return

    await state.update_data(min_moisture=min_m)
    await state.set_state(PlantCreationStates.waiting_for_min_light)
    await message.answer("☀️ Введіть **мінімальний рівень освітленості (lux)** для сповіщень (наприклад: <i>250</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_min_light)
async def process_min_light(message: Message, state: FSMContext):
    try:
        min_l = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Будь ласка, введіть числове значення для освітленості (наприклад, 300):")
        return

    data = await state.get_data()
    plant_name = data.get("plant_name")
    plant_species = data.get("plant_species")
    min_moisture = data.get("min_moisture")
    min_light = min_l
    await state.clear()

    user_id = message.from_user.id
    user_state = get_user_state(user_id)

    if not user_state["jwt_token"]:
        init_backend_for_user(user_id)

    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
    plant_data = {
        "name": plant_name,
        "species": plant_species,
        "minMoisture": min_moisture,
        "maxMoisture": 80.0,
        "minTemp": 18.0,
        "maxTemp": 30.0,
        "minLight": min_light
    }

    try:
        resp = requests.post(f"{BACKEND_API_URL}/plants", json=plant_data, headers=headers)
        if resp.status_code in [200, 201]:
            created = resp.json()
            user_state["plant_id"] = created.get("id")
            user_state["device_key"] = created.get("apiKey")
            user_state["min_moisture"] = min_moisture
            user_state["min_light"] = min_light
            user_state["current_moisture"] = 85.0

            await message.answer(
                f"✅ **Рослину успішно створено та збережено!**\n\n"
                f"🌱 Назва: <b>{plant_name}</b>\n"
                f"🌿 Вид: <i>{plant_species}</i>\n"
                f"💧 Мін. вологість для сповіщень: <b>{min_moisture}%</b>\n"
                f"☀️ Мін. світло для сповіщень: <b>{min_light} lux</b>\n"
                f"🆔 Plant ID: <code>{user_state['plant_id']}</code>\n\n"
                f"Розпочинаємо моніторинг вашого вазона! 🚀",
                parse_mode="HTML"
            )
            await send_or_update_pinned_status(message)
        else:
            await message.answer(f"⚠️ Помилка створення рослини на серверах бекенда: {resp.status_code}")
    except Exception as e:
        await message.answer(f"❌ Помилка мережі при створенні рослини: {e}")

async def send_or_update_pinned_status(message_or_callback: Message | CallbackQuery, is_callback: bool = False):
    user_id = message_or_callback.from_user.id
    user_state = get_user_state(user_id)

    if not user_state["jwt_token"] or not user_state["plant_id"]:
        text = "⏳ Рослину ще не наведено. Будь ласка, запустіть /start та додайте свій вазон."
        if is_callback: await message_or_callback.message.edit_text(text)
        else: await message_or_callback.answer(text)
        return

    history_url = f"{BACKEND_API_URL}/telemetry/{user_state['plant_id']}/history?limit=1"
    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}

    try:
        response = requests.get(history_url, headers=headers)
        if response.status_code == 200:
            data = response.json()
            if data and len(data) > 0:
                latest = data[0]
                msg = (
                    f"📊 <b>ПАНЕЛЬ МОНІТОРИНГУ РОСЛИНИ (Live)</b>\n\n"
                    f"🌱 Вазон ID: <b>#{user_state['plant_id']}</b>\n"
                    f"💧 Вологість ґрунту: <b>{latest.get('moisture')}%</b>\n"
                    f"🌡 Температура: <b>{latest.get('temperature')}°C</b>\n"
                    f"☀️ Освітленість: <b>{latest.get('light')} lux</b>\n\n"
                    f"<i>🔄 Дані оновлюються автоматично в реальному часі.</i>"
                )

                if is_callback:
                    try:
                        await message_or_callback.message.edit_text(msg, parse_mode="HTML", reply_markup=get_inline_status_keyboard())
                        user_state["status_message_id"] = message_or_callback.message.message_id
                    except TelegramBadRequest:
                        pass
                else:
                    sent_msg = await message_or_callback.answer(msg, parse_mode="HTML", reply_markup=get_inline_status_keyboard())
                    user_state["status_message_id"] = sent_msg.message_id
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
    user_state = get_user_state(message.from_user.id)
    user_state["target_chat_id"] = message.chat.id
    await send_or_update_pinned_status(message)

@dp.message(Command("water"))
async def cmd_water(message: Message):
    user_state = get_user_state(message.from_user.id)
    user_state["target_chat_id"] = message.chat.id
    await trigger_watering(message)

async def trigger_watering(message_or_callback):
    user_id = message_or_callback.from_user.id
    user_state = get_user_state(user_id)

    if not user_state["jwt_token"] or not user_state["plant_id"]:
        if isinstance(message_or_callback, CallbackQuery):
            await message_or_callback.answer("⏳ Немає активної рослини.", show_alert=True)
        else:
            await message_or_callback.answer("⏳ Немає активної рослини.")
        return

    water_url = f"{BACKEND_API_URL}/plants/{user_state['plant_id']}/water"
    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}

    try:
        resp = requests.post(water_url, headers=headers)
        if resp.status_code == 200:
            user_state["current_moisture"] = 85.0
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
    while True:
        await asyncio.sleep(12)
        for user_id, user_state in users_state.items():
            if user_state["jwt_token"] and user_state["plant_id"] and user_state["target_chat_id"] and user_state["status_message_id"]:
                history_url = f"{BACKEND_API_URL}/telemetry/{user_state['plant_id']}/history?limit=1"
                headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
                try:
                    response = requests.get(history_url, headers=headers)
                    if response.status_code == 200:
                        data = response.json()
                        if data and len(data) > 0:
                            latest = data[0]
                            msg = (
                                f"📊 <b>ПАНЕЛЬ МОНІТОРИНГУ РОСЛИНИ (Live)</b>\n\n"
                                f"🌱 Вазон ID: <b>#{user_state['plant_id']}</b>\n"
                                f"💧 Вологість ґрунту: <b>{latest.get('moisture')}%</b>\n"
                                f"🌡 Температура: <b>{latest.get('temperature')}°C</b>\n"
                                f"☀️ Освітленість: <b>{latest.get('light')} lux</b>\n\n"
                                f"<i>🔄 Дані оновлюються автоматично в реальному часі.</i>"
                            )
                            await bot.edit_message_text(
                                chat_id=user_state["target_chat_id"],
                                message_id=user_state["status_message_id"],
                                text=msg,
                                parse_mode="HTML",
                                reply_markup=get_inline_status_keyboard()
                            )
                except Exception:
                    pass

async def check_alerts_loop():
    while True:
        await asyncio.sleep(15)
        for user_id, user_state in users_state.items():
            if user_state["jwt_token"] and user_state["target_chat_id"] and user_state["plant_id"]:
                try:
                    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
                    history_resp = requests.get(f"{BACKEND_API_URL}/telemetry/{user_state['plant_id']}/history?limit=1", headers=headers)
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

                            if alert_type == 0 and current_moisture >= user_state["min_moisture"]:
                                continue

                            if alert_id != user_state["last_alert_id"]:
                                user_state["last_alert_id"] = alert_id

                                if alert_type == 0:
                                    msg = (
                                        f"🌵 <b>Час для поливу!</b>\n\n"
                                        f"🌱 Вазон: <b>{latest_alert.get('plantName')}</b>\n"
                                        f"💧 <i>Земля стала занадто сухою (нижче {user_state['min_moisture']}%), рослині потрібна волога.</i>\n\n"
                                        f"Давайте подбаємо про неї? ✨"
                                    )
                                    keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                        [InlineKeyboardButton(text="🌱 Полити рослину", callback_data="water_plant")]
                                    ])
                                elif alert_type == 1:
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
                                    chat_id=user_state["target_chat_id"],
                                    text=msg,
                                    parse_mode="HTML",
                                    reply_markup=keyboard
                                )
                except Exception:
                    pass

async def generate_telemetry():
    await asyncio.sleep(5)
    counter = 0
    while True:
        for user_id, user_state in users_state.items():
            if user_state["device_key"] and user_state["plant_id"]:
                user_state["current_moisture"] -= random.uniform(3.0, 5.0)
                if user_state["current_moisture"] < 15.0:
                    user_state["current_moisture"] = 15.0

                counter += 1
                is_night = (counter % 4) >= 2
                light_value = random.uniform(50.0, 100.0) if is_night else random.uniform(400.0, 600.0)
                temp_value = random.uniform(18.0, 20.0) if is_night else random.uniform(22.0, 26.0)

                payload = {
                    "moisture": round(user_state["current_moisture"], 1),
                    "temperature": round(temp_value, 1),
                    "light": round(light_value, 1)
                }
                headers = {"X-Device-Key": user_state["device_key"]}
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
        BotCommand(command="start", description="🚀 Запустити бота"),
        BotCommand(command="status", description="📊 Показати поточний стан"),
        BotCommand(command="water", description="💧 Полити рослину"),
        BotCommand(command="newplant", description="➕ Додати нову рослину")
    ]
    await bot.set_my_commands(commands)

    asyncio.create_task(generate_telemetry())
    asyncio.create_task(check_alerts_loop())
    asyncio.create_task(auto_refresh_status_loop())
    print("Бот з підтримкою багатьох користувачів та командою /newplant запущено!")
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
