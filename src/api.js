// File: src/api.js
/* ------------------------------------------------------------------
   Tiny fetch wrapper with debug logging:
     • Prepends API root
     • Sends/updates JWT automatically
     • Gracefully handles 401 by logging the user out
-------------------------------------------------------------------*/

const API_ROOT = process.env.REACT_APP_API || "http://localhost:5211";
const TOKEN_KEY = "token";

let authToken = localStorage.getItem(TOKEN_KEY) || null;

export const setToken = t => {
  console.log("[api] ▶️ setToken:", t);
  authToken = t;
  localStorage.setItem(TOKEN_KEY, t);
};

export const clearToken = () => {
  console.log("[api] 🗑 clearToken");
  authToken = null;
  localStorage.removeItem(TOKEN_KEY);
};

async function request(method, path, body) {
  console.log(`[api] ➡️ ${method} ${path} (authToken=${authToken})`, body);

  const res = await fetch(`${API_ROOT}${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(authToken ? { Authorization: `Bearer ${authToken}` } : {})
    },
    body: body ? JSON.stringify(body) : undefined
  });

  console.log(`[api] ⬅️ ${res.status} ${method} ${path}`);
  if (res.status === 401) {
    console.log("[api] got 401, clearing token & redirecting");
    clearToken();
    throw new Error("Unauthorized");
  }

  if (!res.ok) {
    const text = await res.text();
    console.log("[api] error body:", text);
    throw new Error(text || res.statusText);
  }

  // 204 No Content
  if (res.status === 204) return null;

  const json = await res.json();
  console.log("[api] JSON:", json);
  return json;
}

/* Convenience helpers */
export const get   = path        => request("GET",    path);
export const post  = (path, body) => request("POST",   path, body);
export const put   = (path, body) => request("PUT",    path, body);
export const del   = path        => request("DELETE", path);
export const patch = (path, body) => request("PATCH",  path, body);
