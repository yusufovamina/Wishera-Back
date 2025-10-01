# 🚀 Инструкции по запуску Wishera Web App

## 📋 Предварительные требования

1. **.NET 9.0 SDK** - Установите последнюю версию .NET 9.0
2. **MongoDB** - Убедитесь, что MongoDB доступен (используется облачная версия)
3. **RabbitMQ** - Для межсервисного взаимодействия (опционально для локальной разработки)

## 🔧 Настройка проекта

### 1. Восстановление зависимостей
```bash
dotnet restore
```

### 2. Сборка проекта
```bash
dotnet build
```

## 🚀 Запуск сервисов

### Вариант 1: Автоматический запуск всех сервисов

#### Windows (Batch файл)
```bash
start-services.bat
```

#### Windows (PowerShell)
```powershell
.\start-services.ps1
```

### Вариант 2: Ручной запуск каждого сервиса

Откройте отдельные терминалы для каждого сервиса:

#### 1. Auth Service (Порт 5219)
```bash
cd auth-service
dotnet run --urls http://localhost:5219
```

#### 2. User Service (Порт 5220)
```bash
cd user-service
dotnet run --urls http://localhost:5220
```

#### 3. Gift Wishlist Service (Порт 5221)
```bash
cd gift-wishlist-service
dotnet run --urls http://localhost:5221
```

#### 4. Chat Service (Порт 5162)
```bash
cd chat-server-side/PresentationLayer
dotnet run --urls http://localhost:5162
```

#### 5. Main Wishera App (Порт 5155)
```bash
cd WishlistApp
dotnet run --urls http://localhost:5155
```

## 🔐 Внешняя аутентификация (Google и Twitter)

### 1. Создание приложений у провайдеров
- Google: создайте проект в Google Cloud Console → OAuth consent screen → OAuth client (Web). Redirect URI: `http://localhost:5219/signin-google`
- Twitter: создайте приложение в Twitter Developer Portal (OAuth 2.0). Redirect URI: `http://localhost:5219/signin-twitter`

### 2. Переменные окружения/настройки
Добавьте в `auth-service/appsettings.json` или как переменные окружения:
```json
"Authentication": {
  "Google": {
    "ClientId": "GOOGLE_CLIENT_ID",
    "ClientSecret": "GOOGLE_CLIENT_SECRET"
  },
  "Twitter": {
    "ClientId": "TWITTER_CLIENT_ID",
    "ClientSecret": "TWITTER_CLIENT_SECRET"
  }
}
```

### 3. Тестирование локально
- Откройте: `http://localhost:5219/api/ExternalAuth/login/Google`
- Или: `http://localhost:5219/api/ExternalAuth/login/Twitter`
- После успешного входа будет вызван колбэк `/api/ExternalAuth/callback?provider=Google|Twitter`.

## 🌐 URL адреса сервисов

| Сервис | URL | Описание |
|--------|-----|----------|
| Auth Service | http://localhost:5219 | Аутентификация и авторизация |
| User Service | http://localhost:5220 | Управление пользователями |
| Gift Wishlist Service | http://localhost:5221 | Управление подарками и списками желаний |
| Chat Service | http://localhost:5162 | Чат функциональность |
| Main Wishera App | http://localhost:5155 | Основное приложение (API Gateway) |

## 🔍 Проверка работоспособности

После запуска всех сервисов:

1. Откройте http://localhost:5155/swagger для доступа к основному API
2. Проверьте, что все сервисы отвечают на своих портах
3. Убедитесь, что MongoDB подключение работает

## 🛠️ Устранение неполадок

### Ошибка "Port already in use"
- Убедитесь, что порты не заняты другими приложениями
- Используйте команду `netstat -ano | findstr :PORT` для проверки занятых портов

### Ошибка MongoDB подключения
- Проверьте интернет соединение
- Убедитесь, что MongoDB Atlas доступен
- Проверьте правильность строки подключения в appsettings.json

### Ошибка сборки
- Выполните `dotnet clean` и затем `dotnet restore`
- Убедитесь, что установлена правильная версия .NET SDK

## 📝 Примечания

- Все сервисы используют MongoDB Atlas (облачная база данных)
- JWT ключи настроены для всех сервисов
- Cloudinary настроен для загрузки изображений
- RabbitMQ используется для межсервисного взаимодействия

## 🆘 Поддержка

При возникновении проблем:
1. Проверьте логи в консоли каждого сервиса
2. Убедитесь, что все зависимости установлены
3. Проверьте конфигурацию в appsettings.json файлах
