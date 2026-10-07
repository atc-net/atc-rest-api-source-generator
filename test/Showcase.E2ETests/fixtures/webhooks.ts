import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext } from '@playwright/test';
import { API_PORT } from '../ports';

// The API's webhook endpoints (the specification's webhooks section, mapped under /webhooks): the test plays the
// external system that calls them. Each handler broadcasts the event to the SignalR clients of its topic.
const webhooksUrl = `http://localhost:${API_PORT}/webhooks`;

async function post(request: APIRequestContext, path: string, payload: object) {
  const response = await request.post(`${webhooksUrl}/${path}`, { data: payload });
  expect(response.status(), `POST /webhooks/${path}`).toBe(200);
}

export async function sendSystemNotification(request: APIRequestContext, message: string) {
  await post(request, 'system-notification', {
    id: randomUUID(),
    type: 'System',
    severity: 'Info',
    message,
    timestamp: new Date().toISOString(),
  });
}

export async function sendUserActivity(request: APIRequestContext, userId: string) {
  await post(request, 'user-activity', {
    id: randomUUID(),
    userId,
    action: 'ProfileUpdate',
    timestamp: new Date().toISOString(),
    details: 'Sent by the end-to-end tests',
  });
}

export async function sendDataChange(request: APIRequestContext, entityId: string) {
  await post(request, 'data-change', {
    id: randomUUID(),
    entityType: 'Account',
    entityId,
    operation: 'Updated',
    timestamp: new Date().toISOString(),
    previousValue: 'old-value',
    newValue: 'new-value',
  });
}
