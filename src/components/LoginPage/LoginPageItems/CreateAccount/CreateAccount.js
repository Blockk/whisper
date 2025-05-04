// File: src/components/LoginPage/LoginPageItems/CreateAccount/CreateAccount.js
import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import "./CreateAccount.css";

export default function CreateAccount({ onToggle }) {
  const nav = useNavigate();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const submit = (e) => {
    e.preventDefault();
    if (!username || !password) return;

    sessionStorage.setItem("auth_username", username.trim());
    sessionStorage.setItem("auth_password", password);

    nav("/create-pin", { state: { username: username.trim(), password } });
  };

  return (
    <div className="create-account-container">
      <h1>Create an account</h1>

      <form onSubmit={submit}>
        <label>Username</label>
        <input
          required
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          placeholder="Choose a username"
        />

        <label>Password</label>
        <input
          type="password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Create a password"
        />

        <button type="submit" className="create-account-btn">
          Next
        </button>
      </form>

      <div className="login-link">
        Already have an account? <span onClick={onToggle}>Log in</span>
      </div>
    </div>
  );
}
