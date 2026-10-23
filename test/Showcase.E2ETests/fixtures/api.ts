import type { APIRequestContext, TestInfo } from '@playwright/test';
import { apiBaseUrl } from '../ports';

// The token the apps send: not a valid JWT, so the API's demo authentication gives it the demo identity (every scope)
export const demoToken = 'demo';
export const demoHeaders = { Authorization: `Bearer ${demoToken}` };

// The API is in memory and shared by every project of a run: a test names what it creates uniquely, and asserts on
// its own data - never on a global count
export const unique = (label: string, testInfo: TestInfo) =>
  `${label} ${testInfo.project.name} ${Date.now().toString(36)}${Math.floor(Math.random() * 1e4).toString(36)}`;

// A surname no other test (or project) uses: the Showcase refuses a second user with the same first and last name
export const uniqueSurname = (base: string) =>
  `${base}-${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;

// Reads the API directly (as the demo identity) - what a test checks the app's outcome against
export async function apiGet<T>(request: APIRequestContext, path: string): Promise<{ status: number; body: T }> {
  const response = await request.get(`${apiBaseUrl}${path}`, { headers: demoHeaders });
  const text = await response.text();
  return { status: response.status(), body: (text ? JSON.parse(text) : undefined) as T };
}
