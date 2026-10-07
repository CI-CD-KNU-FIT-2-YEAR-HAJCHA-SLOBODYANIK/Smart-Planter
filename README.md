# Smart-Planter

Система моніторингу стану рослин на базі ASP.NET Core та PostgreSQL.

## Системні вимоги

- Docker Desktop (з підтримкою `docker compose`)
- Git

---

## Швидкий запуск

1. **Клонування репозиторію та перехід у папку:**

   git clone https://github.com/CI-CD-KNU-FIT-2-YEAR-HAJCHA-SLOBODYANIK/Smart-Planter.git smart-planter
   cd smart-planter

2. **Запуск сервісів (Backend + PostgreSQL):**

   docker compose up -d --build

3. **Доступ до сервісів:**

   Документація API (Scalar): http://localhost:8000/scalar/v1
   Специфікація OpenAPI: http://localhost:8000/openapi/v1.json
   База даних: localhost:5432
   БД: smart_planter
   Користувач: postgres
   Пароль: postgres_secure_pass

4. **Зупинка:**

   **Зупинити роботу контейнерів:**

   docker compose down

   **Зупинити та видалити збережені дані бази (скидання стану):**

   docker compose down -v

## Frontend

0. **Обов'язково переконатись, що бекенд запущений, тому що без нього фронтенд не зможе працювати**

1. **Завантажити пакети:**

   npm install

2. **Переключити папку на фронтенд:**

   cd frontend

3. **Запустити фронтенд сторінку: (Щоб перейти на сторінку, наведіться на посилання локалхост мишкою і нажміть CTRL + L Mouse (ліва кнопка миші))**

   npm run dev

4. **Зупинити роботу сторінки в терміналі:**

   CTRL + C

5. **Офлайн**

   **Спочатку запустити дві команди водночас або одна за одною**

   npm run build

   npx vite preview

   **Потім, щоб перевірити офлайн режим**

   Зайти в "Inspect" (через праву кнопку миші), де знаходяться dev tools

   Нажати вкладку "Network" і змінити No throttling -> Offline

   Якщо сторінка не чисто біла, значить все працює
