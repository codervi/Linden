// Thin wrapper around fetch for the JSON API.

export class ApiError extends Error {
  constructor(status, problem) {
    super(messageFrom(status, problem));
    this.status = status;
  }
}

function messageFrom(status, problem) {
  if (problem?.errors) {
    // ASP.NET validation problem: { errors: { Field: ["message"] } }
    const first = Object.values(problem.errors).flat()[0];
    if (first) return first;
  }
  if (problem?.detail) return problem.detail;
  if (problem?.title) return problem.title;
  if (status === 429) return "Too many attempts. Wait a minute and try again.";
  return "Something went wrong. Please try again.";
}

let onUnauthorized = () => {};
export function setUnauthorizedHandler(fn) { onUnauthorized = fn; }

export async function api(method, url, body) {
  const options = {
    method,
    credentials: "same-origin",
    // The server rejects changes that don't send JSON or this header (CSRF guard).
    headers: { "X-Requested-With": "linden" },
  };
  if (body !== undefined) {
    options.headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch("/api" + url, options);
  } catch {
    throw new ApiError(0, { detail: "Can't reach the server. Check your connection." });
  }

  if (response.status === 204) return null;

  const text = await response.text();
  let data = null;
  if (text) {
    try { data = JSON.parse(text); } catch { /* not JSON */ }
  }

  if (!response.ok) {
    const isAuthCall = url.startsWith("/auth/");
    if (response.status === 401 && !isAuthCall) onUnauthorized();
    throw new ApiError(response.status, data);
  }
  return data;
}

export const get = (url) => api("GET", url);
export const post = (url, body) => api("POST", url, body);
export const put = (url, body) => api("PUT", url, body);
export const patch = (url, body) => api("PATCH", url, body);
export const del = (url) => api("DELETE", url);
