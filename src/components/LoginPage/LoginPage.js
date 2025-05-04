// File: src/components/LoginPage/LoginPage.js
import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import Login          from "./LoginPageItems/Login/Login";
import CreateAccount  from "./LoginPageItems/CreateAccount/CreateAccount";
import "./LoginPage.css";

export default function LoginPage() {
  const nav = useNavigate();
  const [showLogin, setShowLogin] = useState(true);

  const toggle = () => setShowLogin((v) => !v);

  /* receive creds from <Login /> */
  const handleLogin = (username, password) => {
    /* keep them if user refreshes on /enter‑pin */
    sessionStorage.setItem("auth_username", username);
    sessionStorage.setItem("auth_password", password);

    nav("/enter-pin", { state: { username, password } });
  };

  return (
    <div className="login-page">
      {showLogin ? (
        <Login onToggle={toggle} onLogin={handleLogin} />
      ) : (
        <CreateAccount onToggle={toggle} />
      )}
    </div>
  );
}
