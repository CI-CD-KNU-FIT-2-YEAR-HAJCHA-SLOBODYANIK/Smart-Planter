import pytest
from unittest.mock import patch
from main import (
    get_user_state,
    init_backend_for_user,
    get_auth_data,
    format_status_message
)

def test_user_state_isolation():
    """Тест: Ізоляція станів користувачів (перевірка словника users_state)."""
    state_user1 = get_user_state(111)
    state_user2 = get_user_state(222)

    # Перевіряємо, що це два різні об'єкти в пам'яті
    assert state_user1 is not state_user2

    # Перевіряємо, що зміна одного не впливає на інший
    state_user1["active_plant_id"] = 999
    assert state_user2["active_plant_id"] is None

    # Перевіряємо, що після рефакторингу зайві поля відсутні
    assert "min_moisture" not in state_user1


def test_get_auth_data():
    """Тест: Перевірка нової DRY-функції для формування облікових даних."""
    auth_data = get_auth_data(12345)

    assert auth_data["username"] == "tg_user_12345"
    assert auth_data["password"] == "bot_secure_password_123"
    assert len(auth_data) == 2


@patch("main.requests.post")
def test_init_backend_for_user_success(mock_post):
    """Тест: Успішна авторизація / реєстрація користувача на бекенді."""
    # Імітуємо успішну відповідь від бекенда при логіні (Status 200)
    mock_post.return_value.status_code = 200
    mock_post.return_value.json.return_value = {"token": "fake_jwt_token_123"}

    init_backend_for_user(333)
    user_state = get_user_state(333)

    # Перевіряємо, що токен успішно зберігся у стан користувача
    assert user_state["jwt_token"] == "fake_jwt_token_123"
    # Перевіряємо, що запит пішов з правильними даними
    mock_post.assert_called_once_with(
        "http://backend:8080/api/v1/users/login",
        json={"username": "tg_user_333", "password": "bot_secure_password_123"}
    )


def test_format_status_message():
    """Тест: Перевірка нової DRY-функції форматування статусної панелі."""
    latest_telemetry = {
        "moisture": 45.5,
        "temperature": 23.0,
        "light": 450.0
    }

    result = format_status_message("Орхідея", "Phalaenopsis", latest_telemetry)

    # Перевіряємо, що всі ключові дані потрапили до HTML-рядка
    assert "Орхідея (Phalaenopsis)" in result
    assert "45.5%" in result
    assert "23.0°C" in result
    assert "450.0 lux" in result
    assert "ПАНЕЛЬ МОНІТОРИНГУ" in result


def test_simulation_seasons_logic():
    """Тест: Перевірка математичної логіки зміни сезонів у симуляторі телеметрії."""
    # Ізольована формула розрахунку сезону з generate_telemetry
    def get_season(counter):
        full_days = counter // 4
        return (full_days // 10) % 3

    # Норма (дні 0-9)
    assert get_season(0) == 0
    assert get_season(36) == 0  # 36 // 4 = 9 днів

    # Літо / Спека (дні 10-19)
    assert get_season(40) == 1  # 40 // 4 = 10 днів
    assert get_season(76) == 1  # 76 // 4 = 19 днів

    # Зима / Холод (дні 20-29)
    assert get_season(80) == 2  # 80 // 4 = 20 днів
    assert get_season(116) == 2 # 116 // 4 = 29 днів

    # Повернення до Норми (день 30)
    assert get_season(120) == 0 # 120 // 4 = 30 днів
