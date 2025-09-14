# 🎁 Wishera – The Ultimate Gift Wishlist Manager
A sleek, modern wishlist app built with React (frontend) and .NET (backend), featuring authentication, gift reservations, and a beautiful **Glassmorphism UI**.

## 🌟 Features
✔ **User Authentication** – Register, login, and manage your wishlist securely.  
✔ **Wishlist Management** – Add, edit, and delete gifts from your wishlist.  
✔ **Gift Reservations** – Friends can reserve gifts (without the owner seeing who reserved them).  
✔ **Wishlist Sharing** – Share your wishlist with friends via a unique link.  
✔ **Cloud Image Uploads** – Upload and display gift images using **Cloudinary**.  
✔ **Glassmorphism UI** – A stunning, modern **gradient-based design** with smooth transparency effects.  

---

## 🚀 Tech Stack
### **Frontend (React + Chakra UI)**
- **React.js** – Fast, component-based UI.
- **Chakra UI** – Beautiful UI components with Glassmorphism styling.
- **React Router** – Seamless navigation.
- **Axios** – API requests handling.
- **React Icons** – Modern icons.

### **Backend (.NET Core + MongoDB)**
- **ASP.NET Core Web API** – Robust and scalable backend.
- **MongoDB** – NoSQL database for wishlist and user data.
- **JWT Authentication** – Secure user login.
- **BCrypt** – Password hashing for security.
- **Cloudinary API** – Image hosting and management.

---

## 🌍 API Endpoints

### 🔑 Authentication
| Method | Endpoint | Description |
|--------|---------|------------|
| `POST` | `/api/Auth/register` | Register a new user |
| `POST` | `/api/Auth/login` | Login and get a JWT token |

### 🎁 Gift Management
| Method | Endpoint | Description |
|--------|---------|------------|
| `POST` | `/api/Gift` | Add a new gift |
| `GET` | `/api/Gift/wishlist` | Get all gifts in a user’s wishlist |
| `PUT` | `/api/Gift/{id}` | Update a gift |
| `DELETE` | `/api/Gift/{id}` | Delete a gift |
| `POST` | `/api/Gift/{id}/reserve` | Reserve a gift |
| `POST` | `/api/Gift/{id}/upload-image` | Upload a gift image |



## 📜 License
This project is open-source and available under the MIT License.

### ✨ Credits
💡 Developed with ❤️ by @yusufovamina

