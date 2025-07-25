import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5155/api';

export async function login(email: string, password: string) {
  const response = await axios.post(`${API_URL}/Auth/login`, { email, password });
  return response.data;
}

export async function register(username: string, email: string, password: string) {
  const response = await axios.post(`${API_URL}/Auth/register`, { username, email, password });
  return response.data;
} 