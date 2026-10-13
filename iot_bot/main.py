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
            "plants": [],  # Список усіх рослин користувача
            "active_plant_id": None,  # ID поточно вибраної рослини
            "device_key": None,
            "target_chat_id": None,
            "status_message_id": None,
            "min_moisture": 30.0,
            "max_moisture": 80.0,
            "min_temp": 18.0,
            "max_temp": 30.0,
            "min_light": 300.0,
            "sent_alert_ids": set(),  # Множина для блокування спаму алерів
            "sim_moistures": {}  # Словник {plant_id: moisture} для стабільної симуляції
        }
    return users_state[user_id]

# FSM стани для покрокового додавання рослини з розширеними параметрами
class PlantCreationStates(StatesGroup):
    waiting_for_name = State()
    waiting_for_species = State()
    waiting_for_min_moisture = State()
    waiting_for_max_moisture = State()
    waiting_for_min_temp = State()
    waiting_for_max_temp = State()
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
        ],
        [
            InlineKeyboardButton(text="🌿 Мої рослини", callback_data="show_my_plants")
        ]
    ])

@dp.message(CommandStart())
async def cmd_start(message: Message, state: FSMContext):
    user_id = message.from_user.id
    user_state = get_user_state(user_id)
    user_state["target_chat_id"] = message.chat.id

    init_backend_for_user(user_id)

    # Отримуємо список рослин користувача з бекенда
    if user_state["jwt_token"]:
        headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
        try:
            resp = requests.get(f"{BACKEND_API_URL}/plants", headers=headers)
            if resp.status_code == 200:
                plants = resp.json()
                user_state["plants"] = plants
                if plants and not user_state["active_plant_id"]:
                    active = plants[0]
                    user_state["active_plant_id"] = active.get("id")
                    user_state["device_key"] = active.get("apiKey")
                    user_state["min_moisture"] = active.get("minMoisture", 30.0)
                    user_state["max_moisture"] = active.get("maxMoisture", 80.0)
                    user_state["min_temp"] = active.get("minTemp", 18.0)
                    user_state["max_temp"] = active.get("maxTemp", 30.0)
                    user_state["min_light"] = active.get("minLight", 300.0)
        except Exception:
            pass

    if user_state["active_plant_id"]:
        await message.answer(
            "🪴 Привіт! Ваша система моніторингу активна.\n"
            "Скористайтеся командою /status для перегляду стану, /myplants для перемикання між вазонами або /credentials для отримання даних для Swagger."
        )
        await send_or_update_pinned_status(message)
    else:
        await state.set_state(PlantCreationStates.waiting_for_name)
        await message.answer(
            "🪴 Вітаємо у системі **Smart Planter**!\n\n"
            "У вас ще немає доданих рослин. Давайте додамо вашу першу рослину.\n"
            "Введіть **назву** рослини (наприклад: <i>Моя улюблена орхідея</i>):",
            parse_mode="HTML"
        )

@dp.message(Command("newplant"))
async def cmd_new_plant(message: Message, state: FSMContext):
    user_id = message.from_user.id
    user_state = get_user_state(user_id)
    user_state["target_chat_id"] = message.chat.id

    await state.set_state(PlantCreationStates.waiting_for_name)
    await message.answer(
        "🪴 Давайте додамо **нову рослину** до вашої колекції!\n\n"
        "Введіть **назву** рослини (наприклад: <i>Фікус Бенджаміна</i>):",
        parse_mode="HTML"
    )

@dp.message(Command("credentials"))
async def cmd_credentials(message: Message):
    user_id = message.from_user.id
    username = f"tg_user_{user_id}"
    password = "bot_secure_password_123"

    await message.answer(
        f"🔑 **Ваші дані для авторизації у Swagger / Frontend:**\n\n"
        f"👤 Username: <code>{username}</code>\n"
        f"🔒 Password: <code>{password}</code>\n\n"
        f"<i>Використайте ці дані у ручці `/api/v1/users/login` у Swagger, щоб отримати JWT-токен для перегляду графіків чи тестування API!</i>",
        parse_mode="HTML"
    )

# --- Покроковий FSM для створення рослини ---
@dp.message(PlantCreationStates.waiting_for_name)
async def process_plant_name(message: Message, state: FSMContext):
    await state.update_data(plant_name=message.text)
    await state.set_state(PlantCreationStates.waiting_for_species)
    await message.answer("🌿 Тепер введіть **вид рослини** (наприклад: <i>Ficus benjamina</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_species)
async def process_plant_species(message: Message, state: FSMContext):
    await state.update_data(plant_species=message.text)
    await state.set_state(PlantCreationStates.waiting_for_min_moisture)
    await message.answer("💧 Введіть **мінімальний рівень вологості (%)** для сповіщень про посуху (наприклад: <i>30</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_min_moisture)
async def process_min_moisture(message: Message, state: FSMContext):
    try:
        val = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Введіть числове значення (наприклад, 30):")
        return
    await state.update_data(min_moisture=val)
    await state.set_state(PlantCreationStates.waiting_for_max_moisture)
    await message.answer("💧 Введіть **максимальний рівень вологості (%)** для сповіщень про перелив (наприклад: <i>80</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_max_moisture)
async def process_max_moisture(message: Message, state: FSMContext):
    try:
        val = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Введіть числове значення (наприклад, 80):")
        return
    await state.update_data(max_moisture=val)
    await state.set_state(PlantCreationStates.waiting_for_min_temp)
    await message.answer("🌡 Введіть **мінімальну температуру (°C)** (наприклад: <i>18</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_min_temp)
async def process_min_temp(message: Message, state: FSMContext):
    try:
        val = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Введіть числове значення (наприклад, 18):")
        return
    await state.update_data(min_temp=val)
    await state.set_state(PlantCreationStates.waiting_for_max_temp)
    await message.answer("🌡 Введіть **максимальну температуру (°C)** (наприклад: <i>30</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_max_temp)
async def process_max_temp(message: Message, state: FSMContext):
    try:
        val = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Введіть числове значення (наприклад, 30):")
        return
    await state.update_data(max_temp=val)
    await state.set_state(PlantCreationStates.waiting_for_min_light)
    await message.answer("☀️ Введіть **мінімальний рівень освітленості (lux)** (наприклад: <i>300</i>):", parse_mode="HTML")

@dp.message(PlantCreationStates.waiting_for_min_light)
async def process_min_light(message: Message, state: FSMContext):
    try:
        min_l = float(message.text.replace(',', '.'))
    except ValueError:
        await message.answer("⚠️ Введіть числове значення (наприклад, 300):")
        return

    data = await state.get_data()
    await state.clear()

    user_id = message.from_user.id
    user_state = get_user_state(user_id)

    if not user_state["jwt_token"]:
        init_backend_for_user(user_id)

    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
    plant_data = {
        "name": data.get("plant_name"),
        "species": data.get("plant_species"),
        "minMoisture": data.get("min_moisture"),
        "maxMoisture": data.get("max_moisture"),
        "minTemp": data.get("min_temp"),
        "maxTemp": data.get("max_temp"),
        "minLight": min_l
    }

    try:
        resp = requests.post(f"{BACKEND_API_URL}/plants", json=plant_data, headers=headers)
        if resp.status_code in [200, 201]:
            created = resp.json()
            plant_id = created.get("id")

            user_state["active_plant_id"] = plant_id
            user_state["device_key"] = created.get("apiKey")
            user_state["min_moisture"] = data.get("min_moisture")
            user_state["max_moisture"] = data.get("max_moisture")
            user_state["min_temp"] = data.get("min_temp")
            user_state["max_temp"] = data.get("max_temp")
            user_state["min_light"] = min_l

            user_state["sim_moistures"][plant_id] = 85.0

            all_plants_resp = requests.get(f"{BACKEND_API_URL}/plants", headers=headers)
            if all_plants_resp.status_code == 200:
                user_state["plants"] = all_plants_resp.json()

            await message.answer(
                f"✅ **Рослину успішно створено та додано!**\n\n"
                f"🌱 Назва: <b>{plant_data['name']}</b>\n"
                f"🌿 Вид: <i>{plant_data['species']}</i>\n"
                f"💧 Вологість: від {plant_data['minMoisture']}% до {plant_data['maxMoisture']}%\n"
                f"🌡 Температура: від {plant_data['minTemp']}°C до {plant_data['maxTemp']}°C\n"
                f"☀️ Мін. світло: {plant_data['minLight']} lux\n"
                f"🆔 ID: <code>{plant_id}</code>",
                parse_mode="HTML"
            )
            await send_or_update_pinned_status(message)
        else:
            await message.answer(f"⚠️ Помилка створення рослини на серверах бекенда: {resp.status_code}")
    except Exception as e:
        await message.answer(f"❌ Помилка мережі при створенні рослини: {e}")

# --- Меню вибору рослин (/myplants) ---
@dp.message(Command("myplants"))
async def cmd_my_plants(message: Message):
    user_id = message.from_user.id
    user_state = get_user_state(user_id)

    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
    try:
        resp = requests.get(f"{BACKEND_API_URL}/plants", headers=headers)
        if resp.status_code == 200:
            user_state["plants"] = resp.json()
    except Exception:
        pass

    plants = user_state["plants"]
    if not plants:
        await message.answer("У вас ще немає рослин. Додайте першу через /newplant")
        return

    keyboard_buttons = []
    for p in plants:
        is_active = "🟢 " if p["id"] == user_state["active_plant_id"] else ""
        keyboard_buttons.append([
            InlineKeyboardButton(
                text=f"{is_active}{p['name']} ({p['species']})",
                callback_data=f"select_plant_{p['id']}"
            )
        ])

    await message.answer("🌱 **Ваші рослини:**\nОберіть активну для керування та перегляду:", reply_markup=InlineKeyboardMarkup(inline_keyboard=keyboard_buttons), parse_mode="HTML")

@dp.callback_query(F.data.startswith("select_plant_"))
async def callback_select_plant(callback: CallbackQuery):
    plant_id = int(callback.data.split("_")[2])
    user_id = callback.from_user.id
    user_state = get_user_state(user_id)

    selected_plant = None
    for p in user_state["plants"]:
        if p["id"] == plant_id:
            selected_plant = p
            break

    if selected_plant:
        user_state["active_plant_id"] = selected_plant["id"]
        user_state["device_key"] = selected_plant.get("apiKey")
        user_state["min_moisture"] = selected_plant.get("minMoisture", 30.0)
        user_state["max_moisture"] = selected_plant.get("maxMoisture", 80.0)
        user_state["min_temp"] = selected_plant.get("minTemp", 18.0)
        user_state["max_temp"] = selected_plant.get("maxTemp", 30.0)
        user_state["min_light"] = selected_plant.get("minLight", 300.0)

        await callback.answer(f"Вибрано: {selected_plant['name']}")
        await send_or_update_pinned_status(callback, is_callback=True)
    else:
        await callback.answer("Рослину не знайдено.", show_alert=True)

@dp.callback_query(F.data == "show_my_plants")
async def callback_show_my_plants(callback: CallbackQuery):
    user_id = callback.from_user.id
    user_state = get_user_state(user_id)

    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
    try:
        resp = requests.get(f"{BACKEND_API_URL}/plants", headers=headers)
        if resp.status_code == 200:
            user_state["plants"] = resp.json()
    except Exception:
        pass

    plants = user_state["plants"]
    if not plants:
        await callback.answer("У вас ще немає рослин. Додайте першу через /newplant", show_alert=True)
        return

    keyboard_buttons = []
    for p in plants:
        is_active = "🟢 " if p["id"] == user_state["active_plant_id"] else ""
        keyboard_buttons.append([
            InlineKeyboardButton(
                text=f"{is_active}{p['name']} ({p['species']})",
                callback_data=f"select_plant_{p['id']}"
            )
        ])

    keyboard_buttons.append([
        InlineKeyboardButton(text="⬅️ Назад до статусу", callback_data="refresh_status")
    ])

    await callback.answer()
    try:
        await callback.message.edit_text(
            "🌱 **Ваші рослини:**\nОберіть активну для керування та перегляду:",
            reply_markup=InlineKeyboardMarkup(inline_keyboard=keyboard_buttons),
            parse_mode="HTML"
        )
        user_state["status_message_id"] = callback.message.message_id
    except TelegramBadRequest:
        pass

# --- Обробка закриття тривоги кнопкою ---
@dp.callback_query(F.data.startswith("resolve_alert_"))
async def callback_resolve_alert(callback: CallbackQuery):
    alert_id = int(callback.data.split("_")[2])
    user_id = callback.from_user.id
    user_state = get_user_state(user_id)

    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
    try:
        resp = requests.put(f"{BACKEND_API_URL}/alerts/{alert_id}/resolve", headers=headers)
        if resp.status_code in [200, 204]:
            await callback.answer("✅ Тривогу успішно закрито!", show_alert=True)
            try:
                # Оновлюємо текст повідомлення про тривогу, щоб показати, що вона вирішена
                original_text = callback.message.text or callback.message.caption or "Попередження"
                await callback.message.edit_text(
                    f"✅ <b>[ВИРІШЕНО]</b>\n\n{original_text}",
                    parse_mode="HTML",
                    reply_markup=None
                )
            except TelegramBadRequest:
                pass
        else:
            await callback.answer(f"⚠️ Помилка закриття: {resp.status_code}", show_alert=True)
    except Exception:
        await callback.answer("❌ Помилка мережі при закритті тривоги.", show_alert=True)

async def send_or_update_pinned_status(message_or_callback: Message | CallbackQuery, is_callback: bool = False):
    user_id = message_or_callback.from_user.id
    user_state = get_user_state(user_id)

    if not user_state["jwt_token"] or not user_state["active_plant_id"]:
        text = "⏳ Не вибрано активну рослину. Скористайтеся /myplants або /newplant."
        if is_callback: await message_or_callback.message.edit_text(text)
        else: await message_or_callback.answer(text)
        return

    # Шукаємо назву та вид активної рослини для красивого виводу
    active_plant_name = "Вазон"
    active_plant_species = ""
    for p in user_state.get("plants", []):
        if p.get("id") == user_state["active_plant_id"]:
            active_plant_name = p.get("name", "Вазон")
            active_plant_species = p.get("species", "")
            break

    history_url = f"{BACKEND_API_URL}/telemetry/{user_state['active_plant_id']}/history?limit=1"
    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}

    try:
        response = requests.get(history_url, headers=headers)
        if response.status_code == 200:
            data = response.json()
            if data and len(data) > 0:
                latest = data[0]
                species_str = f" ({active_plant_species})" if active_plant_species else ""
                msg = (
                    f"📊 <b>ПАНЕЛЬ МОНІТОРИНГУ РОСЛИНИ (Live)</b>\n\n"
                    f"🌱 Вазон: <b>{active_plant_name}{species_str}</b>\n"
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

    if not user_state["jwt_token"] or not user_state["active_plant_id"]:
        if isinstance(message_or_callback, CallbackQuery):
            await message_or_callback.answer("⏳ Немає активної рослини.", show_alert=True)
        else:
            await message_or_callback.answer("⏳ Немає активної рослини.")
        return

    plant_id = user_state["active_plant_id"]
    water_url = f"{BACKEND_API_URL}/plants/{plant_id}/water"
    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}

    try:
        resp = requests.post(water_url, headers=headers)
        if resp.status_code == 200:
            if plant_id in user_state["sim_moistures"]:
                user_state["sim_moistures"][plant_id] = 85.0

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
            if user_state["jwt_token"] and user_state["active_plant_id"] and user_state["target_chat_id"] and user_state["status_message_id"]:
                history_url = f"{BACKEND_API_URL}/telemetry/{user_state['active_plant_id']}/history?limit=1"
                headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
                try:
                    response = requests.get(history_url, headers=headers)
                    if response.status_code == 200:
                        data = response.json()
                        if data and len(data) > 0:
                            latest = data[0]

                            active_plant_name = "Вазон"
                            active_plant_species = ""
                            for p in user_state.get("plants", []):
                                if p.get("id") == user_state["active_plant_id"]:
                                    active_plant_name = p.get("name", "Вазон")
                                    active_plant_species = p.get("species", "")
                                    break
                            species_str = f" ({active_plant_species})" if active_plant_species else ""

                            msg = (
                                f"📊 <b>ПАНЕЛЬ МОНІТОРИНГУ РОСЛИНИ (Live)</b>\n\n"
                                f"🌱 Вазон: <b>{active_plant_name}{species_str}</b>\n"
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
    """Фонова перевірка активних алертів без спаму та з можливістю закриття"""
    while True:
        await asyncio.sleep(15)
        for user_id, user_state in users_state.items():
            if user_state["jwt_token"] and user_state["target_chat_id"]:
                try:
                    headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
                    resp = requests.get(f"{BACKEND_API_URL}/alerts/active", headers=headers)
                    if resp.status_code == 200:
                        alerts = resp.json()
                        for latest_alert in alerts:
                            alert_id = latest_alert.get("id")
                            if not alert_id:
                                continue

                            if alert_id not in user_state["sent_alert_ids"]:
                                user_state["sent_alert_ids"].add(alert_id)

                                alert_type = latest_alert.get("type")
                                plant_name = latest_alert.get('plantName', 'Вазон')

                                # Формуємо текст залежно від типу алерта
                                if alert_type == 0:
                                    msg = (
                                        f"🌵 <b>Час для поливу! (Низька вологість)</b>\n\n"
                                        f"🌱 Вазон: <b>{plant_name}</b>\n"
                                        f"💧 <i>Земля стала занадто сухою. Рослині потрібна волога!</i>"
                                    )
                                    keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                        [InlineKeyboardButton(text="🌱 Полити рослину", callback_data="water_plant")],
                                        [InlineKeyboardButton(text="✅ Закрити тривогу", callback_data=f"resolve_alert_{alert_id}")]
                                    ])
                                elif alert_type == 1:
                                    msg = (
                                        f"💧 <b>Увага: Занадто багато вологи (Перелив)!</b>\n\n"
                                        f"🌱 Вазон: <b>{plant_name}</b>\n"
                                        f"⚠️ <i>Ґрунт перенасичений водою. Утримайтеся від поливу!</i>"
                                    )
                                    keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                        [InlineKeyboardButton(text="✅ Закрити тривогу", callback_data=f"resolve_alert_{alert_id}")]
                                    ])
                                elif alert_type == 2:
                                    msg = (
                                        f"🌡 <b>Увага: Занадто низька температура!</b>\n\n"
                                        f"🌱 Вазон: <b>{plant_name}</b>\n"
                                        f"❄️ <i>Температура впала нижче допустимої норми.</i>"
                                    )
                                    keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                        [InlineKeyboardButton(text="✅ Закрити тривогу", callback_data=f"resolve_alert_{alert_id}")]
                                    ])
                                elif alert_type == 3:
                                    msg = (
                                        f"🔥 <b>Увага: Занадто висока температура!</b>\n\n"
                                        f"🌱 Вазон: <b>{plant_name}</b>\n"
                                        f"☀️ <i>Температура перевищила норму. Захистіть рослину від спеки!</i>"
                                    )
                                    keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                        [InlineKeyboardButton(text="✅ Закрити тривогу", callback_data=f"resolve_alert_{alert_id}")]
                                    ])
                                else:
                                    msg = (
                                        f"⚠️ <b>Попередження від датчика!</b>\n\n"
                                        f"🌱 Вазон: <b>{plant_name}</b>\n"
                                        f"💬 <i>{latest_alert.get('message')}</i>"
                                    )
                                    keyboard = InlineKeyboardMarkup(inline_keyboard=[
                                        [InlineKeyboardButton(text="✅ Закрити тривогу", callback_data=f"resolve_alert_{alert_id}")]
                                    ])

                                await bot.send_message(
                                    chat_id=user_state["target_chat_id"],
                                    text=msg,
                                    parse_mode="HTML",
                                    reply_markup=keyboard
                                )
                except Exception:
                    pass

async def generate_telemetry():
    """Симулює та надсилає телеметрію для ВСІХ рослин кожного користувача паралельно без стрибків"""
    await asyncio.sleep(5)
    counter = 0
    while True:
        counter += 1
        is_night = (counter % 4) >= 2

        for user_id, user_state in users_state.items():
            if user_state["jwt_token"]:
                headers = {"Authorization": f"Bearer {user_state['jwt_token']}"}
                try:
                    resp = requests.get(f"{BACKEND_API_URL}/plants", headers=headers)
                    if resp.status_code == 200:
                        user_state["plants"] = resp.json()
                except Exception:
                    pass

            plants = user_state.get("plants", [])
            for plant in plants:
                device_key = plant.get("apiKey")
                plant_id = plant.get("id")

                if not device_key or not plant_id:
                    continue

                if plant_id not in user_state["sim_moistures"]:
                    user_state["sim_moistures"][plant_id] = 85.0

                user_state["sim_moistures"][plant_id] -= random.uniform(3.0, 5.0)
                if user_state["sim_moistures"][plant_id] < 15.0:
                    user_state["sim_moistures"][plant_id] = 15.0

                light_value = random.uniform(50.0, 100.0) if is_night else random.uniform(400.0, 600.0)
                temp_value = random.uniform(18.0, 20.0) if is_night else random.uniform(22.0, 26.0)

                payload = {
                    "moisture": round(user_state["sim_moistures"][plant_id], 1),
                    "temperature": round(temp_value, 1),
                    "light": round(light_value, 1)
                }
                headers = {"X-Device-Key": device_key}
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
        BotCommand(command="newplant", description="➕ Додати нову рослину"),
        BotCommand(command="myplants", description="🌿 Мої рослини"),
        BotCommand(command="credentials", description="🔑 Дані для Swagger / API")
    ]
    await bot.set_my_commands(commands)

    asyncio.create_task(generate_telemetry())
    asyncio.create_task(check_alerts_loop())
    asyncio.create_task(auto_refresh_status_loop())
    print("Бот успішно запущено з покращеним виводом та закриттям тривог!")
    await dp.start_polling(bot)

if __name__ == "__main__":
    asyncio.run(main())
