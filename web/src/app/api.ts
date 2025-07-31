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

export async function forgotPassword(email: string) {
  const response = await axios.post(`${API_URL}/Auth/forgot-password`, { email });
  return response.data;
}

export async function resetPassword(token: string, newPassword: string) {
  const response = await axios.post(`${API_URL}/Auth/reset-password`, { token, newPassword });
  return response.data;
}

export async function checkEmailAvailability(email: string) {
  const response = await axios.get(`${API_URL}/Auth/check-email?email=${encodeURIComponent(email)}`);
  return response.data;
}

export async function checkUsernameAvailability(username: string) {
  const response = await axios.get(`${API_URL}/Auth/check-username?username=${encodeURIComponent(username)}`);
  return response.data;
} 