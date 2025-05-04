// File: src/components/LoginPage/LoginPageItems/Login/Login.js
import React, { useState } from "react";
import "./Login.css";

export default function Login({ onToggle, onLogin }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const submit = (e) => {
    e.preventDefault();
    if (!username || !password) return;
    onLogin(username.trim(), password);
  };

  return (
    <div className="login-container">
      <h1>Log in</h1>

      <form onSubmit={submit}>
        <label>Username</label>
        <input
          required
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          placeholder="Your username"
        />

        <label>Password</label>
        <input
          type="password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="••••••••"
        />

        <button type="submit" className="login-btn">
          Next
        </button>
      </form>

      <div className="create-link">
        Need an account? <span onClick={onToggle}>Sign up</span>
      </div>
    </div>
  );
}
