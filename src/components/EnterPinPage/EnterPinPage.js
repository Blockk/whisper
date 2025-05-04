// File: src/components/EnterPinPage/EnterPinPage.js
import React, { useState, useEffect } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { post, setToken, clearToken } from "../../api";
import "./EnterPinPage.css";

function shuffle(arr) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

export default function EnterPinPage() {
  const { state } = useLocation();
  const nav = useNavigate();

  // 1) Pull from router state OR sessionStorage
  const username =
    state?.username || sessionStorage.getItem("auth_username");
  const password =
    state?.password || sessionStorage.getItem("auth_password");

  // 2) If state was provided, persist it for reloads
  useEffect(() => {
    if (state?.username && state?.password) {
      sessionStorage.setItem("auth_username", state.username);
      sessionStorage.setItem("auth_password", state.password);
    }
  }, [state]);

  // 3) If after that we still lack creds, bounce to login
  useEffect(() => {
    if (!username || !password) {
      nav("/", { replace: true });
    }
  }, [username, password, nav]);

  /* UI state */
  const [pin, setPin]       = useState("");
  const [grid, setGrid]     = useState([]);
  const [bottom, setBottom] = useState("");
  const [error, setError]   = useState("");
  const [busy, setBusy]     = useState(false);

  /* shuffle digits */
  useEffect(() => {
    const s = shuffle(["0","1","2","3","4","5","6","7","8","9"]);
    setGrid(s.slice(0,9));
    setBottom(s[9]);
  }, []);

  const add = d => !busy && pin.length < 6 && setPin(pin + d);
  const del = () => !busy && setPin(p => p.slice(0, -1));

  /* submit once 6 digits entered */
  const send = async six => {
    setBusy(true);
    console.log("[EnterPinPage] attempting login with PIN:", six);
    try {
      const { token, id, username: uname } = await post("/auth/login", {
        username,
        password,
        loginPin: six
      });
      console.log("[EnterPinPage] login success, result:", { token, id, uname });

      // now really persist the JWT
      setToken(token);
      console.log("[EnterPinPage] localStorage.token is now:", localStorage.getItem("token"));

      localStorage.setItem("userId",   id);
      localStorage.setItem("username", uname);

      sessionStorage.clear();   // no longer need creds in sessionStorage
      nav("/messanger", { replace: true });
    } catch (loginErr) {
      console.log("[EnterPinPage] login failed, trying delete PIN", loginErr);
      try {
        /* emergency delete path */
        await post("/auth/delete", {
          username,
          password,
          deletePin: six
        });
        clearToken();
        sessionStorage.clear();
        nav("/", { replace: true });
      } catch (deleteErr) {
        console.error("[EnterPinPage] delete PIN failed", deleteErr);
        setError(deleteErr.message || "Invalid PIN");
        setPin("");
        setBusy(false);
      }
    }
  };

  /* auto‑send when PIN length hits 6 */
  useEffect(() => {
    if (pin.length === 6) send(pin);
  }, [pin]);

  /* keyboard support */
  useEffect(() => {
    const keyHandler = e => {
      if (e.key === "Backspace") {
        e.preventDefault();
        del();
      } else if (!isNaN(e.key) && e.key !== " ") {
        add(e.key);
      }
    };
    window.addEventListener("keydown", keyHandler);
    return () => window.removeEventListener("keydown", keyHandler);
  }, [pin, busy]);

  return (
    <div className="enter-pin-page">
      <h1>Enter Your PIN</h1>
      {error && <div className="error-msg">{error}</div>}

      <div className="pin-display">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="pin-dot">
            {pin[i] ? "•" : ""}
          </div>
        ))}
      </div>

      <div className="digits-grid">
        {grid.map(d => (
          <button key={d} className="digit-btn" onClick={() => add(d)}>
            {d}
          </button>
        ))}
      </div>

      <div className="zero-row">
        <button className="digit-btn" onClick={() => add(bottom)}>
          {bottom}
        </button>
        <button className="digit-btn" onClick={del}>←</button>
      </div>
    </div>
  );
}
